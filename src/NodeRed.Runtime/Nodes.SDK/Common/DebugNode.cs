// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Events;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Common;

/// <summary>
/// Debug node - displays messages in the debug sidebar.
/// </summary>
[NodeType("debug", "debug",
    Category = NodeCategory.Common,
    Color = "#87a980",
    Icon = "fa fa-bug",
    Inputs = 1,
    Outputs = 0)]
public class DebugNode : SdkNodeBase
{
    /// <summary>
    /// Event fired when a debug message is received.
    /// </summary>
    public static event Action<DebugEvent>? OnDebug;

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("complete", "Output", new[]
            {
                ("payload", "msg.payload"),
                ("true", "complete msg object"),
                ("topic", "msg.topic")
            }, defaultValue: "payload")
            .AddCheckbox("tosidebar", "Debug to sidebar", defaultValue: true)
            .AddCheckbox("console", "Debug to console", defaultValue: false)
            .AddCheckbox("tostatus", "Debug to status", defaultValue: false)
            .AddCheckbox("active", "Active", defaultValue: true)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "active", true },
        { "tosidebar", true },
        { "console", false },
        { "tostatus", false },
        { "complete", "payload" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Displays selected message properties in the debug sidebar.")
        .AddInput("msg", "object", "The message to debug")
        .Details(@"
The Debug node can be connected to the output of any node. 
It displays the selected property in the debug sidebar.

**Output options:**
- **msg.payload** - Only the payload property
- **complete msg object** - The entire message object
- **msg.topic** - Only the topic property

You can also choose to output to the browser console and/or show as node status.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var active = GetConfig("active", true);
        if (!active)
        {
            done();
            return Task.CompletedTask;
        }

        var complete = GetConfig("complete", "payload");
        var output = GetOutputValue(msg, complete);

        // Raise debug event for sidebar
        if (GetConfig("tosidebar", true))
        {
            OnDebug?.Invoke(new DebugEvent
            {
                NodeId = Config.Id,
                NodeName = Name,
                Data = output,
                MessageId = msg.Id
            });
        }

        // Log to console
        if (GetConfig("console", false))
        {
            Log($"[{Name}]: {FormatOutput(output)}");
        }

        // Update status if configured
        if (GetConfig("tostatus", false))
        {
            var statusText = FormatOutput(output);
            if (statusText.Length > 32)
            {
                statusText = statusText[..32] + "...";
            }
            Status(statusText, StatusFill.Grey, SdkStatusShape.Dot);
        }

        done();
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
