// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Common;

/// <summary>
/// Catch node - catches errors from other nodes.
/// </summary>
[NodeType("catch", "catch",
    Category = NodeCategory.Common,
    Color = "#e3a75a",
    Icon = "fa fa-exclamation-triangle",
    Inputs = 0,
    Outputs = 1)]
public class CatchNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("scope", "Scope", new[]
            {
                ("all", "Catch errors from all nodes"),
                ("group", "Catch errors from nodes in same group"),
                ("uncaught", "Catch uncaught errors only")
            }, defaultValue: "all")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "scope", "all" },
        { "uncaught", false }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Catches errors from other nodes in the flow.")
        .AddOutput("msg.error", "object", "The error object with message and source")
        .AddOutput("msg.error.message", "string", "The error message")
        .AddOutput("msg.error.source", "object", "Information about the source node")
        .Details(@"
The Catch node is triggered when an error occurs in another node.
It can be configured to catch:

- **All errors** from any node in the same flow
- **Errors from nodes in the same group**
- **Only uncaught errors** that aren't handled by another Catch node

The error information is available in `msg.error`.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Catch nodes are triggered by the runtime when errors occur
        send(0, msg);
        done();
        return Task.CompletedTask;
    }
}
