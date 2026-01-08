// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Storage;

/// <summary>
/// Watch node - watches a directory or file for changes.
/// </summary>
[NodeType("watch", "watch",
    Category = NodeCategory.Storage,
    Color = "#dbb84d",
    Icon = "fa fa-eye",
    Inputs = 0,
    Outputs = 1)]
public class WatchNode : SdkNodeBase
{
    private FileSystemWatcher? _watcher;

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("files", "File(s)", icon: "fa fa-folder-open", placeholder: "/path/to/watch")
            .AddCheckbox("recursive", "Watch sub-directories", defaultValue: true)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "files", "" },
        { "recursive", true }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Watches a directory or file for changes.")
        .AddOutput("msg.payload", "string", "Full path of the file that changed")
        .AddOutput("msg.file", "string", "Name of the file that changed")
        .AddOutput("msg.type", "string", "Type of change: file or directory")
        .Details(@"
Watches a file or directory for changes and outputs a message
whenever a file is added, changed or deleted.

The **msg.type** indicates whether the change was to a file or directory.")
        .Build();

    protected override Task OnInitializeAsync()
    {
        var path = GetConfig<string>("files", "");
        if (string.IsNullOrEmpty(path))
            return Task.CompletedTask;

        var recursive = GetConfig("recursive", true);

        try
        {
            string directory;
            string filter;

            if (Directory.Exists(path))
            {
                directory = path;
                filter = "*.*";
            }
            else if (File.Exists(path))
            {
                directory = Path.GetDirectoryName(path) ?? ".";
                filter = Path.GetFileName(path);
            }
            else
            {
                directory = Path.GetDirectoryName(path) ?? ".";
                filter = Path.GetFileName(path);
                if (string.IsNullOrEmpty(filter)) filter = "*.*";
            }

            if (!Directory.Exists(directory))
            {
                Warn($"Directory does not exist: {directory}");
                return Task.CompletedTask;
            }

            _watcher = new FileSystemWatcher(directory, filter)
            {
                IncludeSubdirectories = recursive,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };

            _watcher.Created += OnFileChanged;
            _watcher.Changed += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;
            _watcher.EnableRaisingEvents = true;

            Status($"Watching {directory}", StatusFill.Green, SdkStatusShape.Ring);
        }
        catch (Exception ex)
        {
            Error($"Failed to start watching: {ex.Message}");
            Status("Error", StatusFill.Red, SdkStatusShape.Ring);
        }

        return Task.CompletedTask;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var msg = NewMessage();
        msg.Payload = e.FullPath;
        msg.Properties["file"] = e.Name;
        msg.Properties["type"] = Directory.Exists(e.FullPath) ? "directory" : "file";
        msg.Properties["event"] = e.ChangeType.ToString().ToLowerInvariant();

        // Note: In a real implementation, we'd need to send through the proper channel
        Status($"{e.ChangeType}: {e.Name}", StatusFill.Green, SdkStatusShape.Dot);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        var msg = NewMessage();
        msg.Payload = e.FullPath;
        msg.Properties["file"] = e.Name;
        msg.Properties["oldPath"] = e.OldFullPath;
        msg.Properties["type"] = Directory.Exists(e.FullPath) ? "directory" : "file";
        msg.Properties["event"] = "renamed";

        Status($"Renamed: {e.OldName} → {e.Name}", StatusFill.Green, SdkStatusShape.Dot);
    }

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Watch node doesn't receive input
        done();
        return Task.CompletedTask;
    }

    protected override Task OnCloseAsync()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
        return Task.CompletedTask;
    }
}
