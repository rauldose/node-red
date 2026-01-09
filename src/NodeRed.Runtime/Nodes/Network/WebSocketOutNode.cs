// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Network;

/// <summary>
/// WebSocket Out node - sends WebSocket messages.
/// </summary>
public class WebSocketOutNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "websocket out",
        Category = NodeCategory.Network,
        DisplayName = "websocket out",
        Color = "#ddd",
        Icon = "fa-plug",
        Inputs = 1,
        Outputs = 0,
        Defaults = new Dictionary<string, object?>
        {
            { "server", "" },
            { "client", "" }
        }
    };

    /// <summary>
    /// Event fired when a message needs to be sent via WebSocket.
    /// </summary>
    public static event Action<string, string, object?>? OnSend;

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var server = GetConfig<string>("server", "");

        // Get session ID from message
        string? sessionId = null;
        if (message.Properties.TryGetValue("_session", out var session))
        {
            sessionId = (session as dynamic)?.id?.ToString();
        }

        // Send the message via WebSocket
        OnSend?.Invoke(server, sessionId ?? "", message.Payload);

        Done();
        return Task.CompletedTask;
    }
}
