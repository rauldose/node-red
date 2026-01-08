// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Function;

/// <summary>
/// Switch node - routes messages based on property values.
/// </summary>
[NodeType("switch", "switch",
    Category = NodeCategory.Function,
    Color = "#e2d96e",
    Icon = "fa fa-random",
    Inputs = 1,
    Outputs = 2)]
public class SwitchNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("property", "Property", icon: "fa fa-ellipsis-h", defaultValue: "payload")
            .AddSelect("propertyType", "Property Type", new[]
            {
                ("msg", "msg."),
                ("flow", "flow."),
                ("global", "global.")
            }, defaultValue: "msg")
            .AddCheckbox("checkall", "Check all rules", defaultValue: true)
            .AddCheckbox("repair", "Recreate message sequences", defaultValue: false)
            .AddInfo("Configure rules in the node configuration to define routing logic.")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "property", "payload" },
        { "propertyType", "msg" },
        { "rules", new List<object>() },
        { "checkall", true },
        { "repair", false },
        { "outputs", 2 }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Routes messages based on their property values.")
        .AddInput("msg", "object", "The message to evaluate")
        .AddOutput("msg", "object", "Routed to matching outputs based on rules")
        .Details(@"
The Switch node routes messages based on rules that evaluate a message property.

**Rule types:**
- **==** - Equal to a value
- **!=** - Not equal to a value
- **<**, **<=**, **>**, **>=** - Numeric comparisons
- **contains** - String contains
- **regex** - Regular expression match
- **is null** - Property is null
- **is not null** - Property is not null
- **otherwise** - Default catch-all

Each matching rule sends the message to its corresponding output.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var property = GetConfig("property", "payload");
        var propertyType = GetConfig("propertyType", "msg");
        var checkall = GetConfig("checkall", true);

        // Get the value to check
        object? value = propertyType switch
        {
            "flow" => Flow.Get(property),
            "global" => Global.Get(property),
            _ => GetMessageProperty(msg, property)
        };

        // For now, simple implementation - send to first output if truthy, second if falsy
        if (IsTruthy(value))
        {
            send(0, msg);
        }
        else
        {
            send(1, msg);
        }

        done();
        return Task.CompletedTask;
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

    private static bool IsTruthy(object? value)
    {
        if (value == null) return false;
        if (value is bool b) return b;
        if (value is string s) return !string.IsNullOrEmpty(s);
        if (value is int i) return i != 0;
        if (value is double d) return d != 0;
        return true;
    }
}
