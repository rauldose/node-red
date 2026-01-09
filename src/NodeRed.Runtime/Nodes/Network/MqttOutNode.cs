// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Network;

/// <summary>
/// MQTT Out node - publishes MQTT messages.
/// </summary>
public class MqttOutNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "mqtt out",
        Category = NodeCategory.Network,
        DisplayName = "mqtt out",
        Color = "#d8bfd8",
        Icon = "fa-sign-out",
        Inputs = 1,
        Outputs = 0,
        Defaults = new Dictionary<string, object?>
        {
            { "broker", "" },
            { "topic", "" },
            { "qos", 0 },
            { "retain", false }
        }
    };

    /// <summary>
    /// Event fired when a message needs to be published via MQTT.
    /// </summary>
    public static event Action<string, string, byte[], int, bool>? OnPublish;

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var broker = GetConfig<string>("broker", "");
        var topic = GetConfig<string>("topic", "");
        var qos = GetConfig<int>("qos", 0);
        var retain = GetConfig<bool>("retain", false);

        // Use topic from message if not configured
        if (string.IsNullOrEmpty(topic))
        {
            topic = message.Topic;
        }

        // Get QoS and retain from message if provided
        if (message.Properties.TryGetValue("qos", out var msgQos))
        {
            qos = Convert.ToInt32(msgQos);
        }
        if (message.Properties.TryGetValue("retain", out var msgRetain))
        {
            retain = Convert.ToBoolean(msgRetain);
        }

        // Convert payload to bytes
        byte[] payload;
        if (message.Payload is byte[] bytes)
        {
            payload = bytes;
        }
        else if (message.Payload is string str)
        {
            payload = System.Text.Encoding.UTF8.GetBytes(str);
        }
        else
        {
            payload = System.Text.Encoding.UTF8.GetBytes(
                System.Text.Json.JsonSerializer.Serialize(message.Payload));
        }

        // Publish the message
        OnPublish?.Invoke(broker, topic, payload, qos, retain);

        Done();
        return Task.CompletedTask;
    }
}
