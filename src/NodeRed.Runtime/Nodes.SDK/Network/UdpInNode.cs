// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
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

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        done();
        return Task.CompletedTask;
    }
}
