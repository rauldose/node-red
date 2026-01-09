// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Common;

/// <summary>
/// Status node - triggered when another node updates its status.
/// </summary>
[NodeType("status", "status",
    Category = NodeCategory.Common,
    Color = "#e3a75a",
    Icon = "fa fa-info-circle",
    Inputs = 0,
    Outputs = 1)]
public class StatusNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("scope", "Scope", new[]
            {
                ("all", "All nodes in flow"),
                ("target", "Selected nodes")
            }, defaultValue: "all")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "scope", "all" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Triggered when another node updates its status.")
        .AddOutput("msg.status", "object", "The status object")
        .AddOutput("msg.status.text", "string", "The status text")
        .AddOutput("msg.status.fill", "string", "The status color (red, green, yellow, blue, grey)")
        .AddOutput("msg.status.shape", "string", "The status shape (ring, dot)")
        .Details(@"
The Status node is triggered whenever a node in the same flow
updates its status display.

This can be used to:
- Monitor node activity
- Track processing state
- Build dashboards of node status")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Status nodes are triggered by the runtime
        send(0, msg);
        done();
        return Task.CompletedTask;
    }
}
