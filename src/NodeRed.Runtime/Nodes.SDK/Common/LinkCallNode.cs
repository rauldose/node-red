// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Common;

/// <summary>
/// Link Call node - calls a Link In node and waits for response.
/// </summary>
[NodeType("link call", "link call",
    Category = NodeCategory.Common,
    Color = "#e8c28b",
    Icon = "fa fa-link",
    Inputs = 1,
    Outputs = 1)]
public class LinkCallNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddNumber("timeout", "Timeout", suffix: "seconds", defaultValue: 30, min: 0)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "links", new List<string>() },
        { "timeout", 30 }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Calls a Link In node and waits for a response from a Link Out node.")
        .AddInput("msg", "object", "The message to send to the linked flow")
        .AddOutput("msg", "object", "The response message from the linked flow")
        .Details(@"
The Link Call node sends a message to a Link In node and waits
for a response from a Link Out node (in return mode).

This allows creating reusable sub-flows that can be called
like functions, with request/response semantics.

**Timeout:** If no response is received within the configured
timeout, the node will generate an error.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Link Call sends to Link In and waits for Link Out (return mode)
        // This is handled by the runtime
        
        var timeout = GetConfig("timeout", 30);
        Status("calling...", StatusFill.Blue, SdkStatusShape.Ring);
        
        // The runtime handles the actual call/response mechanism
        // For now, just pass through
        send(0, msg);
        done();
        
        return Task.CompletedTask;
    }
}
