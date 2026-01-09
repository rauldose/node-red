// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NodeRed.Runtime.Nodes.Network;

/// <summary>
/// TCP In node - receives TCP data.
/// </summary>
public class TcpInNode : NodeBase, IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly List<TcpClient> _clients = new();

    public override NodeDefinition Definition => new()
    {
        Type = "tcp in",
        Category = NodeCategory.Network,
        DisplayName = "tcp in",
        Color = "#c0c0c0",
        Icon = "fa-exchange",
        Inputs = 0,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "server", "server" }, // server, client
            { "host", "" },
            { "port", 0 },
            { "datamode", "stream" }, // stream, single, count, char
            { "datatype", "buffer" }, // buffer, utf8, base64
            { "newline", "" },
            { "topic", "" },
            { "trim", false }
        }
    };

    public override async Task InitializeAsync(FlowNode config, Core.Interfaces.INodeContext context)
    {
        await base.InitializeAsync(config, context);

        var mode = GetConfig<string>("server", "server");
        var port = GetConfig<int>("port", 0);

        if (mode == "server" && port > 0)
        {
            StartServer(port);
        }
    }

    private void StartServer(int port)
    {
        try
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            SetStatus(NodeStatus.Success($"listening on port {port}"));

            // Start accepting clients
            _ = AcceptClientsAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            Log($"TCP server error: {ex.Message}", LogLevel.Error);
            SetStatus(NodeStatus.Error(ex.Message));
        }
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _clients.Add(client);
                _ = HandleClientAsync(client, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"Accept error: {ex.Message}", LogLevel.Error);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        var datatype = GetConfig<string>("datatype", "utf8");
        var topic = GetConfig<string>("topic", "");
        var buffer = new byte[4096];

        try
        {
            var stream = client.GetStream();
            while (!ct.IsCancellationRequested && client.Connected)
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                if (bytesRead == 0) break;

                var data = new byte[bytesRead];
                Array.Copy(buffer, data, bytesRead);

                object payload = datatype switch
                {
                    "utf8" => Encoding.UTF8.GetString(data),
                    "base64" => Convert.ToBase64String(data),
                    _ => data
                };

                var msg = new NodeMessage
                {
                    Payload = payload,
                    Topic = topic
                };
                msg.Properties["ip"] = ((IPEndPoint?)client.Client.RemoteEndPoint)?.Address.ToString() ?? "";
                msg.Properties["port"] = ((IPEndPoint?)client.Client.RemoteEndPoint)?.Port ?? 0;

                Send(msg);
            }
        }
        catch (Exception ex)
        {
            Log($"Client error: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            _clients.Remove(client);
            client.Close();
        }
    }

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // TCP In nodes don't receive input from other nodes
        return Task.CompletedTask;
    }

    public override Task CloseAsync()
    {
        Dispose();
        return base.CloseAsync();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _listener?.Stop();

        foreach (var client in _clients)
        {
            client.Close();
        }
        _clients.Clear();
    }
}
