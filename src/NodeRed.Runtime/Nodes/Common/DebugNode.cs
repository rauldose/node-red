// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Events;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Nodes.Common;

/// <summary>
/// Debug node - displays messages in the debug sidebar.
/// </summary>
public class DebugNode : NodeBase
{
    /// <summary>
    /// Event fired when a debug message is received.
    /// </summary>
    public static event Action<DebugEvent>? OnDebug;

    public override NodeDefinition Definition => new()
    {
        Type = "debug",
        DisplayName = "debug",
        Category = NodeCategory.Common,
        Color = "#87a980",
        Icon = "debug",
        Inputs = 1,
        Outputs = 0,
        Defaults = new Dictionary<string, object?>
        {
            { "name", "" },
            { "active", true },
            { "tosidebar", true },
            { "console", false },
            { "tostatus", false },
            { "complete", "payload" },
            { "targetType", "msg" },
            { "statusVal", "" },
            { "statusType", "auto" }
        },
        HelpText = "Displays selected message properties in the debug sidebar."
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var active = GetConfig("active", true);
        if (!active)
        {
            Done();
            return Task.CompletedTask;
        }

        var complete = GetConfig("complete", "payload");
        var output = GetOutputValue(message, complete);

        // Log to debug
        Log($"[{DisplayName}]: {FormatOutput(output)}", LogLevel.Debug);

        // Raise debug event
        OnDebug?.Invoke(new DebugEvent
        {
            NodeId = Config.Id,
            NodeName = DisplayName,
            Data = output,
            MessageId = message.Id
        });

        // Update status if configured
        if (GetConfig("tostatus", false))
        {
            var statusText = FormatOutput(output);
            if (statusText.Length > 32)
            {
                statusText = statusText[..32] + "...";
            }
            SetStatus(new NodeStatus
            {
                Text = statusText,
                Shape = StatusShape.Dot,
                Color = StatusColor.Grey
            });
        }

        Done();
        return Task.CompletedTask;
    }

    private object? GetOutputValue(NodeMessage message, string complete)
    {
        return complete switch
        {
            "true" or "full" => message,
            "payload" => message.Payload,
            "topic" => message.Topic,
            _ => message.Properties.GetValueOrDefault(complete, $"[Property '{complete}' not found]")
        };
    }

    private static string FormatOutput(object? output)
    {
        if (output == null) return "null";
        if (output is string s) return s;
        if (output is NodeMessage msg)
        {
            return JsonSerializer.Serialize(new
            {
                _msgid = msg.Id,
                payload = msg.Payload,
                topic = msg.Topic
            }, new JsonSerializerOptions { WriteIndented = false });
        }
        if (output is bool or int or long or float or double)
        {
            return output.ToString() ?? "null";
        }
        return JsonSerializer.Serialize(output);
    }
}
