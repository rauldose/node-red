// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Network;

/// <summary>
/// HTTP Response node - sends an HTTP response.
/// </summary>
public class HttpResponseNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "http response",
        Category = NodeCategory.Network,
        DisplayName = "http response",
        Color = "#6baed6",
        Icon = "fa-globe",
        Inputs = 1,
        Outputs = 0,
        Defaults = new Dictionary<string, object?>
        {
            { "statusCode", 200 },
            { "headers", new Dictionary<string, string>() }
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var statusCode = GetConfig<int>("statusCode", 200);

        // Get response object from message
        if (message.Properties.TryGetValue("res", out var resObj) && resObj != null)
        {
            // In a real implementation, we would write the response
            // For now, just mark as done
            Log($"HTTP Response: {statusCode} - {message.Payload}", LogLevel.Debug);
        }
        else
        {
            Log("No response object found in message", LogLevel.Warning);
        }

        Done();
        return Task.CompletedTask;
    }
}
