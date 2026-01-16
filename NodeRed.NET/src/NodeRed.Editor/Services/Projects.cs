// Source: @node-red/editor-client/src/js/ui/projects/*.js
// Translated to C# for NodeRed.NET
namespace NodeRed.Editor.Services;

/// <summary>
/// Project management system.
/// Translated from RED.projects module.
/// </summary>
public class Projects
{
    private readonly EditorState _state;
    private Project? _activeProject;
    private readonly List<Project> _projects = new();
    
    public event Action<Project?>? OnProjectChanged;
    public event Action<List<Project>>? OnProjectsListChanged;
    
    public Projects(EditorState state)
    {
        _state = state;
    }
    
    /// <summary>
    /// Get the active project.
    /// Translated from: projects.getActiveProject = function()
    /// </summary>
    public Project? GetActiveProject() => _activeProject;
    
    /// <summary>
    /// Set the active project.
    /// </summary>
    public void SetActiveProject(Project? project)
    {
        _activeProject = project;
        OnProjectChanged?.Invoke(project);
    }
    
    /// <summary>
    /// Create a new project.
    /// Translated from: projects.createProject = function(options)
    /// </summary>
    public Project CreateProject(ProjectOptions options)
    {
        var project = new Project
        {
            Name = options.Name,
            Description = options.Description ?? "",
            Summary = options.Summary ?? "",
            Version = options.Version ?? "0.0.1",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Files = new ProjectFiles
            {
                Flow = "flows.json",
                Credentials = "flows_cred.json"
            },
            Git = new ProjectGit
            {
                RemoteUrl = options.GitUrl ?? "",
                Branch = options.GitBranch ?? "main"
            }
        };
        
        _projects.Add(project);
        OnProjectsListChanged?.Invoke(_projects);
        
        return project;
    }
    
    /// <summary>
    /// Get list of all projects.
    /// Translated from: projects.getProjects = function()
    /// </summary>
    public List<Project> GetProjects() => _projects;
    
    /// <summary>
    /// Delete a project.
    /// Translated from: projects.deleteProject = function(name)
    /// </summary>
    public bool DeleteProject(string name)
    {
        var project = _projects.FirstOrDefault(p => p.Name == name);
        if (project != null)
        {
            _projects.Remove(project);
            if (_activeProject == project)
            {
                _activeProject = null;
                OnProjectChanged?.Invoke(null);
            }
            OnProjectsListChanged?.Invoke(_projects);
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Get project settings.
    /// Translated from: projects.settings.get = function(project)
    /// </summary>
    public ProjectSettings GetSettings(Project project)
    {
        return project.Settings;
    }
    
    /// <summary>
    /// Update project settings.
    /// </summary>
    public void UpdateSettings(Project project, ProjectSettings settings)
    {
        project.Settings = settings;
        project.ModifiedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Get git status for a project.
    /// Translated from: projects.getStatus = function(project)
    /// </summary>
    public ProjectGitStatus GetGitStatus(Project project)
    {
        return new ProjectGitStatus
        {
            Branch = project.Git.Branch,
            RemoteUrl = project.Git.RemoteUrl,
            Commits = new ProjectCommitInfo
            {
                Ahead = 0,
                Behind = 0
            },
            Files = new ProjectFileStatus
            {
                Modified = new List<string>(),
                Added = new List<string>(),
                Deleted = new List<string>(),
                Untracked = new List<string>()
            }
        };
    }
}

public class Project
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Version { get; set; } = "0.0.1";
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public ProjectFiles Files { get; set; } = new();
    public ProjectGit Git { get; set; } = new();
    public ProjectSettings Settings { get; set; } = new();
}

public class ProjectFiles
{
    public string Flow { get; set; } = "flows.json";
    public string Credentials { get; set; } = "flows_cred.json";
    public string Package { get; set; } = "package.json";
    public string Readme { get; set; } = "README.md";
}

public class ProjectGit
{
    public string RemoteUrl { get; set; } = "";
    public string Branch { get; set; } = "main";
    public List<GitRemote> Remotes { get; set; } = new();
}

public class GitRemote
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

public class ProjectSettings
{
    public bool EncryptCredentials { get; set; } = true;
    public string? CredentialSecret { get; set; }
    public Dictionary<string, object> UserSettings { get; set; } = new();
}

public class ProjectOptions
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Summary { get; set; }
    public string? Version { get; set; }
    public string? GitUrl { get; set; }
    public string? GitBranch { get; set; }
}

public class ProjectGitStatus
{
    public string Branch { get; set; } = "";
    public string RemoteUrl { get; set; } = "";
    public ProjectCommitInfo Commits { get; set; } = new();
    public ProjectFileStatus Files { get; set; } = new();
}

public class ProjectCommitInfo
{
    public int Ahead { get; set; }
    public int Behind { get; set; }
}

public class ProjectFileStatus
{
    public List<string> Modified { get; set; } = new();
    public List<string> Added { get; set; } = new();
    public List<string> Deleted { get; set; } = new();
    public List<string> Untracked { get; set; } = new();
}
