// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Network;

/// <summary>
/// WebSocket In node - receives WebSocket messages.
/// </summary>
public class WebSocketInNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "websocket in",
        Category = NodeCategory.Network,
        DisplayName = "websocket in",
        Color = "#ddd",
        Icon = "fa-plug",
        Inputs = 0,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "server", "" },
            { "client", "" }
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // WebSocket In nodes don't receive input from other nodes
        // They are triggered by incoming WebSocket messages
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a WebSocket message is received.
    /// </summary>
    public void HandleMessage(string data, string sessionId)
    {
        var msg = new NodeMessage
        {
            Payload = data,
            Topic = ""
        };
        msg.Properties["_session"] = new { id = sessionId };

        Send(msg);
    }
}
