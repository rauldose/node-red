// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Storage;

/// <summary>
/// Watch node - monitors a file or directory for changes.
/// </summary>
public class WatchNode : NodeBase, IDisposable
{
    private FileSystemWatcher? _watcher;

    public override NodeDefinition Definition => new()
    {
        Type = "watch",
        Category = NodeCategory.Storage,
        DisplayName = "watch",
        Color = "#87A980",
        Icon = "fa-eye",
        Inputs = 0,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "files", "" },
            { "recursive", true }
        }
    };

    public override Task InitializeAsync(FlowNode config, Core.Interfaces.INodeContext context)
    {
        var result = base.InitializeAsync(config, context);

        var files = GetConfig<string>("files", "");
        var recursive = GetConfig<bool>("recursive", true);

        if (!string.IsNullOrEmpty(files))
        {
            StartWatching(files, recursive);
        }

        return result;
    }

    private void StartWatching(string path, bool recursive)
    {
        try
        {
            string directory;
            string filter;

            if (File.Exists(path))
            {
                directory = Path.GetDirectoryName(path) ?? ".";
                filter = Path.GetFileName(path);
            }
            else if (Directory.Exists(path))
            {
                directory = path;
                filter = "*.*";
            }
            else
            {
                Log($"Path not found: {path}", Core.Enums.LogLevel.Warning);
                return;
            }

            _watcher = new FileSystemWatcher(directory, filter)
            {
                IncludeSubdirectories = recursive,
                NotifyFilter = NotifyFilters.FileName | 
                              NotifyFilters.DirectoryName | 
                              NotifyFilters.LastWrite | 
                              NotifyFilters.Size
            };

            _watcher.Created += OnFileChanged;
            _watcher.Changed += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;

            _watcher.EnableRaisingEvents = true;

            SetStatus(NodeStatus.Success("watching"));
        }
        catch (Exception ex)
        {
            Log($"Watch error: {ex.Message}", Core.Enums.LogLevel.Error);
            SetStatus(NodeStatus.Error(ex.Message));
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var msg = new NodeMessage
        {
            Payload = e.FullPath,
            Topic = e.ChangeType.ToString().ToLower()
        };
        msg.Properties["file"] = e.FullPath;
        msg.Properties["filename"] = e.Name ?? "";
        msg.Properties["type"] = e.ChangeType.ToString().ToLower();

        Send(msg);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        var msg = new NodeMessage
        {
            Payload = new[] { e.OldFullPath, e.FullPath },
            Topic = "rename"
        };
        msg.Properties["file"] = e.FullPath;
        msg.Properties["filename"] = e.Name ?? "";
        msg.Properties["oldFile"] = e.OldFullPath;
        msg.Properties["type"] = "rename";

        Send(msg);
    }

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // Watch node doesn't receive input
        return Task.CompletedTask;
    }

    public override Task CloseAsync()
    {
        Dispose();
        return base.CloseAsync();
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }
}
