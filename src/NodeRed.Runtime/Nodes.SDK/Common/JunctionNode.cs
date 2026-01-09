// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Common;

/// <summary>
/// Junction node - a simple pass-through node for organizing wire routing.
/// Simply passes any message it receives to its output.
/// </summary>
[NodeType("junction", "junction",
    Category = NodeCategory.Common,
    Color = "#999999",
    Icon = "fa fa-circle-o",
    Inputs = 1,
    Outputs = 1)]
public class JunctionNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("A simple pass-through node for organizing wire routing.")
        .AddInput("msg", "object", "Any message")
        .AddOutput("msg", "object", "The same message, unchanged")
        .Details(@"
The Junction node is a simple pass-through node that can be used
to organize wire routing in your flows.

It takes any message on its input and immediately sends it to its output,
unchanged. This is useful for:

- Creating cleaner wire routing in complex flows
- Providing a common junction point for multiple wires
- Organizing flow layouts for better readability")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Simply pass the message through
        send(0, msg);
        done();
        return Task.CompletedTask;
    }
}
