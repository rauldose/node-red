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
    private ClientWebSocket? _client;
    private CancellationTokenSource? _cts;
    private SendDelegate? _send;

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
        .Summary("WebSocket input node.")
        .AddOutput("msg.payload", "string|Buffer", "Data received from WebSocket")
        .AddOutput("msg._session", "object", "Session information")
        .Details(@"
By default, **msg.payload** will contain the data received.
The **msg._session** contains information about the WebSocket session.")
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
                
                _ = Task.Run(async () => await ReceiveLoopAsync(_cts.Token));
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

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];

        while (!ct.IsCancellationRequested && _client?.State == WebSocketState.Open)
        {
            try
            {
                var result = await _client.ReceiveAsync(buffer, ct);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                var data = buffer.AsSpan(0, result.Count).ToArray();
                object payload = result.MessageType == WebSocketMessageType.Text
                    ? Encoding.UTF8.GetString(data)
                    : data;

                var msg = new NodeMessage
                {
                    Payload = payload,
                    Topic = "websocket"
                };

                _send?.Invoke(0, msg);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"WebSocket receive error: {ex.Message}");
                break;
            }
        }

        Status("Disconnected", StatusFill.Red, SdkStatusShape.Ring);
    }

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        _send = send;
        done();
        return Task.CompletedTask;
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
