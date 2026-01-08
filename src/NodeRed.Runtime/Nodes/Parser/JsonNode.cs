// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using System.Text.Json;

namespace NodeRed.Runtime.Nodes.Parser;

/// <summary>
/// JSON node - converts between JSON string and object.
/// </summary>
public class JsonNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "json",
        Category = NodeCategory.Parser,
        DisplayName = "json",
        Color = "#DEBD5C",
        Icon = "fa-code",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "action", "" }, // empty means auto-detect, "str" to stringify, "obj" to parse
            { "property", "payload" },
            { "pretty", false }
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var action = GetConfig<string>("action", "");
        var property = GetConfig<string>("property", "payload");
        var pretty = GetConfig<bool>("pretty", false);

        object? data;
        if (property == "payload")
        {
            data = message.Payload;
        }
        else if (message.Properties.TryGetValue(property, out var propData))
        {
            data = propData;
        }
        else
        {
            Done();
            return Task.CompletedTask;
        }

        object? result;
        var effectiveAction = action;

        // Auto-detect action if not specified
        if (string.IsNullOrEmpty(effectiveAction))
        {
            effectiveAction = data is string ? "obj" : "str";
        }

        try
        {
            if (effectiveAction == "str")
            {
                // Convert object to JSON string
                var options = new JsonSerializerOptions
                {
                    WriteIndented = pretty
                };
                result = JsonSerializer.Serialize(data, options);
            }
            else
            {
                // Parse JSON string to object
                if (data is string jsonString)
                {
                    result = JsonSerializer.Deserialize<JsonElement>(jsonString);
                }
                else
                {
                    result = data;
                }
            }

            if (property == "payload")
            {
                message.Payload = result;
            }
            else
            {
                message.Properties[property] = result!;
            }

            Send(message);
        }
        catch (Exception ex)
        {
            Log($"JSON parse error: {ex.Message}", Core.Enums.LogLevel.Error);
        }

        Done();
        return Task.CompletedTask;
    }
}
