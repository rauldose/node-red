// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
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
    private IMqttClient? _client;
    private CancellationTokenSource? _cts;

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("broker", "Broker", icon: "fa fa-server", placeholder: "mqtt://localhost:1883")
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

    protected override async Task OnInitializeAsync()
    {
        var broker = GetConfig<string>("broker", "");
        
        if (string.IsNullOrEmpty(broker))
        {
            Status("No broker configured", StatusFill.Red, SdkStatusShape.Ring);
            return;
        }

        try
        {
            _cts = new CancellationTokenSource();
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            var uri = broker!.StartsWith("mqtt://") || broker.StartsWith("mqtts://") 
                ? new Uri(broker) 
                : new Uri($"mqtt://{broker}");

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(uri.Host, uri.Port > 0 ? uri.Port : 1883);

            if (uri.Scheme == "mqtts")
            {
                optionsBuilder.WithTlsOptions(o => { });
            }

            await _client.ConnectAsync(optionsBuilder.Build(), _cts.Token);
            Status($"Connected to {uri.Host}", StatusFill.Green, SdkStatusShape.Dot);
        }
        catch (Exception ex)
        {
            Status(ex.Message, StatusFill.Red, SdkStatusShape.Ring);
            Log($"MQTT connection error: {ex.Message}");
        }
    }

    protected override async Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        if (_client?.IsConnected != true)
        {
            Error("Not connected to MQTT broker", msg);
            done();
            return;
        }

        try
        {
            // Topic: use msg.topic if available, otherwise node config
            var topic = msg.Topic ?? GetConfig<string>("topic", "");
            
            // QoS: node config takes precedence, then msg.qos, then default 0
            var nodeQos = GetConfig<string>("qos", "");
            var qosStr = !string.IsNullOrEmpty(nodeQos) ? nodeQos : "0";
            
            // Check if msg has qos override
            if (string.IsNullOrEmpty(nodeQos) && msg.Properties.TryGetValue("qos", out var msgQos))
            {
                qosStr = msgQos?.ToString() ?? "0";
            }
            
            // Retain: node config OR msg.retain
            var retain = GetConfig<bool>("retain", false);
            if (msg.Properties.TryGetValue("retain", out var msgRetain))
            {
                if (msgRetain is bool r) retain = r;
                else if (msgRetain is string rs) retain = rs.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            if (string.IsNullOrEmpty(topic))
            {
                Error("No topic specified", msg);
                done();
                return;
            }

            var qos = qosStr switch
            {
                "1" => MqttQualityOfServiceLevel.AtLeastOnce,
                "2" => MqttQualityOfServiceLevel.ExactlyOnce,
                _ => MqttQualityOfServiceLevel.AtMostOnce
            };

            byte[] payloadBytes;
            if (msg.Payload is byte[] bytes)
            {
                payloadBytes = bytes;
            }
            else if (msg.Payload is string str)
            {
                payloadBytes = Encoding.UTF8.GetBytes(str);
            }
            else
            {
                payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg.Payload));
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payloadBytes)
                .WithQualityOfServiceLevel(qos)
                .WithRetainFlag(retain)
                .Build();

            await _client.PublishAsync(message);
            Status($"→ {topic}", StatusFill.Green, SdkStatusShape.Dot);
        }
        catch (Exception ex)
        {
            Error($"Publish failed: {ex.Message}", msg);
        }

        done();
    }

    protected override async Task OnCloseAsync()
    {
        _cts?.Cancel();
        if (_client?.IsConnected == true)
        {
            await _client.DisconnectAsync();
        }
        _client?.Dispose();
    }
}
