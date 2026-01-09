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
/// UDP Out node - sends UDP packets.
/// </summary>
[NodeType("udp out", "udp out",
    Category = NodeCategory.Network,
    Color = "#6baed6",
    Icon = "fa fa-square",
    Inputs = 1,
    Outputs = 0)]
public class UdpOutNode : SdkNodeBase
{
    private UdpClient? _client;

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("addr", "Address", defaultValue: "localhost", placeholder: "destination address")
            .AddNumber("port", "Port", defaultValue: 9000)
            .AddSelect("iface", "Bind", new[]
            {
                ("", "Any address"),
                ("specific", "Specific local address")
            })
            .AddText("outport", "Local port", showWhen: "iface=specific")
            .AddSelect("multicast", "Multicast", new[]
            {
                ("false", "Disabled"),
                ("broad", "Broadcast"),
                ("multi", "Multicast")
            }, defaultValue: "false")
            .AddCheckbox("base64", "Decode Base64 encoded payload?", defaultValue: false)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "addr", "localhost" },
        { "port", 9000 },
        { "iface", "" },
        { "outport", "" },
        { "multicast", "false" },
        { "base64", false }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Sends msg.payload as a UDP message.")
        .AddInput("msg.payload", "Buffer|string", "Data to send")
        .AddInput("msg.ip", "string", "Optional destination IP override")
        .AddInput("msg.port", "number", "Optional destination port override")
        .Details("Sends **msg.payload** to the configured address and port.")
        .Build();

    protected override Task OnInitializeAsync()
    {
        var multicast = GetConfig<string>("multicast", "false");
        
        _client = new UdpClient();
        
        if (multicast == "broad")
        {
            _client.EnableBroadcast = true;
        }

        return Task.CompletedTask;
    }

    protected override async Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var addr = GetConfig<string>("addr", "localhost");
        var port = GetConfig<int>("port", 9000);
        var decodeBase64 = GetConfig<bool>("base64", false);

        try
        {
            byte[] data;
            if (msg.Payload is byte[] bytes)
            {
                data = bytes;
            }
            else if (msg.Payload is string str)
            {
                data = decodeBase64 ? Convert.FromBase64String(str) : Encoding.UTF8.GetBytes(str);
            }
            else
            {
                data = Encoding.UTF8.GetBytes(msg.Payload?.ToString() ?? "");
            }

            if (_client != null)
            {
                IPAddress targetIp;
                if (IPAddress.TryParse(addr, out var ip))
                {
                    targetIp = ip;
                }
                else
                {
                    var addresses = await Dns.GetHostAddressesAsync(addr!);
                    if (addresses.Length == 0)
                    {
                        Error($"Could not resolve host: {addr}", msg);
                        done();
                        return;
                    }
                    targetIp = addresses[0];
                }
                
                var endpoint = new IPEndPoint(targetIp, port);
                await _client.SendAsync(data, endpoint);
                Status("Sent", StatusFill.Green, SdkStatusShape.Dot);
            }
        }
        catch (Exception ex)
        {
            Error($"UDP send error: {ex.Message}", msg);
            Status(ex.Message, StatusFill.Red, SdkStatusShape.Ring);
        }

        done();
    }

    protected override Task OnCloseAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }
}
