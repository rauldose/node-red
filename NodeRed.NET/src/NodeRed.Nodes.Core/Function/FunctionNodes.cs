// ============================================================
// SOURCE: packages/node_modules/@node-red/nodes/core/function/*.js
// TRANSLATION: Function category nodes - Switch, Change, Range, Template, Delay, Trigger
// ============================================================

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NodeRed.Util;

namespace NodeRed.Nodes.Core.Function;

#region Switch Node
/// <summary>
/// Switch rule configuration
/// SOURCE: n.rules array in switch.js
/// </summary>
public class SwitchRule
{
    [JsonPropertyName("t")]
    public string Type { get; set; } = "eq";
    
    [JsonPropertyName("v")]
    public string? Value { get; set; }
    
    [JsonPropertyName("vt")]
    public string ValueType { get; set; } = "str";
    
    [JsonPropertyName("v2")]
    public string? Value2 { get; set; }
    
    [JsonPropertyName("v2t")]
    public string? Value2Type { get; set; }
    
    [JsonPropertyName("case")]
    public bool CaseInsensitive { get; set; }
}

/// <summary>
/// Switch node configuration
/// SOURCE: SwitchNode constructor parameters
/// </summary>
public class SwitchNodeConfig : NodeConfig
{
    [JsonPropertyName("property")]
    public string Property { get; set; } = "payload";
    
    [JsonPropertyName("propertyType")]
    public string PropertyType { get; set; } = "msg";
    
    [JsonPropertyName("rules")]
    public List<SwitchRule>? Rules { get; set; }
    
    [JsonPropertyName("checkall")]
    public string CheckAll { get; set; } = "true";
    
    [JsonPropertyName("repair")]
    public bool Repair { get; set; }
}

/// <summary>
/// Switch node - Routes messages based on rules
/// SOURCE: packages/node_modules/@node-red/nodes/core/function/10-switch.js
/// 
/// MAPPING NOTES:
/// - operators object → _operators dictionary
/// - SwitchNode(n) → SwitchNode(NodeConfig config)
/// - applyRules() → ApplyRules()
/// </summary>
public class SwitchNode : BaseNode
{
    private readonly List<SwitchRule> _rules;
    private readonly string _property;
    private readonly string _propertyType;
    private readonly bool _checkAll;
    private object? _previousValue;
    
    /// <summary>
    /// Operators dictionary
    /// SOURCE: Lines 20-80 - var operators = { ... }
    /// </summary>
    private static readonly Dictionary<string, Func<object?, object?, object?, bool, bool>> Operators = new()
    {
        ["eq"] = (a, b, _, _) => Equals(a, b),
        ["neq"] = (a, b, _, _) => !Equals(a, b),
        ["lt"] = (a, b, _, _) => Compare(a, b) < 0,
        ["lte"] = (a, b, _, _) => Compare(a, b) <= 0,
        ["gt"] = (a, b, _, _) => Compare(a, b) > 0,
        ["gte"] = (a, b, _, _) => Compare(a, b) >= 0,
        ["btwn"] = (a, b, c, _) =>
        {
            var cmp1 = Compare(a, b);
            var cmp2 = Compare(a, c);
            return (cmp1 >= 0 && cmp2 <= 0) || (cmp1 <= 0 && cmp2 >= 0);
        },
        ["cont"] = (a, b, _, _) => a?.ToString()?.Contains(b?.ToString() ?? "") ?? false,
        ["regex"] = (a, b, _, caseInsensitive) =>
        {
            try
            {
                var options = caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None;
                return Regex.IsMatch(a?.ToString() ?? "", b?.ToString() ?? "", options);
            }
            catch { return false; }
        },
        ["true"] = (a, _, _, _) => a is true,
        ["false"] = (a, _, _, _) => a is false,
        ["null"] = (a, _, _, _) => a == null,
        ["nnull"] = (a, _, _, _) => a != null,
        ["empty"] = (a, _, _, _) => IsEmpty(a),
        ["nempty"] = (a, _, _, _) => !IsEmpty(a),
        ["istype"] = (a, b, _, _) => IsType(a, b?.ToString()),
        ["else"] = (a, _, _, _) => a is true,
        ["hask"] = (a, b, _, _) =>
        {
            if (a is IDictionary<string, object> dict)
                return dict.ContainsKey(b?.ToString() ?? "");
            if (a is JsonElement je && je.ValueKind == JsonValueKind.Object)
                return je.TryGetProperty(b?.ToString() ?? "", out _);
            return false;
        }
    };
    
    private static bool IsEmpty(object? a)
    {
        if (a == null) return false;
        if (a is string s) return s.Length == 0;
        if (a is Array arr) return arr.Length == 0;
        if (a is System.Collections.ICollection col) return col.Count == 0;
        if (a is IDictionary<string, object> dict) return dict.Count == 0;
        return false;
    }
    
    private static bool IsType(object? a, string? type)
    {
        return type switch
        {
            "array" => a is Array || a is System.Collections.IList,
            "buffer" => a is byte[],
            "json" => TryParseJson(a?.ToString()),
            "null" => a == null,
            "number" => a is int or long or float or double or decimal,
            "boolean" => a is bool,
            "string" => a is string,
            "object" => a is IDictionary<string, object> || (a is JsonElement je && je.ValueKind == JsonValueKind.Object),
            _ => false
        };
    }
    
    private static bool TryParseJson(string? s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        try { JsonDocument.Parse(s); return true; }
        catch { return false; }
    }
    
    private static int Compare(object? a, object? b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;
        
        if (a is IComparable ca && b is IComparable cb)
        {
            try { return ca.CompareTo(cb); }
            catch { }
        }
        
        // Try numeric comparison
        if (double.TryParse(a.ToString(), out var da) && double.TryParse(b.ToString(), out var db))
            return da.CompareTo(db);
        
        return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }
    
    public SwitchNode(NodeConfig config) : base(config)
    {
        var switchConfig = config as SwitchNodeConfig ?? new SwitchNodeConfig();
        
        _rules = switchConfig.Rules ?? new List<SwitchRule>();
        _property = switchConfig.Property ?? "payload";
        _propertyType = switchConfig.PropertyType ?? "msg";
        _checkAll = switchConfig.CheckAll != "false";
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                var property = GetProperty(msg);
                var results = ApplyRules(msg, property);
                
                if (results.Any(r => r != null))
                {
                    send(results.ToArray());
                }
                
                _previousValue = property;
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private object? GetProperty(FlowMessage msg)
    {
        return _propertyType switch
        {
            "msg" => GetMessageProperty(msg, _property),
            "flow" => FlowContext.TryGetValue(_property, out var fv) ? fv : null,
            "global" => GlobalContext.TryGetValue(_property, out var gv) ? gv : null,
            _ => null
        };
    }
    
    private object? GetMessageProperty(FlowMessage msg, string property)
    {
        return property switch
        {
            "payload" => msg.Payload,
            "topic" => msg.Topic,
            "_msgid" => msg.MsgId,
            _ => msg.AdditionalProperties?.TryGetValue(property, out var val) == true ? val : null
        };
    }
    
    private List<FlowMessage?> ApplyRules(FlowMessage msg, object? property)
    {
        var results = new List<FlowMessage?>();
        var elseFlag = true;
        
        foreach (var rule in _rules)
        {
            var v1 = EvaluateValue(rule.Value, rule.ValueType, msg);
            var v2 = rule.Value2 != null ? EvaluateValue(rule.Value2, rule.Value2Type, msg) : null;
            
            var testValue = rule.Type == "else" ? elseFlag : property;
            
            if (Operators.TryGetValue(rule.Type, out var op) && op(testValue, v1, v2, rule.CaseInsensitive))
            {
                results.Add(msg.Clone());
                elseFlag = false;
                
                if (!_checkAll)
                    break;
            }
            else
            {
                results.Add(null);
            }
        }
        
        return results;
    }
    
    private object? EvaluateValue(string? value, string? valueType, FlowMessage msg)
    {
        if (valueType == "prev")
            return _previousValue;
        
        return valueType switch
        {
            "str" => value,
            "num" => double.TryParse(value, out var n) ? n : 0,
            "bool" => bool.TryParse(value, out var b) ? b : value?.ToLower() == "true",
            "msg" => GetMessageProperty(msg, value ?? ""),
            "flow" => FlowContext.TryGetValue(value ?? "", out var fv) ? fv : null,
            "global" => GlobalContext.TryGetValue(value ?? "", out var gv) ? gv : null,
            _ => value
        };
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("switch", config => new SwitchNode(config));
    }
}
#endregion

#region Change Node
/// <summary>
/// Change rule configuration
/// SOURCE: n.rules array in change.js
/// </summary>
public class ChangeRule
{
    [JsonPropertyName("t")]
    public string Type { get; set; } = "set"; // set, change, delete, move
    
    [JsonPropertyName("p")]
    public string Property { get; set; } = "";
    
    [JsonPropertyName("pt")]
    public string PropertyType { get; set; } = "msg";
    
    [JsonPropertyName("to")]
    public string? To { get; set; }
    
    [JsonPropertyName("tot")]
    public string ToType { get; set; } = "str";
    
    [JsonPropertyName("from")]
    public string? From { get; set; }
    
    [JsonPropertyName("fromt")]
    public string FromType { get; set; } = "str";
    
    [JsonPropertyName("dc")]
    public bool DeepClone { get; set; }
}

/// <summary>
/// Change node configuration
/// </summary>
public class ChangeNodeConfig : NodeConfig
{
    [JsonPropertyName("rules")]
    public List<ChangeRule>? Rules { get; set; }
}

/// <summary>
/// Change node - Modifies message properties
/// SOURCE: packages/node_modules/@node-red/nodes/core/function/15-change.js
/// </summary>
public class ChangeNode : BaseNode
{
    private readonly List<ChangeRule> _rules;
    
    public ChangeNode(NodeConfig config) : base(config)
    {
        var changeConfig = config as ChangeNodeConfig ?? new ChangeNodeConfig();
        _rules = changeConfig.Rules ?? new List<ChangeRule>();
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                foreach (var rule in _rules)
                {
                    ApplyRule(msg, rule);
                }
                send(msg);
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private void ApplyRule(FlowMessage msg, ChangeRule rule)
    {
        var value = EvaluateValue(rule.To, rule.ToType, msg);
        if (rule.DeepClone && value != null)
        {
            value = JsonSerializer.Deserialize<object>(JsonSerializer.Serialize(value));
        }
        
        switch (rule.Type)
        {
            case "set":
                SetProperty(msg, rule.Property, rule.PropertyType, value);
                break;
                
            case "delete":
                SetProperty(msg, rule.Property, rule.PropertyType, null);
                break;
                
            case "change":
                var current = GetProperty(msg, rule.Property, rule.PropertyType);
                if (current is string str)
                {
                    var from = EvaluateValue(rule.From, rule.FromType, msg);
                    var pattern = rule.FromType == "re" ? rule.From : Regex.Escape(from?.ToString() ?? "");
                    try
                    {
                        var replaced = Regex.Replace(str, pattern ?? "", value?.ToString() ?? "", RegexOptions.None);
                        SetProperty(msg, rule.Property, rule.PropertyType, replaced);
                    }
                    catch { }
                }
                break;
                
            case "move":
                var moveValue = GetProperty(msg, rule.Property, rule.PropertyType);
                SetProperty(msg, rule.To ?? "", rule.ToType, moveValue);
                SetProperty(msg, rule.Property, rule.PropertyType, null);
                break;
        }
    }
    
    private object? GetProperty(FlowMessage msg, string property, string propertyType)
    {
        return propertyType switch
        {
            "msg" => GetMessageProperty(msg, property),
            "flow" => FlowContext.TryGetValue(property, out var fv) ? fv : null,
            "global" => GlobalContext.TryGetValue(property, out var gv) ? gv : null,
            _ => null
        };
    }
    
    private void SetProperty(FlowMessage msg, string property, string propertyType, object? value)
    {
        switch (propertyType)
        {
            case "msg":
                SetMessageProperty(msg, property, value);
                break;
            case "flow":
                if (value == null) FlowContext.TryRemove(property, out _);
                else FlowContext[property] = value;
                break;
            case "global":
                if (value == null) GlobalContext.TryRemove(property, out _);
                else GlobalContext[property] = value;
                break;
        }
    }
    
    private object? GetMessageProperty(FlowMessage msg, string property)
    {
        return property switch
        {
            "payload" => msg.Payload,
            "topic" => msg.Topic,
            "_msgid" => msg.MsgId,
            _ => msg.AdditionalProperties?.TryGetValue(property, out var val) == true ? val : null
        };
    }
    
    private void SetMessageProperty(FlowMessage msg, string property, object? value)
    {
        switch (property)
        {
            case "payload":
                msg.Payload = value;
                break;
            case "topic":
                msg.Topic = value?.ToString();
                break;
            default:
                msg.AdditionalProperties ??= new Dictionary<string, JsonElement>();
                if (value == null)
                    msg.AdditionalProperties.Remove(property);
                else
                    msg.AdditionalProperties[property] = JsonSerializer.Deserialize<JsonElement>(
                        JsonSerializer.Serialize(value));
                break;
        }
    }
    
    private object? EvaluateValue(string? value, string? valueType, FlowMessage msg)
    {
        return valueType switch
        {
            "str" => value,
            "num" => double.TryParse(value, out var n) ? n : 0,
            "bool" => bool.TryParse(value, out var b) ? b : value?.ToLower() == "true",
            "json" => !string.IsNullOrEmpty(value) ? JsonSerializer.Deserialize<object>(value) : null,
            "date" => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            "msg" => GetMessageProperty(msg, value ?? ""),
            "flow" => FlowContext.TryGetValue(value ?? "", out var fv) ? fv : null,
            "global" => GlobalContext.TryGetValue(value ?? "", out var gv) ? gv : null,
            "env" => Environment.GetEnvironmentVariable(value ?? ""),
            _ => value
        };
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("change", config => new ChangeNode(config));
    }
}
#endregion

#region Range Node
/// <summary>
/// Range node configuration
/// SOURCE: 16-range.js
/// </summary>
public class RangeNodeConfig : NodeConfig
{
    [JsonPropertyName("minin")]
    public double MinIn { get; set; } = 0;
    
    [JsonPropertyName("maxin")]
    public double MaxIn { get; set; } = 100;
    
    [JsonPropertyName("minout")]
    public double MinOut { get; set; } = 0;
    
    [JsonPropertyName("maxout")]
    public double MaxOut { get; set; } = 100;
    
    [JsonPropertyName("action")]
    public string Action { get; set; } = "scale"; // scale, clamp, roll
    
    [JsonPropertyName("round")]
    public bool Round { get; set; }
    
    [JsonPropertyName("property")]
    public string Property { get; set; } = "payload";
}

/// <summary>
/// Range node - Maps input value to output range
/// SOURCE: packages/node_modules/@node-red/nodes/core/function/16-range.js
/// </summary>
public class RangeNode : BaseNode
{
    private readonly double _minIn, _maxIn, _minOut, _maxOut;
    private readonly string _action;
    private readonly bool _round;
    private readonly string _property;
    
    public RangeNode(NodeConfig config) : base(config)
    {
        var rangeConfig = config as RangeNodeConfig ?? new RangeNodeConfig();
        
        _minIn = rangeConfig.MinIn;
        _maxIn = rangeConfig.MaxIn;
        _minOut = rangeConfig.MinOut;
        _maxOut = rangeConfig.MaxOut;
        _action = rangeConfig.Action ?? "scale";
        _round = rangeConfig.Round;
        _property = rangeConfig.Property ?? "payload";
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                var input = GetPayloadValue(msg);
                if (input.HasValue)
                {
                    var output = MapValue(input.Value);
                    SetPayloadValue(msg, output);
                    send(msg);
                }
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private double? GetPayloadValue(FlowMessage msg)
    {
        var val = _property == "payload" ? msg.Payload : 
            msg.AdditionalProperties?.TryGetValue(_property, out var v) == true ? v : null;
        
        if (val == null) return null;
        if (double.TryParse(val.ToString(), out var d)) return d;
        return null;
    }
    
    private void SetPayloadValue(FlowMessage msg, double value)
    {
        if (_property == "payload")
            msg.Payload = value;
        else
        {
            msg.AdditionalProperties ??= new Dictionary<string, JsonElement>();
            msg.AdditionalProperties[_property] = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(value));
        }
    }
    
    private double MapValue(double input)
    {
        double output;
        
        switch (_action)
        {
            case "clamp":
                // Clamp to input range, then scale
                input = Math.Max(_minIn, Math.Min(_maxIn, input));
                output = ScaleValue(input);
                break;
                
            case "roll":
                // Wrap around input range
                var range = _maxIn - _minIn;
                if (range > 0)
                {
                    input = ((input - _minIn) % range + range) % range + _minIn;
                }
                output = ScaleValue(input);
                break;
                
            default: // scale
                output = ScaleValue(input);
                break;
        }
        
        return _round ? Math.Round(output) : output;
    }
    
    private double ScaleValue(double input)
    {
        var rangeIn = _maxIn - _minIn;
        var rangeOut = _maxOut - _minOut;
        
        if (rangeIn == 0) return _minOut;
        
        return ((input - _minIn) / rangeIn) * rangeOut + _minOut;
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("range", config => new RangeNode(config));
    }
}
#endregion

#region Delay Node
/// <summary>
/// Delay node configuration
/// SOURCE: 89-delay.js
/// </summary>
public class DelayNodeConfig : NodeConfig
{
    [JsonPropertyName("pauseType")]
    public string PauseType { get; set; } = "delay"; // delay, delayv, rate, random, queue, timed
    
    [JsonPropertyName("timeout")]
    public double Timeout { get; set; } = 5;
    
    [JsonPropertyName("timeoutUnits")]
    public string TimeoutUnits { get; set; } = "seconds";
    
    [JsonPropertyName("rate")]
    public double Rate { get; set; } = 1;
    
    [JsonPropertyName("rateUnits")]
    public string RateUnits { get; set; } = "second";
    
    [JsonPropertyName("randomFirst")]
    public double RandomFirst { get; set; } = 1;
    
    [JsonPropertyName("randomLast")]
    public double RandomLast { get; set; } = 5;
    
    [JsonPropertyName("randomUnits")]
    public string RandomUnits { get; set; } = "seconds";
    
    [JsonPropertyName("drop")]
    public bool Drop { get; set; }
}

/// <summary>
/// Delay node - Delays messages
/// SOURCE: packages/node_modules/@node-red/nodes/core/function/89-delay.js
/// </summary>
public class DelayNode : BaseNode
{
    private readonly string _pauseType;
    private readonly int _timeoutMs;
    private readonly int _rateMs;
    private readonly int _randomFirstMs;
    private readonly int _randomLastMs;
    private readonly bool _drop;
    private readonly Queue<(FlowMessage msg, Action<object?> send, Action<Exception?> done)> _queue = new();
    private Timer? _rateTimer;
    private bool _processing;
    
    public DelayNode(NodeConfig config) : base(config)
    {
        var delayConfig = config as DelayNodeConfig ?? new DelayNodeConfig();
        
        _pauseType = delayConfig.PauseType ?? "delay";
        _timeoutMs = ConvertToMs(delayConfig.Timeout, delayConfig.TimeoutUnits);
        _rateMs = ConvertRateToMs(delayConfig.Rate, delayConfig.RateUnits);
        _randomFirstMs = ConvertToMs(delayConfig.RandomFirst, delayConfig.RandomUnits);
        _randomLastMs = ConvertToMs(delayConfig.RandomLast, delayConfig.RandomUnits);
        _drop = delayConfig.Drop;
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                switch (_pauseType)
                {
                    case "delay":
                        await Task.Delay(_timeoutMs);
                        send(msg);
                        break;
                        
                    case "random":
                        var delay = Random.Shared.Next(_randomFirstMs, _randomLastMs + 1);
                        await Task.Delay(delay);
                        send(msg);
                        break;
                        
                    case "rate":
                    case "queue":
                        if (_drop && _queue.Count > 0)
                        {
                            // Drop this message
                        }
                        else
                        {
                            _queue.Enqueue((msg, send, done));
                            StartRateProcessing();
                            return; // Don't call done yet
                        }
                        break;
                        
                    default:
                        send(msg);
                        break;
                }
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private void StartRateProcessing()
    {
        if (_processing) return;
        _processing = true;
        
        _rateTimer = SetInterval(() =>
        {
            if (_queue.TryDequeue(out var item))
            {
                item.send(item.msg);
                item.done(null);
            }
            else
            {
                _processing = false;
                if (_rateTimer != null)
                {
                    ClearInterval(_rateTimer);
                    _rateTimer = null;
                }
            }
        }, _rateMs);
    }
    
    private int ConvertToMs(double value, string units)
    {
        return units switch
        {
            "milliseconds" => (int)value,
            "seconds" => (int)(value * 1000),
            "minutes" => (int)(value * 60000),
            "hours" => (int)(value * 3600000),
            "days" => (int)(value * 86400000),
            _ => (int)(value * 1000)
        };
    }
    
    private int ConvertRateToMs(double rate, string units)
    {
        if (rate <= 0) rate = 1;
        
        var msPerUnit = units switch
        {
            "second" => 1000,
            "minute" => 60000,
            "hour" => 3600000,
            "day" => 86400000,
            _ => 1000
        };
        
        return (int)(msPerUnit / rate);
    }
    
    public override async Task CloseAsync(bool removed = false)
    {
        if (_rateTimer != null)
        {
            ClearInterval(_rateTimer);
            _rateTimer = null;
        }
        
        // Complete any queued messages
        while (_queue.TryDequeue(out var item))
        {
            item.done(null);
        }
        
        await base.CloseAsync(removed);
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("delay", config => new DelayNode(config));
    }
}
#endregion

#region Trigger Node
/// <summary>
/// Trigger node configuration
/// SOURCE: 89-trigger.js
/// </summary>
public class TriggerNodeConfig : NodeConfig
{
    [JsonPropertyName("op1")]
    public string? Op1 { get; set; } = "1";
    
    [JsonPropertyName("op1type")]
    public string Op1Type { get; set; } = "str";
    
    [JsonPropertyName("op2")]
    public string? Op2 { get; set; } = "0";
    
    [JsonPropertyName("op2type")]
    public string Op2Type { get; set; } = "str";
    
    [JsonPropertyName("duration")]
    public double Duration { get; set; } = 250;
    
    [JsonPropertyName("units")]
    public string Units { get; set; } = "ms";
    
    [JsonPropertyName("extend")]
    public bool Extend { get; set; } = true;
    
    [JsonPropertyName("overrideDelay")]
    public bool OverrideDelay { get; set; }
    
    [JsonPropertyName("reset")]
    public string? Reset { get; set; }
}

/// <summary>
/// Trigger node - Creates timed sequences
/// SOURCE: packages/node_modules/@node-red/nodes/core/function/89-trigger.js
/// </summary>
public class TriggerNode : BaseNode
{
    private readonly string? _op1;
    private readonly string _op1Type;
    private readonly string? _op2;
    private readonly string _op2Type;
    private readonly int _durationMs;
    private readonly bool _extend;
    private readonly string? _reset;
    private Timer? _activeTimer;
    private bool _triggered;
    
    public TriggerNode(NodeConfig config) : base(config)
    {
        var triggerConfig = config as TriggerNodeConfig ?? new TriggerNodeConfig();
        
        _op1 = triggerConfig.Op1;
        _op1Type = triggerConfig.Op1Type ?? "str";
        _op2 = triggerConfig.Op2;
        _op2Type = triggerConfig.Op2Type ?? "str";
        _durationMs = ConvertToMs(triggerConfig.Duration, triggerConfig.Units);
        _extend = triggerConfig.Extend;
        _reset = triggerConfig.Reset;
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                // Check for reset
                if (!string.IsNullOrEmpty(_reset) && msg.Payload?.ToString() == _reset)
                {
                    ResetTrigger();
                    done(null);
                    return;
                }
                
                if (!_triggered || _extend)
                {
                    _triggered = true;
                    
                    // Send first value
                    if (_op1Type != "nul")
                    {
                        var msg1 = msg.Clone();
                        msg1.Payload = EvaluateValue(_op1, _op1Type, msg);
                        send(msg1);
                    }
                    
                    // Clear existing timer if extending
                    if (_activeTimer != null && _extend)
                    {
                        ClearTimeout(_activeTimer);
                    }
                    
                    // Set timer for second value
                    _activeTimer = SetTimeout(() =>
                    {
                        if (_op2Type != "nul")
                        {
                            var msg2 = msg.Clone();
                            msg2.Payload = EvaluateValue(_op2, _op2Type, msg);
                            send(msg2);
                        }
                        _triggered = false;
                        _activeTimer = null;
                    }, _durationMs);
                }
                
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private void ResetTrigger()
    {
        if (_activeTimer != null)
        {
            ClearTimeout(_activeTimer);
            _activeTimer = null;
        }
        _triggered = false;
    }
    
    private object? EvaluateValue(string? value, string valueType, FlowMessage msg)
    {
        return valueType switch
        {
            "str" => value,
            "num" => double.TryParse(value, out var n) ? n : 0,
            "bool" => bool.TryParse(value, out var b) ? b : value?.ToLower() == "true",
            "pay" => msg.Payload,
            "payl" => msg.Payload,
            "date" => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            _ => value
        };
    }
    
    private int ConvertToMs(double value, string units)
    {
        return units switch
        {
            "ms" => (int)value,
            "s" => (int)(value * 1000),
            "min" => (int)(value * 60000),
            "hr" => (int)(value * 3600000),
            _ => (int)value
        };
    }
    
    public override async Task CloseAsync(bool removed = false)
    {
        ResetTrigger();
        await base.CloseAsync(removed);
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("trigger", config => new TriggerNode(config));
    }
}
#endregion

#region Template Node
/// <summary>
/// Template node configuration
/// SOURCE: 80-template.js
/// </summary>
public class TemplateNodeConfig : NodeConfig
{
    [JsonPropertyName("template")]
    public string Template { get; set; } = "";
    
    [JsonPropertyName("syntax")]
    public string Syntax { get; set; } = "mustache";
    
    [JsonPropertyName("field")]
    public string Field { get; set; } = "payload";
    
    [JsonPropertyName("fieldType")]
    public string FieldType { get; set; } = "msg";
    
    [JsonPropertyName("format")]
    public string Format { get; set; } = "handlebars";
    
    [JsonPropertyName("output")]
    public string Output { get; set; } = "str";
}

/// <summary>
/// Template node - Generates output based on template
/// SOURCE: packages/node_modules/@node-red/nodes/core/function/80-template.js
/// </summary>
public class TemplateNode : BaseNode
{
    private readonly string _template;
    private readonly string _syntax;
    private readonly string _field;
    private readonly string _output;
    
    public TemplateNode(NodeConfig config) : base(config)
    {
        var templateConfig = config as TemplateNodeConfig ?? new TemplateNodeConfig();
        
        _template = templateConfig.Template ?? "";
        _syntax = templateConfig.Syntax ?? "mustache";
        _field = templateConfig.Field ?? "payload";
        _output = templateConfig.Output ?? "str";
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                var result = RenderTemplate(_template, msg);
                
                // Apply output type conversion
                object? output = _output switch
                {
                    "json" => JsonSerializer.Deserialize<object>(result),
                    _ => result
                };
                
                // Set the output field
                if (_field == "payload")
                    msg.Payload = output;
                else
                {
                    msg.AdditionalProperties ??= new Dictionary<string, JsonElement>();
                    msg.AdditionalProperties[_field] = JsonSerializer.Deserialize<JsonElement>(
                        JsonSerializer.Serialize(output));
                }
                
                send(msg);
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    /// <summary>
    /// Simple mustache-style template rendering
    /// SOURCE: Mustache.render in template.js
    /// </summary>
    private string RenderTemplate(string template, FlowMessage msg)
    {
        var result = template;
        
        // Simple variable replacement: {{payload}}, {{topic}}, etc.
        result = Regex.Replace(result, @"\{\{(\w+)\}\}", match =>
        {
            var key = match.Groups[1].Value;
            return key switch
            {
                "payload" => msg.Payload?.ToString() ?? "",
                "topic" => msg.Topic ?? "",
                "_msgid" => msg.MsgId,
                _ => msg.AdditionalProperties?.TryGetValue(key, out var val) == true ? val.ToString() ?? "" : ""
            };
        });
        
        // Nested property replacement: {{msg.payload.value}}
        result = Regex.Replace(result, @"\{\{msg\.(\w+)\.(\w+)\}\}", match =>
        {
            var prop = match.Groups[1].Value;
            var subProp = match.Groups[2].Value;
            
            object? obj = prop switch
            {
                "payload" => msg.Payload,
                _ => msg.AdditionalProperties?.TryGetValue(prop, out var val) == true ? val : null
            };
            
            if (obj is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
                if (je.TryGetProperty(subProp, out var subVal))
                    return subVal.ToString();
            }
            
            return "";
        });
        
        return result;
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("template", config => new TemplateNode(config));
    }
}
#endregion

#region Exec Node
/// <summary>
/// Exec node configuration
/// SOURCE: 90-exec.js
/// </summary>
public class ExecNodeConfig : NodeConfig
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "";
    
    [JsonPropertyName("addpay")]
    public string AddPay { get; set; } = "payload";
    
    [JsonPropertyName("append")]
    public string? Append { get; set; }
    
    [JsonPropertyName("useSpawn")]
    public string UseSpawn { get; set; } = "false";
    
    [JsonPropertyName("timer")]
    public int Timer { get; set; } = 0;
    
    [JsonPropertyName("oldrc")]
    public bool OldRc { get; set; }
}

/// <summary>
/// Exec node - Executes system commands
/// SOURCE: packages/node_modules/@node-red/nodes/core/function/90-exec.js
/// </summary>
public class ExecNode : BaseNode
{
    private readonly string _command;
    private readonly string _addPay;
    private readonly string? _append;
    private readonly bool _useSpawn;
    private readonly int _timeout;
    
    public ExecNode(NodeConfig config) : base(config)
    {
        var execConfig = config as ExecNodeConfig ?? new ExecNodeConfig();
        
        _command = execConfig.Command ?? "";
        _addPay = execConfig.AddPay ?? "payload";
        _append = execConfig.Append;
        _useSpawn = execConfig.UseSpawn == "true";
        _timeout = execConfig.Timer > 0 ? execConfig.Timer * 1000 : 0;
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                var cmd = BuildCommand(msg);
                var result = await ExecuteCommandAsync(cmd);
                
                // Output 1: stdout
                var msg1 = msg.Clone();
                msg1.Payload = result.StdOut;
                
                // Output 2: stderr
                var msg2 = msg.Clone();
                msg2.Payload = result.StdErr;
                
                // Output 3: return code
                var msg3 = msg.Clone();
                msg3.Payload = result.ExitCode;
                
                send(new object?[] { msg1, msg2, msg3 });
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private string BuildCommand(FlowMessage msg)
    {
        var cmd = _command;
        
        // Add payload if configured
        if (!string.IsNullOrEmpty(_addPay) && _addPay != "none")
        {
            var payload = msg.Payload?.ToString() ?? "";
            cmd += " " + payload;
        }
        
        // Add append if configured
        if (!string.IsNullOrEmpty(_append))
        {
            cmd += " " + _append;
        }
        
        return cmd;
    }
    
    private async Task<(string StdOut, string StdErr, int ExitCode)> ExecuteCommandAsync(string command)
    {
        using var process = new System.Diagnostics.Process();
        
        // Determine shell based on OS
        if (OperatingSystem.IsWindows())
        {
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/c {command}";
        }
        else
        {
            process.StartInfo.FileName = "/bin/sh";
            process.StartInfo.Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"";
        }
        
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;
        
        process.Start();
        
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        
        if (_timeout > 0)
        {
            var completed = await Task.Run(() => process.WaitForExit(_timeout));
            if (!completed)
            {
                process.Kill();
                throw new TimeoutException($"Command timed out after {_timeout}ms");
            }
        }
        else
        {
            await process.WaitForExitAsync();
        }
        
        return (await stdOutTask, await stdErrTask, process.ExitCode);
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("exec", config => new ExecNode(config));
    }
}
#endregion
