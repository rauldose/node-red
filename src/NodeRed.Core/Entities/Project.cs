// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Core.Entities;

/// <summary>
/// Represents a Node-RED project with Git integration.
/// </summary>
public class Project
{
    /// <summary>
    /// Project ID (unique identifier).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Project name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Project description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Project summary (short description).
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Project version.
    /// </summary>
    public string Version { get; set; } = "0.0.1";

    /// <summary>
    /// Dependencies (node modules required by this project).
    /// </summary>
    public Dictionary<string, string> Dependencies { get; set; } = new();

    /// <summary>
    /// Git configuration for the project.
    /// </summary>
    public ProjectGitConfig? Git { get; set; }

    /// <summary>
    /// Files in the project.
    /// </summary>
    public ProjectFiles Files { get; set; } = new();

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Last modified timestamp.
    /// </summary>
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Git configuration for a project.
/// </summary>
public class ProjectGitConfig
{
    /// <summary>
    /// List of configured remotes.
    /// </summary>
    public List<GitRemote> Remotes { get; set; } = new();

    /// <summary>
    /// Current branch name.
    /// </summary>
    public string? CurrentBranch { get; set; }
}

/// <summary>
/// Git remote configuration.
/// </summary>
public class GitRemote
{
    /// <summary>
    /// Remote name (e.g., "origin").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Remote URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Fetch URL (optional, defaults to Url).
    /// </summary>
    public string? FetchUrl { get; set; }

    /// <summary>
    /// Push URL (optional, defaults to Url).
    /// </summary>
    public string? PushUrl { get; set; }
}

/// <summary>
/// Project files configuration.
/// </summary>
public class ProjectFiles
{
    /// <summary>
    /// Flow file name.
    /// </summary>
    public string Flow { get; set; } = "flows.json";

    /// <summary>
    /// Credentials file name.
    /// </summary>
    public string Credentials { get; set; } = "flows_cred.json";
}

/// <summary>
/// Git status information.
/// </summary>
public class GitStatus
{
    /// <summary>
    /// Current branch name.
    /// </summary>
    public string Branch { get; set; } = string.Empty;

    /// <summary>
    /// Whether the repository has uncommitted changes.
    /// </summary>
    public bool HasChanges { get; set; }

    /// <summary>
    /// Number of commits ahead of remote.
    /// </summary>
    public int Ahead { get; set; }

    /// <summary>
    /// Number of commits behind remote.
    /// </summary>
    public int Behind { get; set; }

    /// <summary>
    /// Files with uncommitted changes.
    /// </summary>
    public GitFileStatus Files { get; set; } = new();
}

/// <summary>
/// Git file status (staged/unstaged/untracked).
/// </summary>
public class GitFileStatus
{
    /// <summary>
    /// Staged files.
    /// </summary>
    public List<string> Staged { get; set; } = new();

    /// <summary>
    /// Unstaged (modified) files.
    /// </summary>
    public List<string> Unstaged { get; set; } = new();

    /// <summary>
    /// Untracked files.
    /// </summary>
    public List<string> Untracked { get; set; } = new();

    /// <summary>
    /// Conflicted files.
    /// </summary>
    public List<string> Conflicted { get; set; } = new();
}

/// <summary>
/// Git branch information.
/// </summary>
public class GitBranch
{
    /// <summary>
    /// Branch name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is the current branch.
    /// </summary>
    public bool Current { get; set; }

    /// <summary>
    /// Tracking remote branch (if any).
    /// </summary>
    public string? Remote { get; set; }

    /// <summary>
    /// Commit SHA of the branch head.
    /// </summary>
    public string CommitSha { get; set; } = string.Empty;
}

/// <summary>
/// Git commit information.
/// </summary>
public class GitCommit
{
    /// <summary>
    /// Commit SHA.
    /// </summary>
    public string Sha { get; set; } = string.Empty;

    /// <summary>
    /// Commit message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Author name.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Author email.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Commit timestamp.
    /// </summary>
    public DateTimeOffset Date { get; set; }

    /// <summary>
    /// Files changed in this commit.
    /// </summary>
    public List<CommitFile> Files { get; set; } = new();
}

/// <summary>
/// File changed in a commit.
/// </summary>
public class CommitFile
{
    /// <summary>
    /// File path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Change type (added, modified, deleted, renamed).
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// File difference information.
/// </summary>
public class FileDiff
{
    /// <summary>
    /// File path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Diff content (unified diff format).
    /// </summary>
    public string Diff { get; set; } = string.Empty;

    /// <summary>
    /// Number of lines added.
    /// </summary>
    public int Additions { get; set; }

    /// <summary>
    /// Number of lines deleted.
    /// </summary>
    public int Deletions { get; set; }
}
