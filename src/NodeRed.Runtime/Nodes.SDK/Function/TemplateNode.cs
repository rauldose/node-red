// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Text.RegularExpressions;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Function;

/// <summary>
/// Template node - generates text using a template.
/// </summary>
[NodeType("template", "template",
    Category = NodeCategory.Function,
    Color = "#e6e0f8",
    Icon = "fa fa-file-text-o",
    Inputs = 1,
    Outputs = 1)]
public class TemplateNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("syntax", "Syntax", new[]
            {
                ("mustache", "Mustache"),
                ("plain", "Plain text")
            }, defaultValue: "mustache")
            .AddCode("template", "Template", defaultValue: "This is the payload: {{payload}}", rows: 10)
            .AddText("field", "Output to", icon: "fa fa-ellipsis-h", defaultValue: "payload")
            .AddSelect("fieldType", "Output Type", new[]
            {
                ("msg", "msg."),
                ("flow", "flow."),
                ("global", "global.")
            }, defaultValue: "msg")
            .AddSelect("format", "Format", new[]
            {
                ("text", "Plain text"),
                ("json", "Parsed JSON")
            }, defaultValue: "text")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "syntax", "mustache" },
        { "template", "This is the payload: {{payload}}" },
        { "field", "payload" },
        { "fieldType", "msg" },
        { "format", "text" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Generates text based on a template.")
        .AddInput("msg", "object", "The message providing template values")
        .AddOutput("msg", "object", "The message with the generated text")
        .Details(@"
The Template node uses Mustache templating to generate text.

**Mustache syntax:**
- `{{property}}` - Insert the value of msg.property
- `{{payload}}` - Insert msg.payload
- `{{topic}}` - Insert msg.topic
- `{{flow.name}}` - Insert from flow context
- `{{global.name}}` - Insert from global context

**Example:**
```
Hello {{name}}, 
Your order #{{payload.orderId}} is {{status}}.
```")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var syntax = GetConfig("syntax", "mustache");
        var template = GetConfig("template", "");
        var field = GetConfig("field", "payload");
        var fieldType = GetConfig("fieldType", "msg");
        var format = GetConfig("format", "text");

        // Process template
        string output;
        if (syntax == "mustache")
        {
            output = ProcessMustacheTemplate(template, msg);
        }
        else
        {
            output = template;
        }

        // Set output
        object outputValue;
        if (format == "json")
        {
            try
            {
                outputValue = System.Text.Json.JsonSerializer.Deserialize<object>(output) ?? output;
            }
            catch
            {
                // If JSON parsing fails, use the raw string
                outputValue = output;
            }
        }
        else
        {
            outputValue = output;
        }

        switch (fieldType)
        {
            case "msg":
                SetMessageProperty(msg, field, outputValue);
                break;
            case "flow":
                Flow.Set(field, outputValue);
                break;
            case "global":
                Global.Set(field, outputValue);
                break;
        }

        send(0, msg);
        done();
        return Task.CompletedTask;
    }

    private string ProcessMustacheTemplate(string template, NodeMessage msg)
    {
        var result = template;

        // Replace {{payload}}
        result = Regex.Replace(result, @"\{\{payload\}\}", msg.Payload?.ToString() ?? "");
        
        // Replace {{topic}}
        result = Regex.Replace(result, @"\{\{topic\}\}", msg.Topic ?? "");

        // Replace {{propertyName}} for other message properties
        result = Regex.Replace(result, @"\{\{(\w+)\}\}", match =>
        {
            var key = match.Groups[1].Value;
            if (key == "payload" || key == "topic") return match.Value;
            
            if (msg.Properties.TryGetValue(key, out var value))
            {
                return value?.ToString() ?? "";
            }
            return match.Value;
        });

        // Replace {{flow.name}}
        result = Regex.Replace(result, @"\{\{flow\.(\w+)\}\}", match =>
        {
            var key = match.Groups[1].Value;
            return Flow.Get(key)?.ToString() ?? "";
        });

        // Replace {{global.name}}
        result = Regex.Replace(result, @"\{\{global\.(\w+)\}\}", match =>
        {
            var key = match.Groups[1].Value;
            return Global.Get(key)?.ToString() ?? "";
        });

        return result;
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
