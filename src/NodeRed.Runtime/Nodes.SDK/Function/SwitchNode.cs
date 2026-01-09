// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Function;

/// <summary>
/// Switch node - routes messages based on property values.
/// Matches JS Node-RED behavior with full operator support.
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
- **is true** / **is false** - Boolean checks
- **is empty** / **is not empty** - Empty check
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

        // Get rules from config
        var rulesConfig = GetConfig<object?>("rules", null);
        var rules = ParseRules(rulesConfig);

        if (rules.Count == 0)
        {
            // Default behavior: truthy goes to output 0, falsy to output 1
            if (IsTruthy(value))
            {
                send(0, msg);
            }
            else
            {
                send(1, msg);
            }
        }
        else
        {
            // Evaluate rules
            bool elseflag = true;
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                bool matched = EvaluateRule(rule, value, elseflag, msg);
                
                if (matched)
                {
                    send(i, msg);
                    elseflag = false;
                    
                    if (!checkall)
                    {
                        // Stop at first match
                        break;
                    }
                }
            }
        }

        done();
        return Task.CompletedTask;
    }

    private List<SwitchRule> ParseRules(object? rulesConfig)
    {
        var rules = new List<SwitchRule>();
        
        if (rulesConfig == null) return rules;

        try
        {
            if (rulesConfig is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in jsonElement.EnumerateArray())
                {
                    var rule = new SwitchRule
                    {
                        Type = item.TryGetProperty("t", out var t) ? t.GetString() ?? "eq" : "eq",
                        Value = item.TryGetProperty("v", out var v) ? GetJsonValue(v) : null,
                        ValueType = item.TryGetProperty("vt", out var vt) ? vt.GetString() ?? "str" : "str",
                        Value2 = item.TryGetProperty("v2", out var v2) ? GetJsonValue(v2) : null,
                        Value2Type = item.TryGetProperty("v2t", out var v2t) ? v2t.GetString() ?? "str" : "str",
                        CaseInsensitive = item.TryGetProperty("case", out var c) && c.GetBoolean()
                    };
                    rules.Add(rule);
                }
            }
            else if (rulesConfig is IEnumerable<object> enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is Dictionary<string, object?> dict)
                    {
                        rules.Add(new SwitchRule
                        {
                            Type = dict.GetValueOrDefault("t")?.ToString() ?? "eq",
                            Value = dict.GetValueOrDefault("v"),
                            ValueType = dict.GetValueOrDefault("vt")?.ToString() ?? "str"
                        });
                    }
                }
            }
        }
        catch
        {
            // If parsing fails, return empty rules
        }

        return rules;
    }

    private static object? GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetDouble(out var d) ? d : element.GetInt64(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private bool EvaluateRule(SwitchRule rule, object? property, bool elseflag, NodeMessage msg)
    {
        var v1 = ResolveValue(rule.Value, rule.ValueType, msg);
        var v2 = ResolveValue(rule.Value2, rule.Value2Type, msg);

        return rule.Type switch
        {
            "eq" => Equals(property, v1) || (property?.ToString() == v1?.ToString()),
            "neq" => !Equals(property, v1) && (property?.ToString() != v1?.ToString()),
            "lt" => Compare(property, v1) < 0,
            "lte" => Compare(property, v1) <= 0,
            "gt" => Compare(property, v1) > 0,
            "gte" => Compare(property, v1) >= 0,
            "btwn" => IsBetween(property, v1, v2),
            "cont" => (property?.ToString() ?? "").Contains(v1?.ToString() ?? ""),
            "regex" => MatchesRegex(property?.ToString() ?? "", v1?.ToString() ?? "", rule.CaseInsensitive),
            "true" => property is true,
            "false" => property is false,
            "null" => property == null,
            "nnull" => property != null,
            "empty" => IsEmpty(property),
            "nempty" => !IsEmpty(property),
            "istype" => IsType(property, v1?.ToString() ?? ""),
            "hask" => HasKey(property, v1?.ToString() ?? ""),
            "else" or "otherwise" => elseflag,
            _ => false
        };
    }

    private object? ResolveValue(object? value, string valueType, NodeMessage msg)
    {
        return valueType switch
        {
            "num" when double.TryParse(value?.ToString(), out var num) => num,
            "bool" => value?.ToString()?.ToLowerInvariant() == "true",
            "msg" => GetMessageProperty(msg, value?.ToString() ?? ""),
            "flow" => Flow.Get(value?.ToString() ?? ""),
            "global" => Global.Get(value?.ToString() ?? ""),
            _ => value
        };
    }

    private static int Compare(object? a, object? b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        // Try numeric comparison
        if (double.TryParse(a.ToString(), out var numA) && double.TryParse(b.ToString(), out var numB))
        {
            return numA.CompareTo(numB);
        }

        // Fall back to string comparison
        return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }

    private static bool IsBetween(object? a, object? b, object? c)
    {
        if (a == null || b == null || c == null) return false;
        
        if (double.TryParse(a.ToString(), out var numA) &&
            double.TryParse(b.ToString(), out var numB) &&
            double.TryParse(c.ToString(), out var numC))
        {
            return (numA >= numB && numA <= numC) || (numA <= numB && numA >= numC);
        }
        return false;
    }

    private static bool MatchesRegex(string value, string pattern, bool caseInsensitive)
    {
        try
        {
            var options = caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None;
            return Regex.IsMatch(value, pattern, options);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsEmpty(object? value)
    {
        if (value == null) return false; // null is not considered empty
        if (value is string s) return s.Length == 0;
        if (value is Array arr) return arr.Length == 0;
        if (value is ICollection<object> col) return col.Count == 0;
        if (value is IDictionary<string, object?> dict) return dict.Count == 0;
        return false;
    }

    private static bool IsType(object? value, string typeName)
    {
        return typeName switch
        {
            "string" => value is string,
            "number" => value is int or long or float or double or decimal,
            "boolean" => value is bool,
            "array" => value is Array or IList<object>,
            "object" => value is IDictionary<string, object?> || (value != null && !IsType(value, "string") && !IsType(value, "number") && !IsType(value, "boolean") && !IsType(value, "array")),
            "null" => value == null,
            "json" => TryParseJson(value?.ToString() ?? ""),
            _ => false
        };
    }

    private static bool TryParseJson(string value)
    {
        try
        {
            JsonDocument.Parse(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasKey(object? obj, string key)
    {
        if (obj == null || string.IsNullOrEmpty(key)) return false;
        if (obj is IDictionary<string, object?> dict) return dict.ContainsKey(key);
        if (obj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
        {
            return jsonElement.TryGetProperty(key, out _);
        }
        return false;
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

    private class SwitchRule
    {
        public string Type { get; set; } = "eq";
        public object? Value { get; set; }
        public string ValueType { get; set; } = "str";
        public object? Value2 { get; set; }
        public string Value2Type { get; set; } = "str";
        public bool CaseInsensitive { get; set; }
    }
}
