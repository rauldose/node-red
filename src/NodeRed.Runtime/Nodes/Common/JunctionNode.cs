// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Common;

/// <summary>
/// Junction node - a simple pass-through node for organizing wire routing.
/// Simply passes any message it receives to its output.
/// </summary>
public class JunctionNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "junction",
        DisplayName = "junction",
        Category = NodeCategory.Common,
        Color = "#999999",
        Icon = "junction",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "name", "" }
        },
        HelpText = "A simple pass-through node for organizing wire routing."
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // Simply pass the message through
        Send(message);
        Done();
        return Task.CompletedTask;
    }
}
