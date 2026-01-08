// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Network;

/// <summary>
/// MQTT In node - connects to an MQTT broker and subscribes to topics.
/// </summary>
[NodeType("mqtt in", "mqtt in",
    Category = NodeCategory.Network,
    Color = "#d8bfd8",
    Icon = "fa fa-arrow-down",
    Inputs = 0,
    Outputs = 1)]
public class MqttInNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("broker", "Server", icon: "fa fa-server", placeholder: "mqtt://localhost:1883")
            .AddText("topic", "Topic", icon: "fa fa-filter", defaultValue: "#")
            .AddSelect("qos", "QoS", new[]
            {
                ("0", "0 - At most once"),
                ("1", "1 - At least once"),
                ("2", "2 - Exactly once")
            }, defaultValue: "0")
            .AddSelect("datatype", "Output", new[]
            {
                ("auto", "Auto-detect"),
                ("utf8", "A UTF-8 string"),
                ("buffer", "A buffer"),
                ("json", "A parsed JSON object"),
                ("base64", "A Base64 encoded string")
            }, defaultValue: "auto")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "broker", "" },
        { "topic", "#" },
        { "qos", "0" },
        { "datatype", "auto" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Connects to an MQTT broker and subscribes to messages from the specified topic.")
        .AddOutput("msg.payload", "various", "Received message payload")
        .AddOutput("msg.topic", "string", "MQTT topic the message was received on")
        .AddOutput("msg.qos", "number", "QoS level")
        .AddOutput("msg.retain", "boolean", "Whether message was retained")
        .Details(@"
Subscribes to the specified topic on the MQTT broker.

The **Topic** field can use MQTT wildcards:
- **+** matches a single level
- **#** matches multiple levels")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        done();
        return Task.CompletedTask;
    }
}
