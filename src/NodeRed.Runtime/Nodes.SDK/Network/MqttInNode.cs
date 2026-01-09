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
    private IMqttClient? _client;
    private CancellationTokenSource? _cts;
    private SendDelegate? _send;

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

    protected override async Task OnInitializeAsync()
    {
        var broker = GetConfig<string>("broker", "");
        var topic = GetConfig<string>("topic", "#");
        var qosStr = GetConfig<string>("qos", "0");
        
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

            // Parse broker URL
            var uri = broker!.StartsWith("mqtt://") || broker.StartsWith("mqtts://") 
                ? new Uri(broker) 
                : new Uri($"mqtt://{broker}");

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(uri.Host, uri.Port > 0 ? uri.Port : 1883);

            if (uri.Scheme == "mqtts")
            {
                optionsBuilder.WithTlsOptions(o => { });
            }

            _client.ApplicationMessageReceivedAsync += async e =>
            {
                var datatype = GetConfig<string>("datatype", "auto");
                var payloadBytes = e.ApplicationMessage.PayloadSegment.ToArray();
                object? payload = datatype switch
                {
                    "utf8" => Encoding.UTF8.GetString(payloadBytes),
                    "buffer" => payloadBytes,
                    "base64" => Convert.ToBase64String(payloadBytes),
                    "json" => TryParseJson(payloadBytes),
                    _ => AutoDetect(payloadBytes)
                };

                var msg = new NodeMessage
                {
                    Payload = payload,
                    Topic = e.ApplicationMessage.Topic
                };

                _send?.Invoke(0, msg);
                await Task.CompletedTask;
            };

            await _client.ConnectAsync(optionsBuilder.Build(), _cts.Token);

            var qos = qosStr switch
            {
                "1" => MqttQualityOfServiceLevel.AtLeastOnce,
                "2" => MqttQualityOfServiceLevel.ExactlyOnce,
                _ => MqttQualityOfServiceLevel.AtMostOnce
            };

            await _client.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic(topic!)
                .WithQualityOfServiceLevel(qos)
                .Build(), _cts.Token);

            Status($"Connected to {uri.Host}", StatusFill.Green, SdkStatusShape.Dot);
        }
        catch (Exception ex)
        {
            Status(ex.Message, StatusFill.Red, SdkStatusShape.Ring);
            Log($"MQTT connection error: {ex.Message}");
        }
    }

    private static object? TryParseJson(byte[] data)
    {
        try
        {
            var str = Encoding.UTF8.GetString(data);
            return JsonSerializer.Deserialize<object>(str);
        }
        catch
        {
            return Encoding.UTF8.GetString(data);
        }
    }

    private static object AutoDetect(byte[] data)
    {
        try
        {
            var str = Encoding.UTF8.GetString(data);
            if (str.TrimStart().StartsWith("{") || str.TrimStart().StartsWith("["))
            {
                return JsonSerializer.Deserialize<object>(str) ?? str;
            }
            return str;
        }
        catch
        {
            return data;
        }
    }

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        _send = send;
        done();
        return Task.CompletedTask;
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
