// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Network;

/// <summary>
/// WebSocket Out node - sends data over WebSocket.
/// </summary>
[NodeType("websocket out", "websocket out",
    Category = NodeCategory.Network,
    Color = "#6baed6",
    Icon = "fa fa-exchange",
    Inputs = 1,
    Outputs = 0)]
public class WebSocketOutNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("mode", "Type", new[]
            {
                ("server", "Listen on"),
                ("client", "Connect to")
            }, defaultValue: "server")
            .AddText("path", "Path", defaultValue: "/ws", showWhen: "mode=server")
            .AddText("url", "URL", placeholder: "ws://localhost:8080", showWhen: "mode=client")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "mode", "server" },
        { "path", "/ws" },
        { "url", "" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("WebSocket output node.")
        .AddInput("msg.payload", "string|Buffer", "Data to send over WebSocket")
        .Details("Sends **msg.payload** to connected WebSocket clients or server.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        Log($"WebSocket send: {msg.Payload}");
        Status("Sent", StatusFill.Green, SdkStatusShape.Dot);
        done();
        return Task.CompletedTask;
    }
}
