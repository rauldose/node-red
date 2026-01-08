// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using System.Net.Sockets;
using System.Text;

namespace NodeRed.Runtime.Nodes.Network;

/// <summary>
/// TCP Out node - sends TCP data.
/// </summary>
public class TcpOutNode : NodeBase, IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;

    public override NodeDefinition Definition => new()
    {
        Type = "tcp out",
        Category = NodeCategory.Network,
        DisplayName = "tcp out",
        Color = "#c0c0c0",
        Icon = "fa-exchange",
        Inputs = 1,
        Outputs = 0,
        Defaults = new Dictionary<string, object?>
        {
            { "host", "" },
            { "port", 0 },
            { "beserver", "client" }, // client, server, reply
            { "base64", false },
            { "end", false }
        }
    };

    public override async Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var host = GetConfig<string>("host", "");
        var port = GetConfig<int>("port", 0);
        var beserver = GetConfig<string>("beserver", "client");
        var base64 = GetConfig<bool>("base64", false);
        var end = GetConfig<bool>("end", false);

        // Get host/port from message if not configured
        if (string.IsNullOrEmpty(host) && message.Properties.TryGetValue("host", out var msgHost))
        {
            host = msgHost?.ToString() ?? "";
        }
        if (port == 0 && message.Properties.TryGetValue("port", out var msgPort))
        {
            port = Convert.ToInt32(msgPort);
        }

        if (string.IsNullOrEmpty(host) || port == 0)
        {
            Log("No host or port specified", LogLevel.Warning);
            Done();
            return;
        }

        try
        {
            // Connect if not connected
            if (_client == null || !_client.Connected)
            {
                _client = new TcpClient();
                await _client.ConnectAsync(host, port);
                _stream = _client.GetStream();
                SetStatus(NodeStatus.Success($"connected to {host}:{port}"));
            }

            // Get data to send
            byte[] data;
            if (message.Payload is byte[] bytes)
            {
                data = bytes;
            }
            else if (base64 && message.Payload is string b64)
            {
                data = Convert.FromBase64String(b64);
            }
            else
            {
                data = Encoding.UTF8.GetBytes(message.Payload?.ToString() ?? "");
            }

            // Send data
            if (_stream != null)
            {
                await _stream.WriteAsync(data);
            }

            // Close connection if configured
            if (end)
            {
                Dispose();
            }
        }
        catch (Exception ex)
        {
            Log($"TCP send error: {ex.Message}", LogLevel.Error);
            SetStatus(NodeStatus.Error(ex.Message));
            Dispose();
        }

        Done();
    }

    public override Task CloseAsync()
    {
        Dispose();
        return base.CloseAsync();
    }

    public void Dispose()
    {
        _stream?.Close();
        _client?.Close();
        _stream = null;
        _client = null;
    }
}
