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
/// UDP In node - listens for incoming UDP packets.
/// </summary>
[NodeType("udp in", "udp in",
    Category = NodeCategory.Network,
    Color = "#6baed6",
    Icon = "fa fa-square",
    Inputs = 0,
    Outputs = 1)]
public class UdpInNode : SdkNodeBase
{
    private UdpClient? _client;
    private CancellationTokenSource? _cts;
    private SendDelegate? _send;

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("iface", "Listen", new[]
            {
                ("", "All local IP addresses"),
                ("specific", "Specific interface")
            })
            .AddText("addr", "Address", showWhen: "iface=specific")
            .AddNumber("port", "Port", defaultValue: 9000)
            .AddSelect("multicast", "Multicast", new[]
            {
                ("false", "Disabled"),
                ("multi", "Join multicast group")
            }, defaultValue: "false")
            .AddText("group", "Group", showWhen: "multicast=multi")
            .AddSelect("datatype", "Output", new[]
            {
                ("buffer", "A Buffer"),
                ("utf8", "A String"),
                ("base64", "A Base64 string")
            }, defaultValue: "buffer")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "iface", "" },
        { "addr", "" },
        { "port", 9000 },
        { "multicast", "false" },
        { "group", "" },
        { "datatype", "buffer" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("A UDP input node that produces msg.payload.")
        .AddOutput("msg.payload", "Buffer|string", "UDP packet data")
        .AddOutput("msg.ip", "string", "Sender IP address")
        .AddOutput("msg.port", "number", "Sender port")
        .Details("Listens for incoming UDP packets on the configured port.")
        .Build();

    protected override async Task OnInitializeAsync()
    {
        var port = GetConfig<int>("port", 9000);
        var iface = GetConfig<string>("iface", "");
        var addr = GetConfig<string>("addr", "");
        var multicast = GetConfig<string>("multicast", "false");
        var group = GetConfig<string>("group", "");

        _cts = new CancellationTokenSource();

        try
        {
            if (iface == "specific" && !string.IsNullOrEmpty(addr))
            {
                var localEp = new IPEndPoint(IPAddress.Parse(addr), port);
                _client = new UdpClient(localEp);
            }
            else
            {
                _client = new UdpClient(port);
            }

            if (multicast == "multi" && !string.IsNullOrEmpty(group))
            {
                _client.JoinMulticastGroup(IPAddress.Parse(group));
            }

            Status($"Listening on port {port}", StatusFill.Green, SdkStatusShape.Dot);
            _ = Task.Run(async () => await ReceiveLoopAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            Status(ex.Message, StatusFill.Red, SdkStatusShape.Ring);
            Error($"UDP error: {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var datatype = GetConfig<string>("datatype", "buffer");

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
                    Topic = $"udp:{result.RemoteEndPoint}"
                };

                _send?.Invoke(0, msg);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"UDP receive error: {ex.Message}");
            }
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
        _client?.Dispose();
        return Task.CompletedTask;
    }
}
