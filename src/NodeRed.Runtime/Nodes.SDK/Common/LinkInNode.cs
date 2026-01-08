// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Common;

/// <summary>
/// Link In node - receives messages from Link Out nodes.
/// </summary>
[NodeType("link in", "link in",
    Category = NodeCategory.Common,
    Color = "#e8c28b",
    Icon = "fa fa-link",
    Inputs = 0,
    Outputs = 1)]
public class LinkInNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag", required: true)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "links", new List<string>() }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Receives messages from Link Out nodes, creating virtual wires.")
        .AddOutput("msg", "object", "The message received from the linked Link Out node")
        .Details(@"
The Link In node receives messages from any Link Out node
that is configured to send to it.

This allows creating virtual wires that:
- Connect flows across different tabs
- Reduce visual clutter in complex flows
- Create reusable flow patterns

Give this node a descriptive name so it's easy to identify
in the Link Out node's target list.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Link In receives messages from Link Out nodes
        send(0, msg);
        done();
        return Task.CompletedTask;
    }
}
