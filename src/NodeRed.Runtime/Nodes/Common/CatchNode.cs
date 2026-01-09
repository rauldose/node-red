// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Common;

/// <summary>
/// Catch node - catches errors from other nodes.
/// </summary>
public class CatchNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "catch",
        DisplayName = "catch",
        Category = NodeCategory.Common,
        Color = "#e8c191",
        Icon = "catch",
        Inputs = 0,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "name", "" },
            { "scope", null }, // null = all nodes, array = specific nodes
            { "uncaught", false }
        },
        HelpText = "Catches errors thrown by nodes on the same tab."
    };

    /// <summary>
    /// Called when an error occurs in the flow.
    /// </summary>
    public void HandleError(string sourceNodeId, Exception error, NodeMessage? originalMessage)
    {
        var scope = GetConfig<List<string>?>("scope", null);
        
        // Check if we should handle this error
        if (scope != null && !scope.Contains(sourceNodeId))
        {
            return;
        }

        var errorMessage = new NodeMessage
        {
            Payload = originalMessage?.Payload,
            Topic = originalMessage?.Topic
        };

        errorMessage.Properties["error"] = new Dictionary<string, object?>
        {
            { "message", error.Message },
            { "source", new Dictionary<string, object?> { { "id", sourceNodeId } } }
        };

        if (originalMessage != null)
        {
            foreach (var prop in originalMessage.Properties)
            {
                if (!errorMessage.Properties.ContainsKey(prop.Key))
                {
                    errorMessage.Properties[prop.Key] = prop.Value;
                }
            }
        }

        Send(errorMessage);
    }

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // Catch nodes don't receive regular input
        Done();
        return Task.CompletedTask;
    }
}
