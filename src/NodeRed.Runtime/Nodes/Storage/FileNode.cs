// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Storage;

/// <summary>
/// File node - writes to a file.
/// </summary>
public class FileNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "file",
        Category = NodeCategory.Storage,
        DisplayName = "file",
        Color = "#87A980",
        Icon = "fa-file",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "filename", "" },
            { "appendNewline", true },
            { "createDir", false },
            { "overwriteFile", "true" }, // true, false, delete
            { "encoding", "none" }
        }
    };

    public override async Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var filename = GetConfig<string>("filename", "");
        var appendNewline = GetConfig<bool>("appendNewline", true);
        var createDir = GetConfig<bool>("createDir", false);
        var overwriteFile = GetConfig<string>("overwriteFile", "true");

        // Use filename from message if not configured
        if (string.IsNullOrEmpty(filename) && message.Properties.TryGetValue("filename", out var msgFilename))
        {
            filename = msgFilename.ToString() ?? "";
        }

        if (string.IsNullOrEmpty(filename))
        {
            Log("No filename specified", Core.Enums.LogLevel.Warning);
            Done();
            return;
        }

        try
        {
            // Create directory if needed
            if (createDir)
            {
                var dir = Path.GetDirectoryName(filename);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            var content = message.Payload?.ToString() ?? "";
            if (appendNewline && !content.EndsWith('\n'))
            {
                content += Environment.NewLine;
            }

            if (overwriteFile == "delete")
            {
                if (File.Exists(filename))
                {
                    File.Delete(filename);
                }
            }
            else if (overwriteFile == "true")
            {
                await File.WriteAllTextAsync(filename, content);
            }
            else
            {
                await File.AppendAllTextAsync(filename, content);
            }

            SetStatus(NodeStatus.Success("ok"));
            Send(message);
        }
        catch (Exception ex)
        {
            Log($"File write error: {ex.Message}", Core.Enums.LogLevel.Error);
            SetStatus(NodeStatus.Error(ex.Message));
        }

        Done();
    }
}
