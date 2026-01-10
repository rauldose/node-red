// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using NodeRed.Core.Entities;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Services;

/// <summary>
/// In-memory project service implementation.
/// For production, implement file-based storage with Git integration.
/// </summary>
public class ProjectService : IProjectService
{
    private readonly Dictionary<string, Project> _projects = new();
    private readonly Dictionary<string, string> _activeProjects = new(); // userId -> projectId
    private readonly IGitService _gitService;
    private readonly string _projectsBasePath;
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new project service.
    /// </summary>
    /// <param name="gitService">Git service for repository operations.</param>
    /// <param name="projectsBasePath">Base path for storing projects.</param>
    public ProjectService(IGitService gitService, string? projectsBasePath = null)
    {
        _gitService = gitService;
        _projectsBasePath = projectsBasePath ?? Path.Combine(Path.GetTempPath(), "node-red-projects");
        
        // Ensure projects directory exists
        if (!Directory.Exists(_projectsBasePath))
        {
            Directory.CreateDirectory(_projectsBasePath);
        }
    }

    /// <inheritdoc />
    public Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<ProjectListResponse> ListProjectsAsync(string userId)
    {
        lock (_lock)
        {
            var response = new ProjectListResponse
            {
                Projects = _projects.Values.ToList()
            };

            if (_activeProjects.TryGetValue(userId, out var activeId))
            {
                var activeProject = _projects.GetValueOrDefault(activeId);
                response.Active = activeProject?.Name;
            }

            return Task.FromResult(response);
        }
    }

    /// <inheritdoc />
    public async Task<Project> CreateProjectAsync(string userId, ProjectCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Project name is required", nameof(request));
        }

        var projectId = GenerateProjectId(request.Name);
        var projectPath = GetProjectPath(projectId);

        // Check if project already exists
        lock (_lock)
        {
            if (_projects.Values.Any(p => p.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Project '{request.Name}' already exists");
            }
        }

        // Create project directory
        Directory.CreateDirectory(projectPath);

        var project = new Project
        {
            Id = projectId,
            Name = request.Name,
            Summary = request.Summary ?? string.Empty,
            Files = new ProjectFiles
            {
                Flow = request.FlowFile ?? "flows.json",
                Credentials = request.CredentialsFile ?? "flows_cred.json"
            }
        };

        // Clone from Git URL if provided
        if (!string.IsNullOrEmpty(request.GitUrl))
        {
            await CloneProjectAsync(projectPath, request.GitUrl, request.Credentials);
            project.Git = new ProjectGitConfig
            {
                Remotes = new List<GitRemote>
                {
                    new GitRemote { Name = "origin", Url = request.GitUrl }
                }
            };
        }
        else
        {
            // Initialize Git repository
            await _gitService.InitAsync(projectPath);
            project.Git = new ProjectGitConfig();
        }

        // Create default files
        await CreateDefaultFilesAsync(projectPath, project.Files);

        lock (_lock)
        {
            _projects[projectId] = project;
        }

        return project;
    }

    /// <inheritdoc />
    public async Task<Project> InitializeProjectAsync(string userId, string projectId, ProjectInitOptions options)
    {
        Project? project;
        lock (_lock)
        {
            project = _projects.GetValueOrDefault(projectId);
        }

        if (project == null)
        {
            throw new KeyNotFoundException($"Project '{projectId}' not found");
        }

        var projectPath = GetProjectPath(projectId);

        // Update project files configuration
        project.Files = new ProjectFiles
        {
            Flow = options.FlowFile,
            Credentials = options.CredentialsFile
        };

        // Initialize Git if requested
        if (options.InitGit && project.Git == null)
        {
            await _gitService.InitAsync(projectPath);
            project.Git = new ProjectGitConfig();
        }

        // Create default files
        await CreateDefaultFilesAsync(projectPath, project.Files);

        project.ModifiedAt = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            _projects[projectId] = project;
        }

        return project;
    }

    /// <inheritdoc />
    public Task<Project?> GetActiveProjectAsync(string userId)
    {
        lock (_lock)
        {
            if (_activeProjects.TryGetValue(userId, out var projectId))
            {
                return Task.FromResult(_projects.GetValueOrDefault(projectId));
            }
            return Task.FromResult<Project?>(null);
        }
    }

    /// <inheritdoc />
    public Task SetActiveProjectAsync(string userId, string projectId, bool clearContext = false)
    {
        lock (_lock)
        {
            if (!_projects.ContainsKey(projectId))
            {
                throw new KeyNotFoundException($"Project '{projectId}' not found");
            }

            _activeProjects[userId] = projectId;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Project?> GetProjectAsync(string userId, string projectId)
    {
        lock (_lock)
        {
            return Task.FromResult(_projects.GetValueOrDefault(projectId));
        }
    }

    /// <inheritdoc />
    public Task<Project> UpdateProjectAsync(string userId, string projectId, ProjectUpdateRequest updates)
    {
        lock (_lock)
        {
            var project = _projects.GetValueOrDefault(projectId);
            if (project == null)
            {
                throw new KeyNotFoundException($"Project '{projectId}' not found");
            }

            if (!string.IsNullOrWhiteSpace(updates.Name))
                project.Name = updates.Name;
            if (updates.Summary != null)
                project.Summary = updates.Summary;
            if (updates.Description != null)
                project.Description = updates.Description;
            if (!string.IsNullOrWhiteSpace(updates.Version))
                project.Version = updates.Version;
            if (updates.Dependencies != null)
                project.Dependencies = updates.Dependencies;

            project.ModifiedAt = DateTimeOffset.UtcNow;
            _projects[projectId] = project;

            return Task.FromResult(project);
        }
    }

    /// <inheritdoc />
    public Task DeleteProjectAsync(string userId, string projectId)
    {
        lock (_lock)
        {
            if (!_projects.ContainsKey(projectId))
            {
                throw new KeyNotFoundException($"Project '{projectId}' not found");
            }

            // Remove from active projects if active
            var usersToRemove = _activeProjects.Where(kv => kv.Value == projectId).Select(kv => kv.Key).ToList();
            foreach (var user in usersToRemove)
            {
                _activeProjects.Remove(user);
            }

            _projects.Remove(projectId);
        }

        // Delete project directory
        var projectPath = GetProjectPath(projectId);
        if (Directory.Exists(projectPath))
        {
            Directory.Delete(projectPath, true);
        }

        return Task.CompletedTask;
    }

    private string GetProjectPath(string projectId)
    {
        return Path.Combine(_projectsBasePath, projectId);
    }

    private static string GenerateProjectId(string name)
    {
        // Create URL-safe ID from name
        var id = name.ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('_', '-');
        
        // Remove invalid characters
        id = new string(id.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        
        // Ensure uniqueness with timestamp
        return $"{id}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }

    private static async Task CloneProjectAsync(string projectPath, string gitUrl, GitCredentials? credentials)
    {
        // This would use LibGit2Sharp or git CLI in a real implementation
        // For now, just create the directory structure
        await Task.CompletedTask;
    }

    private static async Task CreateDefaultFilesAsync(string projectPath, ProjectFiles files)
    {
        // Create flow file
        var flowPath = Path.Combine(projectPath, files.Flow);
        if (!File.Exists(flowPath))
        {
            await File.WriteAllTextAsync(flowPath, "[]");
        }

        // Create credentials file
        var credPath = Path.Combine(projectPath, files.Credentials);
        if (!File.Exists(credPath))
        {
            await File.WriteAllTextAsync(credPath, "{}");
        }

        // Create package.json
        var packagePath = Path.Combine(projectPath, "package.json");
        if (!File.Exists(packagePath))
        {
            var package = new
            {
                name = Path.GetFileName(projectPath),
                description = "A Node-RED Project",
                version = "0.0.1",
                dependencies = new Dictionary<string, string>()
            };
            await File.WriteAllTextAsync(packagePath, JsonSerializer.Serialize(package, new JsonSerializerOptions { WriteIndented = true }));
        }

        // Create .gitignore
        var gitignorePath = Path.Combine(projectPath, ".gitignore");
        if (!File.Exists(gitignorePath))
        {
            await File.WriteAllTextAsync(gitignorePath, $"# Credentials file\n{files.Credentials}\n");
        }

        // Create README.md
        var readmePath = Path.Combine(projectPath, "README.md");
        if (!File.Exists(readmePath))
        {
            await File.WriteAllTextAsync(readmePath, $"# {Path.GetFileName(projectPath)}\n\nA Node-RED Project\n");
        }
    }
}
