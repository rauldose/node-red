// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using System.Net;
using System.Net.Sockets;
using System.Text;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Network;

/// <summary>
/// TCP In node - provides a choice of TCP inputs.
/// </summary>
[NodeType("tcp in", "tcp in",
    Category = NodeCategory.Network,
    Color = "#6baed6",
    Icon = "fa fa-plug",
    Inputs = 0,
    Outputs = 1)]
public class TcpInNode : SdkNodeBase
{
    private TcpListener? _listener;
    private TcpClient? _client;
    private CancellationTokenSource? _cts;
    private SendDelegate? _send;

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("server", "Type", new[]
            {
                ("server", "Listen on"),
                ("client", "Connect to")
            }, defaultValue: "server")
            .AddText("host", "Host", defaultValue: "localhost", showWhen: "server=client")
            .AddNumber("port", "Port", defaultValue: 9000)
            .AddSelect("datamode", "Output", new[]
            {
                ("stream", "Stream of data"),
                ("single", "Single message")
            }, defaultValue: "stream")
            .AddSelect("datatype", "as", new[]
            {
                ("buffer", "A Buffer object"),
                ("utf8", "A UTF-8 string"),
                ("base64", "A Base64 encoded string")
            }, defaultValue: "buffer")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "server", "server" },
        { "host", "localhost" },
        { "port", 9000 },
        { "datamode", "stream" },
        { "datatype", "buffer" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Provides a choice of TCP inputs.")
        .AddOutput("msg.payload", "string|Buffer", "Received data")
        .AddOutput("msg._session", "object", "Session information")
        .Details(@"
Can be configured to:
- **Listen** on a port for incoming TCP connections
- **Connect** to a remote TCP server")
        .Build();

    protected override async Task OnInitializeAsync()
    {
        var mode = GetConfig<string>("server", "server");
        var port = GetConfig<int>("port", 9000);
        var host = GetConfig<string>("host", "localhost");

        _cts = new CancellationTokenSource();

        try
        {
            if (mode == "server")
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                Status($"Listening on port {port}", StatusFill.Green, SdkStatusShape.Dot);
                
                _ = Task.Run(async () => await AcceptClientsAsync(_cts.Token));
            }
            else
            {
                _client = new TcpClient();
                await _client.ConnectAsync(host!, port);
                Status($"Connected to {host}:{port}", StatusFill.Green, SdkStatusShape.Dot);
                
                _ = Task.Run(async () => await ReadDataAsync(_client, _cts.Token));
            }
        }
        catch (Exception ex)
        {
            Status(ex.Message, StatusFill.Red, SdkStatusShape.Ring);
            Error($"TCP error: {ex.Message}");
        }
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(async () => await ReadDataAsync(client, ct));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"Accept error: {ex.Message}");
            }
        }
    }

    private async Task ReadDataAsync(TcpClient client, CancellationToken ct)
    {
        var datatype = GetConfig<string>("datatype", "buffer");
        var buffer = new byte[4096];
        var stream = client.GetStream();
        var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

        try
        {
            while (!ct.IsCancellationRequested && client.Connected)
            {
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                if (bytesRead == 0) break;

                var data = buffer.AsSpan(0, bytesRead).ToArray();
                object payload = datatype switch
                {
                    "utf8" => Encoding.UTF8.GetString(data),
                    "base64" => Convert.ToBase64String(data),
                    _ => data
                };

                var msg = new NodeMessage
                {
                    Payload = payload,
                    Topic = $"tcp:{endpoint}"
                };

                _send?.Invoke(0, msg);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log($"Read error: {ex.Message}");
        }
        finally
        {
            client.Dispose();
        }
    }

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        _send = send;
        done();
        return Task.CompletedTask;
    }

    protected override Task OnCloseAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _client?.Dispose();
        return Task.CompletedTask;
    }
}
