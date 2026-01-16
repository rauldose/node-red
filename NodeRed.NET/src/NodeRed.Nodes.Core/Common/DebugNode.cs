// ============================================================
// SOURCE: packages/node_modules/@node-red/nodes/core/common/21-debug.js
// TRANSLATION: Debug node - displays messages in sidebar/console
// ============================================================
// ORIGINAL CODE (key sections):
// ------------------------------------------------------------
// function DebugNode(n) {
//     RED.nodes.createNode(this,n);
//     this.name = n.name;
//     this.complete = n.complete || "payload";
//     this.console = n.console || false;
//     this.tostatus = n.tostatus || false;
//     this.tosidebar = n.tosidebar;
//     this.active = n.active;
//     ...
//     this.on("input", function(msg, send, done) { ... });
// }
// RED.nodes.registerType("debug", DebugNode);
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using NodeRed.Util;

namespace NodeRed.Nodes.Core.Common;

/// <summary>
/// Debug node configuration
/// SOURCE: DebugNode constructor parameters
/// </summary>
public class DebugNodeConfig : NodeConfig
{
    [JsonPropertyName("complete")]
    public string Complete { get; set; } = "payload";
    
    [JsonPropertyName("targetType")]
    public string? TargetType { get; set; }
    
    [JsonPropertyName("console")]
    public string Console { get; set; } = "false";
    
    [JsonPropertyName("tostatus")]
    public bool ToStatus { get; set; }
    
    [JsonPropertyName("statusType")]
    public string StatusType { get; set; } = "auto";
    
    [JsonPropertyName("statusVal")]
    public string? StatusVal { get; set; }
    
    [JsonPropertyName("tosidebar")]
    public bool ToSidebar { get; set; } = true;
    
    [JsonPropertyName("active")]
    public bool? Active { get; set; }
}

/// <summary>
/// Debug message structure for sidebar
/// SOURCE: sendDebug() function
/// </summary>
public class DebugMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    
    [JsonPropertyName("z")]
    public string? Z { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("topic")]
    public string? Topic { get; set; }
    
    [JsonPropertyName("property")]
    public string? Property { get; set; }
    
    [JsonPropertyName("msg")]
    public object? Msg { get; set; }
    
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

/// <summary>
/// Debug node - Displays messages in the debug sidebar and/or console
/// SOURCE: packages/node_modules/@node-red/nodes/core/common/21-debug.js
/// 
/// MAPPING NOTES:
/// - DebugNode(n) → DebugNode(NodeConfig config)
/// - this.complete → _complete
/// - this.console → _console
/// - this.tosidebar → _toSidebar
/// - this.active → _active
/// - sendDebug() → SendDebug()
/// - util.inspect() → JsonSerializer.Serialize()
/// </summary>
public class DebugNode : BaseNode
{
    private readonly string _complete;
    private readonly bool _hasEditExpression;
    private readonly bool _console;
    private readonly bool _toStatus;
    private readonly string _statusType;
    private readonly string? _statusVal;
    private readonly bool _toSidebar;
    private bool _active;
    private int _counter;
    private long _lastTime;
    private readonly int _debugMaxLength;
    private readonly int _statusLength;
    
    /// <summary>
    /// Event raised when a debug message is generated
    /// </summary>
    public static event EventHandler<DebugMessage>? DebugMessageReceived;
    
    /// <summary>
    /// Constructor - equivalent to function DebugNode(n)
    /// SOURCE: Lines 13-65 of 21-debug.js
    /// </summary>
    public DebugNode(NodeConfig config) : base(config)
    {
        var debugConfig = config as DebugNodeConfig ?? new DebugNodeConfig();
        
        // SOURCE: Lines 14-29
        _hasEditExpression = debugConfig.TargetType == "jsonata";
        _complete = _hasEditExpression ? "jsonata" : (debugConfig.Complete ?? "payload");
        if (_complete == "false") _complete = "payload";
        
        _console = debugConfig.Console == "true";
        _toStatus = debugConfig.ToStatus;
        _statusType = debugConfig.StatusType ?? "auto";
        _statusVal = debugConfig.StatusVal ?? _complete;
        _toSidebar = debugConfig.ToSidebar;
        _active = debugConfig.Active ?? true;
        
        _counter = 0;
        _lastTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        // Default settings
        // SOURCE: Lines 7-8 - var debuglength = RED.settings.debugMaxLength || 1000
        _debugMaxLength = 1000;
        _statusLength = 32;
        
        // Set initial status
        // SOURCE: Lines 30-44
        if (_toStatus)
        {
            Status("grey", "ring", "");
        }
        else if (_statusType == "counter")
        {
            Status("blue", "ring", _counter.ToString());
        }
        
        // Register input handler
        // SOURCE: Lines 123-213 - this.on("input", function(msg, send, done))
        OnInput(async (msg, send, done) =>
        {
            try
            {
                await ProcessMessage(msg);
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    /// <summary>
    /// Process an incoming message
    /// SOURCE: Lines 123-213 - input handler
    /// </summary>
    private async Task ProcessMessage(FlowMessage msg)
    {
        if (!_active)
            return;
        
        // Handle status updates
        // SOURCE: Lines 128-177
        if (_toStatus)
        {
            if (_statusType == "counter")
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var diff = now - _lastTime;
                _lastTime = now;
                _counter++;
                
                if (diff > 100)
                {
                    Status("blue", "ring", _counter.ToString());
                }
            }
            else
            {
                var statusValue = PrepareStatus(msg);
                var text = FormatStatusText(statusValue);
                if (text.Length > _statusLength)
                {
                    text = text.Substring(0, _statusLength) + "...";
                }
                Status("grey", "dot", text);
            }
        }
        
        // Prepare and send debug message
        // SOURCE: Lines 179-212
        if (_complete == "true")
        {
            // Debug complete message
            if (_console)
            {
                Log("\n" + JsonSerializer.Serialize(msg, new JsonSerializerOptions { WriteIndented = true }));
            }
            
            if (_toSidebar)
            {
                SendDebug(new DebugMessage
                {
                    Id = Id,
                    Z = Z,
                    Name = Name,
                    Topic = msg.Topic,
                    Msg = msg
                });
            }
        }
        else
        {
            // Debug specific property
            var debugMsg = PrepareValue(msg);
            
            if (_console)
            {
                var output = debugMsg.Msg;
                if (output is string str)
                {
                    Log(str.Contains('\n') ? "\n" + str : str);
                }
                else
                {
                    Log("\n" + JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            
            if (_toSidebar)
            {
                SendDebug(debugMsg);
            }
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Prepare the value to debug
    /// SOURCE: Lines 66-84 - function prepareValue(msg, done)
    /// </summary>
    private DebugMessage PrepareValue(FlowMessage msg)
    {
        var property = "payload";
        object? output = msg.Payload;
        
        if (_complete != "false" && _complete != "payload")
        {
            property = _complete;
            output = GetMessageProperty(msg, _complete);
        }
        
        return new DebugMessage
        {
            Id = Id,
            Z = Z,
            Name = Name,
            Topic = msg.Topic,
            Property = property,
            Msg = output
        };
    }
    
    /// <summary>
    /// Prepare status value
    /// SOURCE: Lines 86-114 - function prepareStatus(msg, done)
    /// </summary>
    private object? PrepareStatus(FlowMessage msg)
    {
        if (_statusType == "auto")
        {
            return _complete == "true" ? msg.Payload : PrepareValue(msg).Msg;
        }
        else
        {
            return GetMessageProperty(msg, _statusVal ?? "payload");
        }
    }
    
    /// <summary>
    /// Format value for status display
    /// </summary>
    private string FormatStatusText(object? value)
    {
        if (value == null) return "";
        if (value is string str) return str;
        return JsonSerializer.Serialize(value);
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
    /// Send debug message to sidebar
    /// SOURCE: Lines 227-232 - function sendDebug(msg)
    /// </summary>
    private void SendDebug(DebugMessage debugMsg)
    {
        // Truncate message if too long
        var json = JsonSerializer.Serialize(debugMsg);
        if (json.Length > _debugMaxLength)
        {
            // Truncate the message content
            debugMsg.Msg = json.Substring(0, _debugMaxLength) + "...";
        }
        
        // Emit to subscribers (e.g., SignalR hub for sidebar)
        DebugMessageReceived?.Invoke(this, debugMsg);
        
        // Also emit through events system
        Events.Instance.Emit("debug", debugMsg);
    }
    
    /// <summary>
    /// Set the active state
    /// SOURCE: Lines 242-248 - function setNodeState(node, state)
    /// </summary>
    public void SetActive(bool active)
    {
        _active = active;
    }
    
    /// <summary>
    /// Get the active state
    /// </summary>
    public bool IsActive => _active;
    
    /// <summary>
    /// Register the debug node type
    /// SOURCE: Lines 216-225 - RED.nodes.registerType("debug", DebugNode)
    /// </summary>
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("debug", config => new DebugNode(config));
    }
}
