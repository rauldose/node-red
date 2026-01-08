// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Storage;

/// <summary>
/// File node - writes msg.payload to a file.
/// </summary>
[NodeType("file", "file",
    Category = NodeCategory.Storage,
    Color = "#dbb84d",
    Icon = "fa fa-file",
    Inputs = 1,
    Outputs = 1)]
public class FileNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("filename", "Filename", icon: "fa fa-file", placeholder: "/path/to/file")
            .AddSelect("action", "Action", new[]
            {
                ("append", "Append to file"),
                ("overwrite", "Overwrite file"),
                ("delete", "Delete file")
            }, defaultValue: "append")
            .AddCheckbox("appendNewline", "Add newline to each payload", defaultValue: true)
            .AddCheckbox("createDir", "Create directory if it doesn't exist", defaultValue: false)
            .AddSelect("encoding", "Encoding", new[]
            {
                ("none", "Default"),
                ("utf8", "UTF-8"),
                ("ascii", "ASCII"),
                ("base64", "Base64")
            }, defaultValue: "none")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "filename", "" },
        { "action", "append" },
        { "appendNewline", true },
        { "createDir", false },
        { "encoding", "none" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Writes msg.payload to a file.")
        .AddInput("msg.payload", "string|Buffer", "Data to write to file")
        .AddInput("msg.filename", "string", "Optional filename override")
        .AddOutput("msg", "object", "Original message passed through")
        .Details(@"
Writes **msg.payload** to a file. The filename can be configured in the node or
provided in **msg.filename**. 

**Actions:**
- **Append**: Add to end of file
- **Overwrite**: Replace file contents
- **Delete**: Remove the file")
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

            var action = GetConfig("action", "append");
            var appendNewline = GetConfig("appendNewline", true);
            var createDir = GetConfig("createDir", false);

            if (createDir)
            {
                var dir = Path.GetDirectoryName(filename);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }

            switch (action)
            {
                case "append":
                    var content = msg.Payload?.ToString() ?? "";
                    if (appendNewline) content += Environment.NewLine;
                    await File.AppendAllTextAsync(filename, content);
                    break;

                case "overwrite":
                    await File.WriteAllTextAsync(filename, msg.Payload?.ToString() ?? "");
                    break;

                case "delete":
                    if (File.Exists(filename))
                        File.Delete(filename);
                    break;
            }

            Status($"Written to {Path.GetFileName(filename)}", StatusFill.Green, SdkStatusShape.Dot);
            send(0, msg);
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
