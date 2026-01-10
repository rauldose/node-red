// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Interfaces;
using NodeRed.Runtime.Services;
using Xunit;

namespace NodeRed.Tests.Services;

/// <summary>
/// Unit tests for ProjectService.
/// </summary>
public class ProjectServiceTests
{
    private readonly IGitService _gitService;
    private readonly string _testProjectsPath;

    public ProjectServiceTests()
    {
        _testProjectsPath = Path.Combine(Path.GetTempPath(), $"node-red-test-{Guid.NewGuid()}");
        _gitService = new MockGitService();
    }

    private ProjectService CreateService()
    {
        return new ProjectService(_gitService, _testProjectsPath);
    }

    [Fact]
    public async Task IsAvailable_ReturnsTrue()
    {
        var service = CreateService();

        var result = await service.IsAvailableAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task CreateProject_CreatesNewProject()
    {
        var service = CreateService();
        var request = new ProjectCreateRequest
        {
            Name = "Test Project",
            Summary = "A test project"
        };

        var project = await service.CreateProjectAsync("user1", request);

        Assert.NotNull(project);
        Assert.Equal("Test Project", project.Name);
        Assert.Equal("A test project", project.Summary);
        Assert.NotEmpty(project.Id);
    }

    [Fact]
    public async Task CreateProject_WithGitUrl_ClonesRepository()
    {
        var service = CreateService();
        var request = new ProjectCreateRequest
        {
            Name = "Cloned Project",
            GitUrl = "https://github.com/test/repo.git"
        };

        var project = await service.CreateProjectAsync("user1", request);

        Assert.NotNull(project.Git);
        Assert.Single(project.Git.Remotes);
        Assert.Equal("origin", project.Git.Remotes[0].Name);
        Assert.Equal("https://github.com/test/repo.git", project.Git.Remotes[0].Url);
    }

    [Fact]
    public async Task CreateProject_DuplicateName_ThrowsException()
    {
        var service = CreateService();
        var request = new ProjectCreateRequest { Name = "Duplicate Project" };

        await service.CreateProjectAsync("user1", request);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateProjectAsync("user1", request));
    }

    [Fact]
    public async Task ListProjects_ReturnsAllProjects()
    {
        var service = CreateService();
        await service.CreateProjectAsync("user1", new ProjectCreateRequest { Name = "Project 1" });
        await service.CreateProjectAsync("user1", new ProjectCreateRequest { Name = "Project 2" });

        var result = await service.ListProjectsAsync("user1");

        Assert.Equal(2, result.Projects.Count);
    }

    [Fact]
    public async Task SetActiveProject_SetsActiveProject()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync("user1", new ProjectCreateRequest { Name = "Active Project" });

        await service.SetActiveProjectAsync("user1", project.Id);
        var result = await service.ListProjectsAsync("user1");

        Assert.Equal("Active Project", result.Active);
    }

    [Fact]
    public async Task GetActiveProject_ReturnsActiveProject()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync("user1", new ProjectCreateRequest { Name = "My Project" });
        await service.SetActiveProjectAsync("user1", project.Id);

        var active = await service.GetActiveProjectAsync("user1");

        Assert.NotNull(active);
        Assert.Equal(project.Id, active.Id);
    }

    [Fact]
    public async Task GetProject_ReturnsProject()
    {
        var service = CreateService();
        var created = await service.CreateProjectAsync("user1", new ProjectCreateRequest { Name = "Get Test" });

        var project = await service.GetProjectAsync("user1", created.Id);

        Assert.NotNull(project);
        Assert.Equal("Get Test", project.Name);
    }

    [Fact]
    public async Task UpdateProject_UpdatesFields()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync("user1", new ProjectCreateRequest { Name = "Original" });

        var updated = await service.UpdateProjectAsync("user1", project.Id, new ProjectUpdateRequest
        {
            Name = "Updated",
            Summary = "New summary",
            Version = "1.0.0"
        });

        Assert.Equal("Updated", updated.Name);
        Assert.Equal("New summary", updated.Summary);
        Assert.Equal("1.0.0", updated.Version);
    }

    [Fact]
    public async Task DeleteProject_RemovesProject()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync("user1", new ProjectCreateRequest { Name = "To Delete" });

        await service.DeleteProjectAsync("user1", project.Id);
        var result = await service.GetProjectAsync("user1", project.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task InitializeProject_CreatesDefaultFiles()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync("user1", new ProjectCreateRequest { Name = "Init Test" });

        var initialized = await service.InitializeProjectAsync("user1", project.Id, new ProjectInitOptions
        {
            FlowFile = "my-flows.json",
            CredentialsFile = "my-creds.json",
            InitGit = true
        });

        Assert.Equal("my-flows.json", initialized.Files.Flow);
        Assert.Equal("my-creds.json", initialized.Files.Credentials);
    }

    [Fact]
    public async Task SetActiveProject_NonExistent_ThrowsException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SetActiveProjectAsync("user1", "non-existent"));
    }
}

/// <summary>
/// Mock Git service for testing.
/// </summary>
public class MockGitService : IGitService
{
    public Task InitAsync(string projectPath) => Task.CompletedTask;
    
    public Task<GitStatus> GetStatusAsync(string projectPath, bool includeRemote = false)
    {
        return Task.FromResult(new GitStatus
        {
            Branch = "main",
            HasChanges = false
        });
    }

    public Task<List<GitBranch>> GetBranchesAsync(string projectPath, bool remote = false)
    {
        return Task.FromResult(new List<GitBranch>
        {
            new() { Name = "main", Current = true, CommitSha = "abc123" }
        });
    }

    public Task<GitBranch> GetBranchStatusAsync(string projectPath, string branch)
    {
        return Task.FromResult(new GitBranch { Name = branch, CommitSha = "abc123" });
    }

    public Task SetBranchAsync(string projectPath, string branch, bool create = false) => Task.CompletedTask;
    public Task DeleteBranchAsync(string projectPath, string branch, bool force = false) => Task.CompletedTask;
    public Task StageAsync(string projectPath, IEnumerable<string>? paths = null) => Task.CompletedTask;
    public Task UnstageAsync(string projectPath, IEnumerable<string>? paths = null) => Task.CompletedTask;

    public Task<GitCommit> CommitAsync(string projectPath, string message, string? author = null, string? email = null)
    {
        return Task.FromResult(new GitCommit
        {
            Sha = Guid.NewGuid().ToString("N")[..8],
            Message = message,
            Author = author ?? "Test",
            Email = email ?? "test@test.com",
            Date = DateTimeOffset.UtcNow
        });
    }

    public Task<GitCommit> GetCommitAsync(string projectPath, string sha)
    {
        return Task.FromResult(new GitCommit
        {
            Sha = sha,
            Message = "Test commit",
            Author = "Test",
            Email = "test@test.com",
            Date = DateTimeOffset.UtcNow
        });
    }

    public Task<List<GitCommit>> GetCommitsAsync(string projectPath, int limit = 20, string? before = null)
    {
        return Task.FromResult(new List<GitCommit>
        {
            new() { Sha = "abc123", Message = "Initial commit", Author = "Test", Date = DateTimeOffset.UtcNow }
        });
    }

    public Task RevertFileAsync(string projectPath, string path) => Task.CompletedTask;

    public Task<FileDiff> GetFileDiffAsync(string projectPath, string path, string type = "unstaged")
    {
        return Task.FromResult(new FileDiff { Path = path, Diff = "", Additions = 0, Deletions = 0 });
    }

    public Task<List<GitRemote>> GetRemotesAsync(string projectPath)
    {
        return Task.FromResult(new List<GitRemote>());
    }

    public Task AddRemoteAsync(string projectPath, GitRemote remote) => Task.CompletedTask;
    public Task RemoveRemoteAsync(string projectPath, string name) => Task.CompletedTask;
    public Task UpdateRemoteAsync(string projectPath, string name, GitRemote remote) => Task.CompletedTask;
    public Task PullAsync(string projectPath, string remote, bool track = false, bool allowUnrelatedHistories = false) => Task.CompletedTask;
    public Task PushAsync(string projectPath, string remote, bool track = false) => Task.CompletedTask;
    public Task AbortMergeAsync(string projectPath) => Task.CompletedTask;
    public Task ResolveMergeAsync(string projectPath, string path, string resolution) => Task.CompletedTask;

    public Task<List<string>> GetFilesAsync(string projectPath)
    {
        return Task.FromResult(new List<string> { "flows.json", "package.json" });
    }

    public Task<string> GetFileContentAsync(string projectPath, string path, string? tree = null)
    {
        return Task.FromResult("{}");
    }
}
