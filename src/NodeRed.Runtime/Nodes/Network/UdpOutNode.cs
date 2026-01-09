// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NodeRed.Runtime.Nodes.Network;

/// <summary>
/// UDP Out node - sends UDP data.
/// </summary>
public class UdpOutNode : NodeBase, IDisposable
{
    private UdpClient? _client;

    public override NodeDefinition Definition => new()
    {
        Type = "udp out",
        Category = NodeCategory.Network,
        DisplayName = "udp out",
        Color = "#c0c0c0",
        Icon = "fa-exchange",
        Inputs = 1,
        Outputs = 0,
        Defaults = new Dictionary<string, object?>
        {
            { "addr", "" },
            { "iface", "" },
            { "port", 0 },
            { "ipv", "udp4" },
            { "outport", 0 },
            { "base64", false },
            { "multicast", false }
        }
    };

    public override async Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var addr = GetConfig<string>("addr", "");
        var port = GetConfig<int>("port", 0);
        var base64 = GetConfig<bool>("base64", false);

        // Get host/port from message if not configured
        if (string.IsNullOrEmpty(addr) && message.Properties.TryGetValue("ip", out var msgAddr))
        {
            addr = msgAddr?.ToString() ?? "";
        }
        if (port == 0 && message.Properties.TryGetValue("port", out var msgPort))
        {
            port = Convert.ToInt32(msgPort);
        }

        if (string.IsNullOrEmpty(addr) || port == 0)
        {
            Log("No address or port specified", LogLevel.Warning);
            Done();
            return;
        }

        try
        {
            _client ??= new UdpClient();

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
            var endpoint = new IPEndPoint(IPAddress.Parse(addr), port);
            await _client.SendAsync(data, data.Length, endpoint);

            SetStatus(NodeStatus.Success($"sent to {addr}:{port}"));
        }
        catch (Exception ex)
        {
            Log($"UDP send error: {ex.Message}", LogLevel.Error);
            SetStatus(NodeStatus.Error(ex.Message));
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
        _client?.Close();
        _client = null;
    }
}
