// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Storage;

/// <summary>
/// File In node - reads from a file.
/// </summary>
public class FileInNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "file in",
        Category = NodeCategory.Storage,
        DisplayName = "file in",
        Color = "#87A980",
        Icon = "fa-file-import",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "filename", "" },
            { "format", "utf8" }, // utf8, lines, stream, buffer
            { "chunk", false },
            { "sendError", false },
            { "encoding", "utf8" }
        }
    };

    public override async Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var filename = GetConfig<string>("filename", "");
        var format = GetConfig<string>("format", "utf8");
        var sendError = GetConfig<bool>("sendError", false);

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
            if (!File.Exists(filename))
            {
                throw new FileNotFoundException($"File not found: {filename}");
            }

            if (format == "lines")
            {
                // Send each line as a separate message
                var lines = await File.ReadAllLinesAsync(filename);
                foreach (var line in lines)
                {
                    var lineMsg = new NodeMessage
                    {
                        Topic = message.Topic,
                        Payload = line
                    };
                    lineMsg.Properties["filename"] = filename;
                    Send(lineMsg);
                }
            }
            else if (format == "buffer")
            {
                // Read as byte array
                var bytes = await File.ReadAllBytesAsync(filename);
                message.Payload = bytes;
                message.Properties["filename"] = filename;
                Send(message);
            }
            else
            {
                // Read as string (utf8)
                var content = await File.ReadAllTextAsync(filename);
                message.Payload = content;
                message.Properties["filename"] = filename;
                Send(message);
            }

            SetStatus(NodeStatus.Success("ok"));
        }
        catch (Exception ex)
        {
            Log($"File read error: {ex.Message}", Core.Enums.LogLevel.Error);
            SetStatus(NodeStatus.Error(ex.Message));

            if (sendError)
            {
                message.Payload = null;
                message.Properties["error"] = ex.Message;
                Send(message);
            }
        }

        Done();
    }
}
