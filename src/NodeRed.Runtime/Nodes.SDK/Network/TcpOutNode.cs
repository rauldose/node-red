// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Network;

/// <summary>
/// TCP Out node - provides a choice of TCP outputs.
/// </summary>
[NodeType("tcp out", "tcp out",
    Category = NodeCategory.Network,
    Color = "#6baed6",
    Icon = "fa fa-plug",
    Inputs = 1,
    Outputs = 0)]
public class TcpOutNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("beserver", "Type", new[]
            {
                ("reply", "Reply to TCP"),
                ("server", "Listen on"),
                ("client", "Connect to")
            }, defaultValue: "client")
            .AddText("host", "Host", defaultValue: "localhost", showWhen: "beserver=client")
            .AddNumber("port", "Port", defaultValue: 9000, showWhen: "beserver!=reply")
            .AddCheckbox("base64", "Decode Base64 message?", defaultValue: false)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "beserver", "client" },
        { "host", "localhost" },
        { "port", 9000 },
        { "base64", false }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Provides a choice of TCP outputs.")
        .AddInput("msg.payload", "string|Buffer", "Data to send")
        .Details(@"
Can either connect to a remote TCP port, or accept incoming connections.
**msg.payload** can be a Buffer, string or Base64 encoded string.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        Log($"TCP send: {msg.Payload}");
        Status("Sent", StatusFill.Green, SdkStatusShape.Dot);
        done();
        return Task.CompletedTask;
    }
}
