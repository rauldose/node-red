// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;

namespace NodeRed.Core.Interfaces;

/// <summary>
/// Interface for project management operations.
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// Checks if projects feature is available.
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Lists all projects.
    /// </summary>
    /// <param name="userId">User ID making the request.</param>
    Task<ProjectListResponse> ListProjectsAsync(string userId);

    /// <summary>
    /// Creates a new project.
    /// </summary>
    /// <param name="userId">User ID making the request.</param>
    /// <param name="project">Project configuration.</param>
    Task<Project> CreateProjectAsync(string userId, ProjectCreateRequest project);

    /// <summary>
    /// Initializes an empty project with default files.
    /// </summary>
    /// <param name="userId">User ID making the request.</param>
    /// <param name="projectId">Project ID.</param>
    /// <param name="options">Initialization options.</param>
    Task<Project> InitializeProjectAsync(string userId, string projectId, ProjectInitOptions options);

    /// <summary>
    /// Gets the active project.
    /// </summary>
    /// <param name="userId">User ID making the request.</param>
    Task<Project?> GetActiveProjectAsync(string userId);

    /// <summary>
    /// Sets the active project.
    /// </summary>
    /// <param name="userId">User ID making the request.</param>
    /// <param name="projectId">Project ID to activate.</param>
    /// <param name="clearContext">Whether to clear context on switch.</param>
    Task SetActiveProjectAsync(string userId, string projectId, bool clearContext = false);

    /// <summary>
    /// Gets a project by ID.
    /// </summary>
    /// <param name="userId">User ID making the request.</param>
    /// <param name="projectId">Project ID.</param>
    Task<Project?> GetProjectAsync(string userId, string projectId);

    /// <summary>
    /// Updates a project.
    /// </summary>
    /// <param name="userId">User ID making the request.</param>
    /// <param name="projectId">Project ID.</param>
    /// <param name="updates">Update data.</param>
    Task<Project> UpdateProjectAsync(string userId, string projectId, ProjectUpdateRequest updates);

    /// <summary>
    /// Deletes a project.
    /// </summary>
    /// <param name="userId">User ID making the request.</param>
    /// <param name="projectId">Project ID.</param>
    Task DeleteProjectAsync(string userId, string projectId);
}

/// <summary>
/// Interface for Git operations on projects.
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Initializes a Git repository for a project.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    Task InitAsync(string projectPath);

    /// <summary>
    /// Gets the current Git status.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="includeRemote">Whether to fetch remote status.</param>
    Task<GitStatus> GetStatusAsync(string projectPath, bool includeRemote = false);

    /// <summary>
    /// Gets list of branches.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="remote">Whether to get remote branches.</param>
    Task<List<GitBranch>> GetBranchesAsync(string projectPath, bool remote = false);

    /// <summary>
    /// Gets branch status (ahead/behind).
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="branch">Branch name.</param>
    Task<GitBranch> GetBranchStatusAsync(string projectPath, string branch);

    /// <summary>
    /// Switches to a branch.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="branch">Branch name.</param>
    /// <param name="create">Whether to create the branch if it doesn't exist.</param>
    Task SetBranchAsync(string projectPath, string branch, bool create = false);

    /// <summary>
    /// Deletes a branch.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="branch">Branch name.</param>
    /// <param name="force">Whether to force delete.</param>
    Task DeleteBranchAsync(string projectPath, string branch, bool force = false);

    /// <summary>
    /// Stages files for commit.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="paths">File paths to stage (null for all).</param>
    Task StageAsync(string projectPath, IEnumerable<string>? paths = null);

    /// <summary>
    /// Unstages files.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="paths">File paths to unstage (null for all).</param>
    Task UnstageAsync(string projectPath, IEnumerable<string>? paths = null);

    /// <summary>
    /// Creates a commit.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="message">Commit message.</param>
    /// <param name="author">Author name.</param>
    /// <param name="email">Author email.</param>
    Task<GitCommit> CommitAsync(string projectPath, string message, string? author = null, string? email = null);

    /// <summary>
    /// Gets a specific commit.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="sha">Commit SHA.</param>
    Task<GitCommit> GetCommitAsync(string projectPath, string sha);

    /// <summary>
    /// Gets commit history.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="limit">Maximum number of commits.</param>
    /// <param name="before">Get commits before this SHA.</param>
    Task<List<GitCommit>> GetCommitsAsync(string projectPath, int limit = 20, string? before = null);

    /// <summary>
    /// Reverts a file to its committed state.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="path">File path.</param>
    Task RevertFileAsync(string projectPath, string path);

    /// <summary>
    /// Gets the diff for a file.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="path">File path.</param>
    /// <param name="type">Diff type (staged, unstaged).</param>
    Task<FileDiff> GetFileDiffAsync(string projectPath, string path, string type = "unstaged");

    /// <summary>
    /// Gets list of remotes.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    Task<List<GitRemote>> GetRemotesAsync(string projectPath);

    /// <summary>
    /// Adds a remote.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="remote">Remote configuration.</param>
    Task AddRemoteAsync(string projectPath, GitRemote remote);

    /// <summary>
    /// Removes a remote.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="name">Remote name.</param>
    Task RemoveRemoteAsync(string projectPath, string name);

    /// <summary>
    /// Updates a remote.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="name">Remote name.</param>
    /// <param name="remote">New remote configuration.</param>
    Task UpdateRemoteAsync(string projectPath, string name, GitRemote remote);

    /// <summary>
    /// Pulls from a remote.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="remote">Remote name.</param>
    /// <param name="track">Whether to track the remote branch.</param>
    /// <param name="allowUnrelatedHistories">Whether to allow unrelated histories.</param>
    Task PullAsync(string projectPath, string remote, bool track = false, bool allowUnrelatedHistories = false);

    /// <summary>
    /// Pushes to a remote.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="remote">Remote name.</param>
    /// <param name="track">Whether to set upstream.</param>
    Task PushAsync(string projectPath, string remote, bool track = false);

    /// <summary>
    /// Aborts an in-progress merge.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    Task AbortMergeAsync(string projectPath);

    /// <summary>
    /// Resolves a merge conflict.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="path">File path with conflict.</param>
    /// <param name="resolution">Resolution strategy (ours, theirs, manual).</param>
    Task ResolveMergeAsync(string projectPath, string path, string resolution);

    /// <summary>
    /// Lists files in the project.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    Task<List<string>> GetFilesAsync(string projectPath);

    /// <summary>
    /// Gets file contents.
    /// </summary>
    /// <param name="projectPath">Project directory path.</param>
    /// <param name="path">File path.</param>
    /// <param name="tree">Git tree (HEAD, staging area).</param>
    Task<string> GetFileContentAsync(string projectPath, string path, string? tree = null);
}

/// <summary>
/// Response for project list.
/// </summary>
public class ProjectListResponse
{
    /// <summary>
    /// List of projects.
    /// </summary>
    public List<Project> Projects { get; set; } = new();

    /// <summary>
    /// Active project name (if any).
    /// </summary>
    public string? Active { get; set; }
}

/// <summary>
/// Request to create a project.
/// </summary>
public class ProjectCreateRequest
{
    /// <summary>
    /// Project name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Project summary.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Clone from Git URL (for cloning existing repo).
    /// </summary>
    public string? GitUrl { get; set; }

    /// <summary>
    /// Git credentials (if needed).
    /// </summary>
    public GitCredentials? Credentials { get; set; }

    /// <summary>
    /// Flow file path.
    /// </summary>
    public string? FlowFile { get; set; }

    /// <summary>
    /// Credentials file path.
    /// </summary>
    public string? CredentialsFile { get; set; }
}

/// <summary>
/// Git credentials for authentication.
/// </summary>
public class GitCredentials
{
    /// <summary>
    /// Username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password or token.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// SSH key path.
    /// </summary>
    public string? SshKeyPath { get; set; }

    /// <summary>
    /// SSH passphrase.
    /// </summary>
    public string? SshPassphrase { get; set; }
}

/// <summary>
/// Options for initializing a project.
/// </summary>
public class ProjectInitOptions
{
    /// <summary>
    /// Flow file name.
    /// </summary>
    public string FlowFile { get; set; } = "flows.json";

    /// <summary>
    /// Credentials file name.
    /// </summary>
    public string CredentialsFile { get; set; } = "flows_cred.json";

    /// <summary>
    /// Initialize Git repository.
    /// </summary>
    public bool InitGit { get; set; } = true;
}

/// <summary>
/// Request to update a project.
/// </summary>
public class ProjectUpdateRequest
{
    /// <summary>
    /// New project name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// New project summary.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// New project description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// New project version.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Updated dependencies.
    /// </summary>
    public Dictionary<string, string>? Dependencies { get; set; }
}
