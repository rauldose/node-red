// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Function;

/// <summary>
/// Change node - sets, changes, moves, or deletes properties of a message.
/// </summary>
[NodeType("change", "change",
    Category = NodeCategory.Function,
    Color = "#fdd0a2",
    Icon = "fa fa-edit",
    Inputs = 1,
    Outputs = 1)]
public class ChangeNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("action", "Action", new[]
            {
                ("set", "Set"),
                ("change", "Change"),
                ("move", "Move"),
                ("delete", "Delete")
            }, defaultValue: "set")
            .AddText("property", "Property", icon: "fa fa-ellipsis-h", defaultValue: "payload")
            .AddSelect("propertyType", "on", new[]
            {
                ("msg", "msg."),
                ("flow", "flow."),
                ("global", "global.")
            }, defaultValue: "msg")
            .AddText("value", "to", placeholder: "value", hideWhen: "action=delete")
            .AddSelect("valueType", "Type", new[]
            {
                ("str", "String"),
                ("num", "Number"),
                ("bool", "Boolean"),
                ("json", "JSON"),
                ("date", "Timestamp"),
                ("msg", "msg."),
                ("flow", "flow."),
                ("global", "global.")
            }, defaultValue: "str", hideWhen: "action=delete")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "rules", new List<object>() }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Sets, changes, moves, or deletes message properties.")
        .AddInput("msg", "object", "The message to modify")
        .AddOutput("msg", "object", "The modified message")
        .Details(@"
The Change node modifies message properties using rules.

**Actions:**
- **Set** - Set a property to a value
- **Change** - Search and replace in a string property
- **Move** - Move a property to a new location
- **Delete** - Remove a property

**Value types:**
- String, Number, Boolean, JSON, Timestamp
- msg., flow., global. - Copy from another location

Multiple rules can be applied in sequence.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var action = GetConfig("action", "set");
        var property = GetConfig("property", "payload");
        var propertyType = GetConfig("propertyType", "msg");
        var value = GetConfig<object?>("value", null);
        var valueType = GetConfig("valueType", "str");

        // Convert value based on type
        var resolvedValue = ResolveValue(value, valueType, msg);

        // Apply action
        switch (action)
        {
            case "set":
                SetProperty(msg, property, propertyType, resolvedValue);
                break;
            case "delete":
                DeleteProperty(msg, property, propertyType);
                break;
            case "move":
                // Move is set + delete of original
                break;
            case "change":
                // String replacement
                break;
        }

        send(0, msg);
        done();
        return Task.CompletedTask;
    }

    private object? ResolveValue(object? value, string valueType, NodeMessage msg)
    {
        return valueType switch
        {
            "num" when double.TryParse(value?.ToString(), out var num) => num,
            "bool" => value?.ToString()?.ToLowerInvariant() == "true",
            "date" => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            "msg" => GetMessageProperty(msg, value?.ToString() ?? ""),
            "flow" => Flow.Get(value?.ToString() ?? ""),
            "global" => Global.Get(value?.ToString() ?? ""),
            _ => value?.ToString()
        };
    }

    private void SetProperty(NodeMessage msg, string property, string propertyType, object? value)
    {
        switch (propertyType)
        {
            case "msg":
                SetMessageProperty(msg, property, value);
                break;
            case "flow":
                Flow.Set(property, value);
                break;
            case "global":
                Global.Set(property, value);
                break;
        }
    }

    private void DeleteProperty(NodeMessage msg, string property, string propertyType)
    {
        switch (propertyType)
        {
            case "msg":
                if (property != "payload" && property != "topic")
                {
                    msg.Properties.Remove(property);
                }
                break;
            case "flow":
                Flow.Set(property, null);
                break;
            case "global":
                Global.Set(property, null);
                break;
        }
    }

    private object? GetMessageProperty(NodeMessage msg, string property)
    {
        return property switch
        {
            "payload" => msg.Payload,
            "topic" => msg.Topic,
            _ => msg.Properties.GetValueOrDefault(property)
        };
    }

    private void SetMessageProperty(NodeMessage msg, string property, object? value)
    {
        switch (property)
        {
            case "payload":
                msg.Payload = value;
                break;
            case "topic":
                msg.Topic = value?.ToString() ?? "";
                break;
            default:
                msg.Properties[property] = value;
                break;
        }
    }
}
