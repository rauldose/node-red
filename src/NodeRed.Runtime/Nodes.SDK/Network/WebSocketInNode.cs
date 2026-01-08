// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Network;

/// <summary>
/// WebSocket In node - listens for WebSocket connections.
/// </summary>
[NodeType("websocket in", "websocket in",
    Category = NodeCategory.Network,
    Color = "#6baed6",
    Icon = "fa fa-exchange",
    Inputs = 0,
    Outputs = 1)]
public class WebSocketInNode : SdkNodeBase
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
        .Summary("WebSocket input node.")
        .AddOutput("msg.payload", "string|Buffer", "Data received from WebSocket")
        .AddOutput("msg._session", "object", "Session information")
        .Details(@"
By default, **msg.payload** will contain the data received.
The **msg._session** contains information about the WebSocket session.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        done();
        return Task.CompletedTask;
    }
}
