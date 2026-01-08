// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Network;

/// <summary>
/// MQTT In node - subscribes to MQTT topics.
/// </summary>
public class MqttInNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "mqtt in",
        Category = NodeCategory.Network,
        DisplayName = "mqtt in",
        Color = "#d8bfd8",
        Icon = "fa-sign-in",
        Inputs = 0,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "broker", "" },
            { "topic", "" },
            { "qos", 0 },
            { "datatype", "auto" }, // auto, utf8, buffer, json, base64
            { "nl", false },
            { "rap", true },
            { "rh", 0 }
        }
    };

    /// <summary>
    /// Gets the MQTT topic this node is subscribed to.
    /// </summary>
    public string Topic => GetConfig<string>("topic", "");

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // MQTT In nodes don't receive input from other nodes
        // They are triggered by incoming MQTT messages
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when an MQTT message is received on the subscribed topic.
    /// </summary>
    public void HandleMessage(string topic, byte[] payload, bool retain)
    {
        var datatype = GetConfig<string>("datatype", "auto");

        object messagePayload = datatype switch
        {
            "utf8" => System.Text.Encoding.UTF8.GetString(payload),
            "buffer" => payload,
            "base64" => Convert.ToBase64String(payload),
            "json" => System.Text.Json.JsonSerializer.Deserialize<object>(payload) ?? new object(),
            _ => TryParseAuto(payload)
        };

        var msg = new NodeMessage
        {
            Payload = messagePayload,
            Topic = topic
        };
        msg.Properties["retain"] = retain;
        msg.Properties["qos"] = GetConfig<int>("qos", 0);

        Send(msg);
    }

    private static object TryParseAuto(byte[] payload)
    {
        try
        {
            var str = System.Text.Encoding.UTF8.GetString(payload);
            
            // Try to parse as JSON
            if ((str.StartsWith("{") && str.EndsWith("}")) ||
                (str.StartsWith("[") && str.EndsWith("]")))
            {
                return System.Text.Json.JsonSerializer.Deserialize<object>(str) ?? str;
            }

            // Try to parse as number
            if (double.TryParse(str, out var num))
            {
                return num;
            }

            // Return as string
            return str;
        }
        catch
        {
            return payload;
        }
    }
}
