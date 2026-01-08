// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Common;

/// <summary>
/// Comment node - adds a comment to the flow.
/// </summary>
[NodeType("comment", "comment",
    Category = NodeCategory.Common,
    Color = "#ffffff",
    Icon = "fa fa-comment",
    Inputs = 0,
    Outputs = 0)]
public class CommentNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddTextArea("info", "Comment", rows: 10, placeholder: "Add your comment here...")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "info", "" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Adds a comment to the flow for documentation purposes.")
        .Details(@"
The Comment node allows you to add documentation notes to your flows.
It has no inputs or outputs and does not affect the flow execution.

Use comments to:
- Document complex flow logic
- Add notes for other developers
- Mark sections of your flow")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Comment nodes don't process messages
        done();
        return Task.CompletedTask;
    }
}
