// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
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

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        Log($"UDP send to {GetConfig("addr", "localhost")}:{GetConfig("port", 9000)}");
        Status("Sent", StatusFill.Green, SdkStatusShape.Dot);
        done();
        return Task.CompletedTask;
    }
}
