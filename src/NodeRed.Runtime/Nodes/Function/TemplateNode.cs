// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Text.RegularExpressions;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Function;

/// <summary>
/// Template node - generates output based on a template with variable substitution.
/// Supports Mustache-like syntax: {{payload}}, {{topic}}, {{msg.property}}.
/// </summary>
public partial class TemplateNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "template",
        DisplayName = "template",
        Category = NodeCategory.Function,
        Color = "#e2d96e",
        Icon = "template",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "name", "" },
            { "field", "payload" },
            { "fieldType", "msg" },
            { "format", "handlebars" },
            { "syntax", "mustache" },
            { "template", "This is the payload: {{payload}}" },
            { "output", "str" }
        },
        HelpText = "Sets a property based on the provided template."
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        try
        {
            var template = GetConfig("template", "{{payload}}");
            var field = GetConfig("field", "payload");
            var output = GetConfig("output", "str");

            var result = ProcessTemplate(template, message);

            // Convert output type if needed
            object? finalResult = output switch
            {
                "json" => System.Text.Json.JsonSerializer.Deserialize<object>(result),
                "yaml" => result, // YAML parsing would need additional library
                _ => result
            };

            // Set the field
            var newMsg = message.Clone();
            switch (field)
            {
                case "payload":
                    newMsg.Payload = finalResult;
                    break;
                case "topic":
                    newMsg.Topic = finalResult?.ToString();
                    break;
                default:
                    newMsg.Properties[field] = finalResult;
                    break;
            }

            Send(newMsg);
            Done();
        }
        catch (Exception ex)
        {
            Log($"Template error: {ex.Message}", LogLevel.Error);
            Done(ex);
        }

        return Task.CompletedTask;
    }

    private string ProcessTemplate(string template, NodeMessage message)
    {
        // Replace {{property}} patterns
        var result = TemplateRegex().Replace(template, match =>
        {
            var property = match.Groups[1].Value.Trim();
            return GetPropertyValue(property, message)?.ToString() ?? "";
        });

        return result;
    }

    private object? GetPropertyValue(string property, NodeMessage message)
    {
        // Handle msg.property syntax
        if (property.StartsWith("msg."))
        {
            property = property[4..];
        }

        return property.ToLowerInvariant() switch
        {
            "payload" => message.Payload,
            "topic" => message.Topic,
            "_msgid" or "msgid" or "id" => message.Id,
            "timestamp" => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            "date" => DateTime.Now.ToString("yyyy-MM-dd"),
            "time" => DateTime.Now.ToString("HH:mm:ss"),
            _ => message.Properties.GetValueOrDefault(property)
        };
    }

    [GeneratedRegex(@"\{\{([^}]+)\}\}")]
    private static partial Regex TemplateRegex();
}
