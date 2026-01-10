// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using NodeRed.Core.Entities;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Services;

/// <summary>
/// Git service implementation using git CLI commands.
/// For production environments, consider using LibGit2Sharp for better performance.
/// </summary>
public partial class GitService : IGitService
{
    private readonly string _gitExecutable;

    /// <summary>
    /// Creates a new Git service.
    /// </summary>
    /// <param name="gitExecutable">Path to git executable (defaults to 'git').</param>
    public GitService(string gitExecutable = "git")
    {
        _gitExecutable = gitExecutable;
    }

    /// <inheritdoc />
    public async Task InitAsync(string projectPath)
    {
        await RunGitCommandAsync(projectPath, "init");
        
        // Configure user if not set globally
        try
        {
            await RunGitCommandAsync(projectPath, "config user.email");
        }
        catch
        {
            await RunGitCommandAsync(projectPath, "config user.email \"node-red@localhost\"");
            await RunGitCommandAsync(projectPath, "config user.name \"Node-RED\"");
        }
    }

    /// <inheritdoc />
    public async Task<GitStatus> GetStatusAsync(string projectPath, bool includeRemote = false)
    {
        var status = new GitStatus();

        // Get current branch
        try
        {
            var branchOutput = await RunGitCommandAsync(projectPath, "branch --show-current");
            status.Branch = branchOutput.Trim();
        }
        catch
        {
            status.Branch = "main";
        }

        // Get status
        var statusOutput = await RunGitCommandAsync(projectPath, "status --porcelain");
        var lines = statusOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.Length < 3) continue;

            var indexStatus = line[0];
            var workTreeStatus = line[1];
            var filePath = line.Substring(3).Trim();

            if (indexStatus != ' ' && indexStatus != '?')
            {
                status.Files.Staged.Add(filePath);
            }

            if (workTreeStatus == 'M' || workTreeStatus == 'D')
            {
                status.Files.Unstaged.Add(filePath);
            }

            if (indexStatus == '?' && workTreeStatus == '?')
            {
                status.Files.Untracked.Add(filePath);
            }

            if (indexStatus == 'U' || workTreeStatus == 'U')
            {
                status.Files.Conflicted.Add(filePath);
            }
        }

        status.HasChanges = status.Files.Staged.Count > 0 ||
                           status.Files.Unstaged.Count > 0 ||
                           status.Files.Untracked.Count > 0;

        // Get ahead/behind counts
        if (includeRemote)
        {
            try
            {
                await RunGitCommandAsync(projectPath, "fetch --quiet");
                var trackingOutput = await RunGitCommandAsync(projectPath, "rev-list --left-right --count HEAD...@{upstream}");
                var parts = trackingOutput.Trim().Split('\t');
                if (parts.Length == 2)
                {
                    int.TryParse(parts[0], out var ahead);
                    int.TryParse(parts[1], out var behind);
                    status.Ahead = ahead;
                    status.Behind = behind;
                }
            }
            catch
            {
                // No upstream configured
            }
        }

        return status;
    }

    /// <inheritdoc />
    public async Task<List<GitBranch>> GetBranchesAsync(string projectPath, bool remote = false)
    {
        var branches = new List<GitBranch>();
        var args = remote ? "branch -r --format=\"%(refname:short)|%(objectname:short)\"" 
                         : "branch --format=\"%(refname:short)|%(objectname:short)|%(HEAD)\"";

        var output = await RunGitCommandAsync(projectPath, args);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length >= 2)
            {
                branches.Add(new GitBranch
                {
                    Name = parts[0].Trim(),
                    CommitSha = parts[1].Trim(),
                    Current = parts.Length > 2 && parts[2].Trim() == "*",
                    Remote = remote ? parts[0].Split('/').FirstOrDefault() : null
                });
            }
        }

        return branches;
    }

    /// <inheritdoc />
    public async Task<GitBranch> GetBranchStatusAsync(string projectPath, string branch)
    {
        var branchInfo = new GitBranch { Name = branch };

        try
        {
            var shaOutput = await RunGitCommandAsync(projectPath, $"rev-parse {branch}");
            branchInfo.CommitSha = shaOutput.Trim();

            var currentOutput = await RunGitCommandAsync(projectPath, "branch --show-current");
            branchInfo.Current = currentOutput.Trim() == branch;

            var trackingOutput = await RunGitCommandAsync(projectPath, $"config branch.{branch}.remote");
            branchInfo.Remote = trackingOutput.Trim();
        }
        catch
        {
            // Branch may not exist or have tracking info
        }

        return branchInfo;
    }

    /// <inheritdoc />
    public async Task SetBranchAsync(string projectPath, string branch, bool create = false)
    {
        if (create)
        {
            await RunGitCommandAsync(projectPath, $"checkout -b {EscapeBranchName(branch)}");
        }
        else
        {
            await RunGitCommandAsync(projectPath, $"checkout {EscapeBranchName(branch)}");
        }
    }

    /// <inheritdoc />
    public async Task DeleteBranchAsync(string projectPath, string branch, bool force = false)
    {
        var flag = force ? "-D" : "-d";
        await RunGitCommandAsync(projectPath, $"branch {flag} {EscapeBranchName(branch)}");
    }

    /// <inheritdoc />
    public async Task StageAsync(string projectPath, IEnumerable<string>? paths = null)
    {
        if (paths == null || !paths.Any())
        {
            await RunGitCommandAsync(projectPath, "add -A");
        }
        else
        {
            foreach (var path in paths)
            {
                await RunGitCommandAsync(projectPath, $"add -- \"{EscapePath(path)}\"");
            }
        }
    }

    /// <inheritdoc />
    public async Task UnstageAsync(string projectPath, IEnumerable<string>? paths = null)
    {
        if (paths == null || !paths.Any())
        {
            await RunGitCommandAsync(projectPath, "reset HEAD");
        }
        else
        {
            foreach (var path in paths)
            {
                await RunGitCommandAsync(projectPath, $"reset HEAD -- \"{EscapePath(path)}\"");
            }
        }
    }

    /// <inheritdoc />
    public async Task<GitCommit> CommitAsync(string projectPath, string message, string? author = null, string? email = null)
    {
        var args = new StringBuilder("commit");
        
        if (!string.IsNullOrEmpty(author) && !string.IsNullOrEmpty(email))
        {
            args.Append($" --author=\"{EscapeQuotes(author)} <{EscapeQuotes(email)}>\"");
        }
        
        args.Append($" -m \"{EscapeQuotes(message)}\"");

        await RunGitCommandAsync(projectPath, args.ToString());

        // Get the commit we just made
        var shaOutput = await RunGitCommandAsync(projectPath, "rev-parse HEAD");
        var sha = shaOutput.Trim();

        return await GetCommitAsync(projectPath, sha);
    }

    /// <inheritdoc />
    public async Task<GitCommit> GetCommitAsync(string projectPath, string sha)
    {
        var format = "%H|%s|%an|%ae|%aI";
        var output = await RunGitCommandAsync(projectPath, $"log -1 --format=\"{format}\" {sha}");
        var parts = output.Trim().Split('|');

        var commit = new GitCommit
        {
            Sha = parts[0],
            Message = parts.Length > 1 ? parts[1] : "",
            Author = parts.Length > 2 ? parts[2] : "",
            Email = parts.Length > 3 ? parts[3] : "",
            Date = parts.Length > 4 ? DateTimeOffset.Parse(parts[4]) : DateTimeOffset.UtcNow
        };

        // Get changed files
        var filesOutput = await RunGitCommandAsync(projectPath, $"diff-tree --no-commit-id --name-status -r {sha}");
        var fileLines = filesOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in fileLines)
        {
            var fileParts = line.Split('\t');
            if (fileParts.Length >= 2)
            {
                commit.Files.Add(new CommitFile
                {
                    Status = GetFileStatus(fileParts[0]),
                    Path = fileParts[1]
                });
            }
        }

        return commit;
    }

    /// <inheritdoc />
    public async Task<List<GitCommit>> GetCommitsAsync(string projectPath, int limit = 20, string? before = null)
    {
        var commits = new List<GitCommit>();
        var format = "%H|%s|%an|%ae|%aI";
        var args = $"log --format=\"{format}\" -n {limit}";

        if (!string.IsNullOrEmpty(before))
        {
            args += $" {before}^";
        }

        var output = await RunGitCommandAsync(projectPath, args);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length >= 5)
            {
                commits.Add(new GitCommit
                {
                    Sha = parts[0],
                    Message = parts[1],
                    Author = parts[2],
                    Email = parts[3],
                    Date = DateTimeOffset.TryParse(parts[4], out var date) ? date : DateTimeOffset.UtcNow
                });
            }
        }

        return commits;
    }

    /// <inheritdoc />
    public async Task RevertFileAsync(string projectPath, string path)
    {
        await RunGitCommandAsync(projectPath, $"checkout -- \"{EscapePath(path)}\"");
    }

    /// <inheritdoc />
    public async Task<FileDiff> GetFileDiffAsync(string projectPath, string path, string type = "unstaged")
    {
        var args = type == "staged" 
            ? $"diff --cached -- \"{EscapePath(path)}\"" 
            : $"diff -- \"{EscapePath(path)}\"";

        var output = await RunGitCommandAsync(projectPath, args);

        var additions = 0;
        var deletions = 0;
        foreach (var line in output.Split('\n'))
        {
            if (line.StartsWith('+') && !line.StartsWith("+++"))
                additions++;
            else if (line.StartsWith('-') && !line.StartsWith("---"))
                deletions++;
        }

        return new FileDiff
        {
            Path = path,
            Diff = output,
            Additions = additions,
            Deletions = deletions
        };
    }

    /// <inheritdoc />
    public async Task<List<GitRemote>> GetRemotesAsync(string projectPath)
    {
        var remotes = new List<GitRemote>();
        var output = await RunGitCommandAsync(projectPath, "remote -v");
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var processed = new HashSet<string>();
        foreach (var line in lines)
        {
            var match = RemotePattern().Match(line);
            if (match.Success)
            {
                var name = match.Groups[1].Value;
                if (!processed.Contains(name))
                {
                    processed.Add(name);
                    remotes.Add(new GitRemote
                    {
                        Name = name,
                        Url = match.Groups[2].Value
                    });
                }
            }
        }

        return remotes;
    }

    /// <inheritdoc />
    public async Task AddRemoteAsync(string projectPath, GitRemote remote)
    {
        await RunGitCommandAsync(projectPath, $"remote add {EscapeBranchName(remote.Name)} \"{EscapeQuotes(remote.Url)}\"");
    }

    /// <inheritdoc />
    public async Task RemoveRemoteAsync(string projectPath, string name)
    {
        await RunGitCommandAsync(projectPath, $"remote remove {EscapeBranchName(name)}");
    }

    /// <inheritdoc />
    public async Task UpdateRemoteAsync(string projectPath, string name, GitRemote remote)
    {
        await RunGitCommandAsync(projectPath, $"remote set-url {EscapeBranchName(name)} \"{EscapeQuotes(remote.Url)}\"");
    }

    /// <inheritdoc />
    public async Task PullAsync(string projectPath, string remote, bool track = false, bool allowUnrelatedHistories = false)
    {
        var args = $"pull {EscapeBranchName(remote)}";
        if (allowUnrelatedHistories)
        {
            args += " --allow-unrelated-histories";
        }
        await RunGitCommandAsync(projectPath, args);
    }

    /// <inheritdoc />
    public async Task PushAsync(string projectPath, string remote, bool track = false)
    {
        var args = $"push {EscapeBranchName(remote)}";
        if (track)
        {
            args += " -u";
        }
        await RunGitCommandAsync(projectPath, args);
    }

    /// <inheritdoc />
    public async Task AbortMergeAsync(string projectPath)
    {
        await RunGitCommandAsync(projectPath, "merge --abort");
    }

    /// <inheritdoc />
    public async Task ResolveMergeAsync(string projectPath, string path, string resolution)
    {
        switch (resolution.ToLowerInvariant())
        {
            case "ours":
                await RunGitCommandAsync(projectPath, $"checkout --ours -- \"{EscapePath(path)}\"");
                break;
            case "theirs":
                await RunGitCommandAsync(projectPath, $"checkout --theirs -- \"{EscapePath(path)}\"");
                break;
            default:
                // Manual resolution - just stage the file
                break;
        }
        await StageAsync(projectPath, new[] { path });
    }

    /// <inheritdoc />
    public async Task<List<string>> GetFilesAsync(string projectPath)
    {
        var output = await RunGitCommandAsync(projectPath, "ls-files");
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    /// <inheritdoc />
    public async Task<string> GetFileContentAsync(string projectPath, string path, string? tree = null)
    {
        if (string.IsNullOrEmpty(tree))
        {
            var fullPath = Path.Combine(projectPath, path);
            return await File.ReadAllTextAsync(fullPath);
        }
        else
        {
            var output = await RunGitCommandAsync(projectPath, $"show {tree}:\"{EscapePath(path)}\"");
            return output;
        }
    }

    private async Task<string> RunGitCommandAsync(string workingDirectory, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _gitExecutable,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Git command failed: {error}");
        }

        return output;
    }

    private static string GetFileStatus(string statusCode)
    {
        return statusCode switch
        {
            "A" => "added",
            "M" => "modified",
            "D" => "deleted",
            "R" => "renamed",
            "C" => "copied",
            _ => "modified"
        };
    }

    private static string EscapePath(string path)
    {
        return path.Replace("\\", "/").Replace("\"", "\\\"");
    }

    private static string EscapeQuotes(string value)
    {
        return value.Replace("\"", "\\\"");
    }

    private static string EscapeBranchName(string name)
    {
        // Only allow safe characters in branch names
        return new string(name.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '/').ToArray());
    }

    [GeneratedRegex(@"^(\S+)\s+(\S+)\s+\((fetch|push)\)$")]
    private static partial Regex RemotePattern();
}
