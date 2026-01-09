// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Net.WebSockets;
using System.Text;
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
    private ClientWebSocket? _client;
    private CancellationTokenSource? _cts;

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("mode", "Type", new[]
            {
                ("server", "Listen on"),
                ("client", "Connect to")
            }, defaultValue: "client")
            .AddText("path", "Path", defaultValue: "/ws", showWhen: "mode=server")
            .AddText("url", "URL", placeholder: "ws://localhost:8080", showWhen: "mode=client")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "mode", "client" },
        { "path", "/ws" },
        { "url", "" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("WebSocket output node.")
        .AddInput("msg.payload", "string|Buffer", "Data to send over WebSocket")
        .Details("Sends **msg.payload** to connected WebSocket clients or server.")
        .Build();

    protected override async Task OnInitializeAsync()
    {
        var mode = GetConfig<string>("mode", "client");
        var url = GetConfig<string>("url", "");

        if (mode == "client" && !string.IsNullOrEmpty(url))
        {
            _cts = new CancellationTokenSource();
            
            try
            {
                _client = new ClientWebSocket();
                await _client.ConnectAsync(new Uri(url), _cts.Token);
                Status($"Connected to {url}", StatusFill.Green, SdkStatusShape.Dot);
            }
            catch (Exception ex)
            {
                Status(ex.Message, StatusFill.Red, SdkStatusShape.Ring);
                Error($"WebSocket error: {ex.Message}");
            }
        }
        else if (mode == "server")
        {
            Status("Server mode requires ASP.NET Core integration", StatusFill.Yellow, SdkStatusShape.Ring);
        }
    }

    protected override async Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        if (_client?.State != WebSocketState.Open)
        {
            Error("WebSocket not connected", msg);
            done();
            return;
        }

        try
        {
            byte[] data;
            WebSocketMessageType messageType;

            if (msg.Payload is byte[] bytes)
            {
                data = bytes;
                messageType = WebSocketMessageType.Binary;
            }
            else
            {
                var str = msg.Payload?.ToString() ?? "";
                data = Encoding.UTF8.GetBytes(str);
                messageType = WebSocketMessageType.Text;
            }

            await _client.SendAsync(data, messageType, true, CancellationToken.None);
            Status("Sent", StatusFill.Green, SdkStatusShape.Dot);
        }
        catch (Exception ex)
        {
            Error($"WebSocket send error: {ex.Message}", msg);
            Status(ex.Message, StatusFill.Red, SdkStatusShape.Ring);
        }

        done();
    }

    protected override async Task OnCloseAsync()
    {
        _cts?.Cancel();
        if (_client?.State == WebSocketState.Open)
        {
            await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }
        _client?.Dispose();
    }
}
