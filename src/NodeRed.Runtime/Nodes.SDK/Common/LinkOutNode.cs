// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Common;

/// <summary>
/// Link Out node - sends messages to Link In nodes.
/// </summary>
[NodeType("link out", "link out",
    Category = NodeCategory.Common,
    Color = "#e8c28b",
    Icon = "fa fa-link",
    Inputs = 1,
    Outputs = 0)]
public class LinkOutNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("mode", "Mode", new[]
            {
                ("link", "Send to all connected link nodes"),
                ("return", "Return to calling link node")
            }, defaultValue: "link")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "mode", "link" },
        { "links", new List<string>() }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Sends messages to Link In nodes, creating virtual wires.")
        .AddInput("msg", "object", "The message to send to connected Link In nodes")
        .Details(@"
The Link Out node sends messages to one or more Link In nodes,
creating virtual wires between them.

**Modes:**
- **Send to all** - Messages are sent to all connected Link In nodes
- **Return** - Messages are returned to the Link Call node that invoked this flow

This allows creating virtual wires that:
- Connect flows across different tabs
- Reduce visual clutter in complex flows
- Create reusable sub-flows")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Link Out sends to configured Link In nodes
        // This is handled by the runtime based on the links configuration
        var mode = GetConfig("mode", "link");
        
        if (mode == "return")
        {
            // Return mode - send back to caller
            // The runtime handles the actual routing
        }
        else
        {
            // Normal link mode - send to all connected Link In nodes
            // The runtime handles the actual routing based on the links property
        }
        
        done();
        return Task.CompletedTask;
    }
}
