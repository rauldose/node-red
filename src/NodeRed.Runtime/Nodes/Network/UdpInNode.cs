// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NodeRed.Runtime.Nodes.Network;

/// <summary>
/// UDP In node - receives UDP data.
/// </summary>
public class UdpInNode : NodeBase, IDisposable
{
    private UdpClient? _client;
    private CancellationTokenSource? _cts;

    public override NodeDefinition Definition => new()
    {
        Type = "udp in",
        Category = NodeCategory.Network,
        DisplayName = "udp in",
        Color = "#c0c0c0",
        Icon = "fa-exchange",
        Inputs = 0,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "group", "" },
            { "iface", "" },
            { "port", 0 },
            { "ipv", "udp4" },
            { "multicast", false },
            { "datatype", "buffer" } // buffer, utf8, base64
        }
    };

    public override async Task InitializeAsync(FlowNode config, Core.Interfaces.INodeContext context)
    {
        await base.InitializeAsync(config, context);

        var port = GetConfig<int>("port", 0);

        if (port > 0)
        {
            StartListening(port);
        }
    }

    private void StartListening(int port)
    {
        try
        {
            _cts = new CancellationTokenSource();
            _client = new UdpClient(port);

            SetStatus(NodeStatus.Success($"listening on port {port}"));

            // Start receiving
            _ = ReceiveAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            Log($"UDP error: {ex.Message}", LogLevel.Error);
            SetStatus(NodeStatus.Error(ex.Message));
        }
    }

    private async Task ReceiveAsync(CancellationToken ct)
    {
        var datatype = GetConfig<string>("datatype", "utf8");

        while (!ct.IsCancellationRequested && _client != null)
        {
            try
            {
                var result = await _client.ReceiveAsync(ct);
                var data = result.Buffer;

                object payload = datatype switch
                {
                    "utf8" => Encoding.UTF8.GetString(data),
                    "base64" => Convert.ToBase64String(data),
                    _ => data
                };

                var msg = new NodeMessage
                {
                    Payload = payload,
                    Topic = ""
                };
                msg.Properties["ip"] = result.RemoteEndPoint.Address.ToString();
                msg.Properties["port"] = result.RemoteEndPoint.Port;

                Send(msg);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"UDP receive error: {ex.Message}", LogLevel.Error);
            }
        }
    }

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // UDP In nodes don't receive input from other nodes
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
        _client?.Close();
        _client = null;
    }
}
