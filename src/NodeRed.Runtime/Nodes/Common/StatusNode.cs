// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Common;

/// <summary>
/// Status node - monitors the status of other nodes.
/// </summary>
public class StatusNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "status",
        Category = NodeCategory.Common,
        DisplayName = "status",
        Color = "#c0deed",
        Icon = "fa-info-circle",
        Inputs = 0,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "scope", new List<string>() }
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // Status nodes don't receive normal input
        // They are triggered by the runtime when registered nodes change status
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the runtime when a registered node changes status.
    /// </summary>
    public void TriggerStatusChange(string nodeId, NodeStatus status)
    {
        var scope = GetConfig<List<string>>("scope", new List<string>());
        if (scope.Count == 0 || scope.Contains(nodeId))
        {
            var msg = new NodeMessage
            {
                Payload = new
                {
                    color = status.Color.ToString().ToLower(),
                    shape = status.Shape.ToString().ToLower(),
                    text = status.Text
                },
                Topic = nodeId
            };
            msg.Properties["status"] = status;
            msg.Properties["source"] = new { id = nodeId };
            Send(msg);
        }
    }
}
