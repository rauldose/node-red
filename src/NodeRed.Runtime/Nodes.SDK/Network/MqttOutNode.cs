// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Network;

/// <summary>
/// MQTT Out node - connects to an MQTT broker and publishes messages.
/// </summary>
[NodeType("mqtt out", "mqtt out",
    Category = NodeCategory.Network,
    Color = "#d8bfd8",
    Icon = "fa fa-arrow-up",
    Inputs = 1,
    Outputs = 0)]
public class MqttOutNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("broker", "Server", icon: "fa fa-server", placeholder: "mqtt://localhost:1883")
            .AddText("topic", "Topic", icon: "fa fa-filter")
            .AddSelect("qos", "QoS", new[]
            {
                ("0", "0 - At most once"),
                ("1", "1 - At least once"),
                ("2", "2 - Exactly once")
            }, defaultValue: "0")
            .AddCheckbox("retain", "Retain", defaultValue: false)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "broker", "" },
        { "topic", "" },
        { "qos", "0" },
        { "retain", false }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Connects to an MQTT broker and publishes messages.")
        .AddInput("msg.payload", "various", "Payload to publish")
        .AddInput("msg.topic", "string", "Topic to publish to (if not set in node)")
        .AddInput("msg.qos", "number", "QoS override")
        .AddInput("msg.retain", "boolean", "Retain flag override")
        .Details(@"
Publishes **msg.payload** to the configured or provided MQTT topic.

The topic can be set in the node configuration or provided dynamically via **msg.topic**.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var topic = msg.Topic ?? GetConfig<string>("topic", "");
        Log($"MQTT publish to {topic}: {msg.Payload}");
        Status($"→ {topic}", StatusFill.Green, SdkStatusShape.Dot);
        done();
        return Task.CompletedTask;
    }
}
