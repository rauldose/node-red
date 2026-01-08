// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Common;

/// <summary>
/// Complete node - triggers when a node completes processing.
/// </summary>
public class CompleteNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "complete",
        Category = NodeCategory.Common,
        DisplayName = "complete",
        Color = "#c0deed",
        Icon = "fa-check-circle",
        Inputs = 0,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "scope", new List<string>() }
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // Complete nodes don't receive normal input
        // They are triggered by the runtime when registered nodes complete
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the runtime when a registered node completes.
    /// </summary>
    public void TriggerComplete(string nodeId, NodeMessage message)
    {
        var scope = GetConfig<List<string>>("scope", new List<string>());
        if (scope.Count == 0 || scope.Contains(nodeId))
        {
            Send(message);
        }
    }
}
