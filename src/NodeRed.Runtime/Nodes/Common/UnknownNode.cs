// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Common;

/// <summary>
/// Unknown node - a placeholder for nodes that are not installed or recognized.
/// This node is used when a flow references a node type that doesn't exist.
/// It does not process messages.
/// </summary>
public class UnknownNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "unknown",
        DisplayName = "unknown",
        Category = NodeCategory.Common,
        Color = "#c0c0c0",
        Icon = "alert",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "name", "" }
        },
        HelpText = "This node represents an unknown or missing node type."
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // Unknown nodes don't process messages but log a warning
        Log($"Unknown node type - message dropped", LogLevel.Warning);
        Done();
        return Task.CompletedTask;
    }
}
