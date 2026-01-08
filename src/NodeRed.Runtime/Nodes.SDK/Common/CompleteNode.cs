// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Common;

/// <summary>
/// Complete node - triggered when another node completes processing.
/// </summary>
[NodeType("complete", "complete",
    Category = NodeCategory.Common,
    Color = "#e3a75a",
    Icon = "fa fa-check",
    Inputs = 0,
    Outputs = 1)]
public class CompleteNode : SdkNodeBase
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
        .Summary("Triggered when another node completes processing a message.")
        .AddOutput("msg", "object", "The message that was being processed")
        .Details(@"
The Complete node is triggered when a node calls its `done()` callback
to indicate it has finished processing a message.

This is useful for:
- Tracking message completion through a flow
- Implementing acknowledgment patterns
- Cleanup after processing")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Complete nodes are triggered by the runtime
        send(0, msg);
        done();
        return Task.CompletedTask;
    }
}
