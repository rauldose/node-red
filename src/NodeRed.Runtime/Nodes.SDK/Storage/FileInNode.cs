// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Storage;

/// <summary>
/// File In node - reads the contents of a file.
/// </summary>
[NodeType("file in", "file in",
    Category = NodeCategory.Storage,
    Color = "#dbb84d",
    Icon = "fa fa-file-o",
    Inputs = 1,
    Outputs = 1)]
public class FileInNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("filename", "Filename", icon: "fa fa-file", placeholder: "/path/to/file")
            .AddSelect("format", "Output", new[]
            {
                ("utf8", "A utf8 string"),
                ("lines", "A msg per line"),
                ("buffer", "A single Buffer object"),
                ("stream", "A stream of Buffers")
            }, defaultValue: "utf8")
            .AddSelect("encoding", "Encoding", new[]
            {
                ("none", "Default"),
                ("utf8", "UTF-8"),
                ("ascii", "ASCII"),
                ("base64", "Base64")
            }, defaultValue: "none", showWhen: "format=utf8")
            .AddCheckbox("allProps", "Send message for all properties", defaultValue: false)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "filename", "" },
        { "format", "utf8" },
        { "encoding", "none" },
        { "allProps", false }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Reads the contents of a file.")
        .AddInput("msg.filename", "string", "Optional filename override")
        .AddOutput("msg.payload", "string|Buffer", "File contents")
        .AddOutput("msg.filename", "string", "The filename that was read")
        .Details(@"
Reads the contents of a file and outputs as **msg.payload**.

**Output formats:**
- **UTF-8 string**: Entire file as a string
- **A msg per line**: One message for each line
- **Buffer object**: Raw bytes
- **Stream of Buffers**: Multiple buffer chunks")
        .Build();

    protected override async Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var filename = msg.Properties.TryGetValue("filename", out var fn)
                ? fn?.ToString()
                : GetConfig<string>("filename", "");

            if (string.IsNullOrEmpty(filename))
            {
                Error("No filename specified");
                done(new Exception("No filename specified"));
                return;
            }

            if (!File.Exists(filename))
            {
                Error($"File not found: {filename}");
                done(new Exception($"File not found: {filename}"));
                return;
            }

            var format = GetConfig("format", "utf8");

            switch (format)
            {
                case "utf8":
                    msg.Payload = await File.ReadAllTextAsync(filename);
                    msg.Properties["filename"] = filename;
                    send(0, msg);
                    break;

                case "lines":
                    var lines = await File.ReadAllLinesAsync(filename);
                    foreach (var line in lines)
                    {
                        var lineMsg = CloneMessage(msg);
                        lineMsg.Payload = line;
                        lineMsg.Properties["filename"] = filename;
                        send(0, lineMsg);
                    }
                    break;

                case "buffer":
                    msg.Payload = await File.ReadAllBytesAsync(filename);
                    msg.Properties["filename"] = filename;
                    send(0, msg);
                    break;

                default:
                    msg.Payload = await File.ReadAllTextAsync(filename);
                    msg.Properties["filename"] = filename;
                    send(0, msg);
                    break;
            }

            Status($"Read {Path.GetFileName(filename)}", StatusFill.Green, SdkStatusShape.Dot);
            done();
        }
        catch (Exception ex)
        {
            Error(ex.Message);
            Status("Error", StatusFill.Red, SdkStatusShape.Ring);
            done(ex);
        }
    }
}
