// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Parser;

/// <summary>
/// JSON node - converts between JSON string and object.
/// </summary>
[NodeType("json", "json",
    Category = NodeCategory.Parser,
    Color = "#8bbce8",
    Icon = "fa fa-file-code-o",
    Inputs = 1,
    Outputs = 1)]
public class JsonNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("action", "Action", new[]
            {
                ("auto", "Convert to/from JSON"),
                ("str", "Always convert to JSON string"),
                ("obj", "Always convert to Object")
            }, defaultValue: "auto")
            .AddText("property", "Property", defaultValue: "payload")
            .AddCheckbox("indent", "Format JSON (pretty print)", defaultValue: false)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "action", "auto" },
        { "property", "payload" },
        { "indent", false }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Converts between a JSON string and its JavaScript object representation.")
        .AddInput("msg.payload", "string|object", "The value to convert")
        .AddOutput("msg.payload", "object|string", "The converted value")
        .Details(@"
The JSON node converts between JSON strings and objects.

**Auto mode:**
- If input is a string, parse it to an object
- If input is an object, stringify it

**Force modes:**
- Always to JSON string
- Always to Object

Format option adds indentation for readable output.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var action = GetConfig("action", "auto");
        var property = GetConfig("property", "payload");
        var indent = GetConfig("indent", false);

        try
        {
            var value = property == "payload" 
                ? msg.Payload 
                : msg.Properties.GetValueOrDefault(property);

            object? result = action switch
            {
                "str" => ToJsonString(value, indent),
                "obj" => ToObject(value),
                _ => value is string s ? ToObject(s) : ToJsonString(value, indent)
            };

            if (property == "payload")
                msg.Payload = result;
            else
                msg.Properties[property] = result;

            send(0, msg);
        }
        catch (Exception ex)
        {
            Error($"JSON error: {ex.Message}", msg);
        }

        done();
        return Task.CompletedTask;
    }

    private static string ToJsonString(object? value, bool indent)
    {
        var options = new JsonSerializerOptions { WriteIndented = indent };
        return JsonSerializer.Serialize(value, options);
    }

    private static object? ToObject(object? value)
    {
        if (value is not string s) return value;
        return JsonSerializer.Deserialize<object>(s);
    }
}
