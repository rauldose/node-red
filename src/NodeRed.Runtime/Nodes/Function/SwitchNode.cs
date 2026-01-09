// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Function;

/// <summary>
/// Switch node - routes messages based on property values.
/// </summary>
public class SwitchNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "switch",
        DisplayName = "switch",
        Category = NodeCategory.Function,
        Color = "#e2d96e",
        Icon = "switch",
        Inputs = 1,
        Outputs = 2,
        Defaults = new Dictionary<string, object?>
        {
            { "name", "" },
            { "property", "payload" },
            { "propertyType", "msg" },
            { "rules", new List<object>() },
            { "checkall", "true" },
            { "repair", false },
            { "outputs", 2 }
        },
        HelpText = "Route messages based on their property values."
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        try
        {
            var property = GetConfig("property", "payload");
            var checkAll = GetConfig("checkall", "true") == "true";
            var value = GetPropertyValue(message, property);
            var rules = GetRules();
            var matchedOutputs = new List<int>();

            for (int i = 0; i < rules.Count; i++)
            {
                if (EvaluateRule(value, rules[i]))
                {
                    matchedOutputs.Add(i);
                    if (!checkAll) break;
                }
            }

            foreach (var output in matchedOutputs)
            {
                Send(output, message.Clone());
            }

            Done();
        }
        catch (Exception ex)
        {
            Log($"Switch node error: {ex.Message}", LogLevel.Error);
            Done(ex);
        }

        return Task.CompletedTask;
    }

    private object? GetPropertyValue(NodeMessage message, string property)
    {
        return property switch
        {
            "payload" => message.Payload,
            "topic" => message.Topic,
            _ => message.Properties.GetValueOrDefault(property)
        };
    }

    private List<Dictionary<string, object?>> GetRules()
    {
        if (Config.Config.TryGetValue("rules", out var rulesObj) &&
            rulesObj is IEnumerable<object> rules)
        {
            return rules.OfType<Dictionary<string, object?>>().ToList();
        }
        return new List<Dictionary<string, object?>>();
    }

    private static bool EvaluateRule(object? value, Dictionary<string, object?> rule)
    {
        var type = rule.GetValueOrDefault("t")?.ToString() ?? "eq";
        var ruleValue = rule.GetValueOrDefault("v");

        return type switch
        {
            "eq" => Equals(value?.ToString(), ruleValue?.ToString()),
            "neq" => !Equals(value?.ToString(), ruleValue?.ToString()),
            "lt" => Compare(value, ruleValue) < 0,
            "lte" => Compare(value, ruleValue) <= 0,
            "gt" => Compare(value, ruleValue) > 0,
            "gte" => Compare(value, ruleValue) >= 0,
            "cont" => value?.ToString()?.Contains(ruleValue?.ToString() ?? "") == true,
            "regex" => MatchesRegex(value, ruleValue),
            "true" => IsTruthy(value),
            "false" => !IsTruthy(value),
            "null" => value == null,
            "nnull" => value != null,
            "empty" => IsEmpty(value),
            "nempty" => !IsEmpty(value),
            "else" => true,
            _ => false
        };
    }

    private static int Compare(object? a, object? b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        if (double.TryParse(a.ToString(), out var numA) &&
            double.TryParse(b.ToString(), out var numB))
        {
            return numA.CompareTo(numB);
        }

        return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }

    private static bool MatchesRegex(object? value, object? pattern)
    {
        if (value == null || pattern == null) return false;
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                value.ToString() ?? "",
                pattern.ToString() ?? "");
        }
        catch
        {
            return false;
        }
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

    private static bool IsEmpty(object? value)
    {
        if (value == null) return true;
        if (value is string s) return string.IsNullOrEmpty(s);
        if (value is System.Collections.ICollection c) return c.Count == 0;
        return false;
    }
}
