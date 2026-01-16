// ============================================================
// SOURCE: packages/node_modules/@node-red/nodes/core/common/20-inject.js
// TRANSLATION: Inject node - triggers flow execution
// ============================================================
// ORIGINAL CODE (key sections):
// ------------------------------------------------------------
// function InjectNode(n) {
//     RED.nodes.createNode(this,n);
//     this.props = n.props;
//     this.repeat = n.repeat;
//     this.crontab = n.crontab;
//     this.once = n.once;
//     this.onceDelay = (n.onceDelay || 0.1) * 1000;
//     ...
//     this.on("input", function(msg, send, done) { ... });
// }
// RED.nodes.registerType("inject",InjectNode);
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NodeRed.Nodes.Core.Common;

/// <summary>
/// Property configuration for inject node
/// SOURCE: n.props array in inject.js
/// </summary>
public class InjectProperty
{
    [JsonPropertyName("p")]
    public string Property { get; set; } = "payload";
    
    [JsonPropertyName("v")]
    public string? Value { get; set; }
    
    [JsonPropertyName("vt")]
    public string ValueType { get; set; } = "str";
}

/// <summary>
/// Inject node configuration
/// SOURCE: InjectNode constructor parameters
/// </summary>
public class InjectNodeConfig : NodeConfig
{
    [JsonPropertyName("props")]
    public List<InjectProperty>? Props { get; set; }
    
    [JsonPropertyName("repeat")]
    public double? Repeat { get; set; }
    
    [JsonPropertyName("crontab")]
    public string? Crontab { get; set; }
    
    [JsonPropertyName("once")]
    public bool Once { get; set; }
    
    [JsonPropertyName("onceDelay")]
    public double OnceDelay { get; set; } = 0.1;
    
    // Legacy properties
    [JsonPropertyName("payload")]
    public string? Payload { get; set; }
    
    [JsonPropertyName("payloadType")]
    public string? PayloadType { get; set; }
    
    [JsonPropertyName("topic")]
    public string? Topic { get; set; }
}

/// <summary>
/// Inject node - Triggers flow execution with configurable payload
/// SOURCE: packages/node_modules/@node-red/nodes/core/common/20-inject.js
/// 
/// MAPPING NOTES:
/// - InjectNode(n) → InjectNode(InjectNodeConfig config)
/// - this.props → _props
/// - this.repeat → _repeat
/// - this.interval_id → _intervalTimer
/// - this.on("input") → OnInput handler
/// - setInterval → Timer with periodic callback
/// - setTimeout → Task.Delay
/// </summary>
public class InjectNode : BaseNode
{
    private readonly List<InjectProperty> _props;
    private readonly double? _repeat;
    private readonly string? _crontab;
    private readonly bool _once;
    private readonly double _onceDelay;
    private Timer? _intervalTimer;
    private Timer? _onceTimer;
    
    /// <summary>
    /// Constructor - equivalent to function InjectNode(n)
    /// SOURCE: Lines 21-95 of 20-inject.js
    /// </summary>
    public InjectNode(NodeConfig config) : base(config)
    {
        var injectConfig = config as InjectNodeConfig ?? new InjectNodeConfig();
        
        // Handle legacy format
        // SOURCE: Lines 24-46 - if(!Array.isArray(n.props))
        if (injectConfig.Props == null || injectConfig.Props.Count == 0)
        {
            _props = new List<InjectProperty>
            {
                new() { Property = "payload", Value = injectConfig.Payload, ValueType = injectConfig.PayloadType ?? "date" },
                new() { Property = "topic", Value = injectConfig.Topic, ValueType = "str" }
            };
        }
        else
        {
            _props = injectConfig.Props;
            
            // Handle legacy payload/topic values in props
            foreach (var prop in _props)
            {
                if (prop.Property == "payload" && prop.Value == null)
                {
                    prop.Value = injectConfig.Payload;
                    prop.ValueType = injectConfig.PayloadType ?? "date";
                }
                else if (prop.Property == "topic" && prop.ValueType == "str" && prop.Value == null)
                {
                    prop.Value = injectConfig.Topic;
                }
            }
        }
        
        _repeat = injectConfig.Repeat;
        _crontab = injectConfig.Crontab;
        _once = injectConfig.Once;
        _onceDelay = (injectConfig.OnceDelay > 0 ? injectConfig.OnceDelay : 0.1) * 1000;
        
        // Validate repeat value
        // SOURCE: Lines 70-73 - if (node.repeat > 2147483)
        if (_repeat > 2147483)
        {
            Error("Repeat interval too long");
            _repeat = null;
        }
        
        // Register input handler
        // SOURCE: Lines 97-162 - this.on("input", function(msg, send, done))
        OnInput(async (msg, send, done) =>
        {
            try
            {
                await EvaluatePropertiesAsync(msg, _props);
                send(msg);
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
        
        // Setup repeater
        // SOURCE: Lines 88-95 - if (this.once) / node.repeaterSetup()
        if (_once)
        {
            // Once mode: inject once after delay, then setup repeater
            _onceTimer = SetTimeout(() =>
            {
                _ = ReceiveAsync(new FlowMessage());
                SetupRepeater();
            }, (int)_onceDelay);
        }
        else
        {
            SetupRepeater();
        }
    }
    
    /// <summary>
    /// Setup the repeater timer
    /// SOURCE: Lines 75-86 - node.repeaterSetup = function()
    /// </summary>
    private void SetupRepeater()
    {
        if (_repeat.HasValue && _repeat.Value > 0)
        {
            // Repeat mode: inject at regular intervals
            // SOURCE: Lines 76-81 - this.interval_id = setInterval(...)
            var repeatMs = (int)(_repeat.Value * 1000);
            Debug($"Repeat interval: {repeatMs}ms");
            
            _intervalTimer = SetInterval(() =>
            {
                _ = ReceiveAsync(new FlowMessage());
            }, repeatMs);
        }
        else if (!string.IsNullOrEmpty(_crontab))
        {
            // Cron mode: inject at cron schedule
            // SOURCE: Lines 82-85 - this.cronjob = scheduleTask(...)
            Debug($"Crontab: {_crontab}");
            // Note: Full cron support would require a cron library like Cronos
            // For now, we log that cron is configured but not fully implemented
            Warn("Crontab scheduling requires additional cron library integration");
        }
    }
    
    /// <summary>
    /// Evaluate properties and set them on the message
    /// SOURCE: Lines 105-151 - function evaluateProperty(doneEvaluating)
    /// </summary>
    private async Task EvaluatePropertiesAsync(FlowMessage msg, List<InjectProperty> props)
    {
        foreach (var prop in props)
        {
            if (string.IsNullOrEmpty(prop.Property))
                continue;
            
            var value = EvaluatePropertyValue(prop.Value, prop.ValueType, msg);
            SetMessageProperty(msg, prop.Property, value);
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Evaluate a property value based on type
    /// SOURCE: Lines 134-147 - RED.util.evaluateNodeProperty(...)
    /// </summary>
    private object? EvaluatePropertyValue(string? value, string valueType, FlowMessage msg)
    {
        return valueType switch
        {
            "str" => value ?? "",
            "num" => double.TryParse(value, out var num) ? num : 0,
            "bool" => bool.TryParse(value, out var b) ? b : value?.ToLower() == "true",
            "json" => !string.IsNullOrEmpty(value) ? JsonSerializer.Deserialize<object>(value) : null,
            "date" => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            "bin" => !string.IsNullOrEmpty(value) ? Convert.FromBase64String(value) : Array.Empty<byte>(),
            "msg" => GetMessageProperty(msg, value ?? "payload"),
            "flow" => FlowContext.TryGetValue(value ?? "", out var fv) ? fv : null,
            "global" => GlobalContext.TryGetValue(value ?? "", out var gv) ? gv : null,
            "env" => Environment.GetEnvironmentVariable(value ?? ""),
            _ => value
        };
    }
    
    /// <summary>
    /// Get a property from a message
    /// SOURCE: RED.util.getMessageProperty
    /// </summary>
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
    
    /// <summary>
    /// Set a property on a message
    /// SOURCE: RED.util.setMessageProperty
    /// </summary>
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
            case "_msgid":
                msg.MsgId = value?.ToString() ?? global::NodeRed.Util.Util.GenerateId();
                break;
            default:
                msg.AdditionalProperties ??= new Dictionary<string, JsonElement>();
                if (value != null)
                {
                    var json = JsonSerializer.Serialize(value);
                    msg.AdditionalProperties[property] = JsonSerializer.Deserialize<JsonElement>(json);
                }
                break;
        }
    }
    
    /// <summary>
    /// Close handler - cleanup timers
    /// SOURCE: Lines 167-177 - InjectNode.prototype.close
    /// </summary>
    public override async Task CloseAsync(bool removed = false)
    {
        if (_onceTimer != null)
        {
            ClearTimeout(_onceTimer);
            _onceTimer = null;
        }
        
        if (_intervalTimer != null)
        {
            ClearInterval(_intervalTimer);
            _intervalTimer = null;
        }
        
        await base.CloseAsync(removed);
    }
    
    /// <summary>
    /// Register the inject node type
    /// SOURCE: Line 165 - RED.nodes.registerType("inject",InjectNode)
    /// </summary>
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("inject", config => new InjectNode(config));
    }
}
