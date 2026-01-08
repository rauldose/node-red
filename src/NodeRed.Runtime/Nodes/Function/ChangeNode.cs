// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Function;

/// <summary>
/// Change node - modifies message properties (set, change, delete, move).
/// </summary>
public class ChangeNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "change",
        DisplayName = "change",
        Category = NodeCategory.Function,
        Color = "#e2d96e",
        Icon = "change",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "name", "" },
            { "rules", new List<object>() },
            { "action", "" },
            { "property", "" },
            { "from", "" },
            { "to", "" },
            { "reg", false }
        },
        HelpText = "Set, change, delete or move properties of a message, flow context or global context."
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        try
        {
            var result = message.Clone();

            if (Config.Config.TryGetValue("rules", out var rulesObj) && 
                rulesObj is IEnumerable<object> rules)
            {
                foreach (var rule in rules)
                {
                    ProcessRule(result, rule);
                }
            }
            else
            {
                ProcessLegacyRule(result);
            }

            Send(result);
            Done();
        }
        catch (Exception ex)
        {
            Log($"Change node error: {ex.Message}", LogLevel.Error);
            Done(ex);
        }

        return Task.CompletedTask;
    }

    private void ProcessRule(NodeMessage message, object rule)
    {
        if (rule is not Dictionary<string, object?> ruleDict) return;

        var action = ruleDict.GetValueOrDefault("t")?.ToString() ?? "set";
        var property = ruleDict.GetValueOrDefault("p")?.ToString() ?? "payload";
        var value = ruleDict.GetValueOrDefault("to");
        var valueType = ruleDict.GetValueOrDefault("tot")?.ToString() ?? "str";

        switch (action)
        {
            case "set":
                SetProperty(message, property, ResolveValue(message, value, valueType));
                break;
            case "change":
                var from = ruleDict.GetValueOrDefault("from")?.ToString() ?? "";
                var to = ruleDict.GetValueOrDefault("to")?.ToString() ?? "";
                ChangeProperty(message, property, from, to);
                break;
            case "delete":
                DeleteProperty(message, property);
                break;
            case "move":
                var target = ruleDict.GetValueOrDefault("to")?.ToString();
                if (target != null) MoveProperty(message, property, target);
                break;
        }
    }

    private object? ResolveValue(NodeMessage message, object? value, string valueType)
    {
        return valueType switch
        {
            "str" => value?.ToString(),
            "num" when double.TryParse(value?.ToString(), out var num) => num,
            "bool" => value?.ToString()?.ToLowerInvariant() == "true",
            "json" => value,
            "msg" => GetMessageProperty(message, value?.ToString() ?? ""),
            "flow" => Context.GetFlowContext<object>(value?.ToString() ?? ""),
            "global" => Context.GetGlobalContext<object>(value?.ToString() ?? ""),
            "date" => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            _ => value
        };
    }

    private object? GetMessageProperty(NodeMessage message, string property)
    {
        return property switch
        {
            "payload" => message.Payload,
            "topic" => message.Topic,
            _ => message.Properties.GetValueOrDefault(property)
        };
    }

    private void ProcessLegacyRule(NodeMessage message)
    {
        var action = GetConfig("action", "set");
        var property = GetConfig("property", "payload");
        var to = Config.Config.GetValueOrDefault("to");

        switch (action)
        {
            case "set":
                SetProperty(message, property, to);
                break;
            case "delete":
                DeleteProperty(message, property);
                break;
        }
    }

    private static void SetProperty(NodeMessage message, string property, object? value)
    {
        switch (property)
        {
            case "payload":
                message.Payload = value;
                break;
            case "topic":
                message.Topic = value?.ToString();
                break;
            default:
                message.Properties[property] = value;
                break;
        }
    }

    private static void ChangeProperty(NodeMessage message, string property, string from, string to)
    {
        var currentValue = property switch
        {
            "payload" => message.Payload?.ToString(),
            "topic" => message.Topic,
            _ => message.Properties.GetValueOrDefault(property)?.ToString()
        };

        if (currentValue != null)
        {
            var newValue = currentValue.Replace(from, to);
            SetProperty(message, property, newValue);
        }
    }

    private static void DeleteProperty(NodeMessage message, string property)
    {
        switch (property)
        {
            case "payload":
                message.Payload = null;
                break;
            case "topic":
                message.Topic = null;
                break;
            default:
                message.Properties.Remove(property);
                break;
        }
    }

    private static void MoveProperty(NodeMessage message, string source, string target)
    {
        object? value = source switch
        {
            "payload" => message.Payload,
            "topic" => message.Topic,
            _ => message.Properties.GetValueOrDefault(source)
        };

        DeleteProperty(message, source);
        SetProperty(message, target, value);
    }
}
