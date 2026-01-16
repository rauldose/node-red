// ============================================================
// SOURCE: packages/node_modules/@node-red/runtime/lib/nodes/Node.js
// TRANSLATION: Base node infrastructure for all core nodes
// ============================================================
// This file provides the base Node class that all nodes inherit from,
// translating the Node.js EventEmitter-based pattern to C# async pattern.
// ============================================================

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using NodeRed.Util;

namespace NodeRed.Nodes.Core;

/// <summary>
/// Flow message structure matching Node-RED msg object
/// SOURCE: Node-RED message object structure
/// </summary>
public class FlowMessage
{
    [JsonPropertyName("_msgid")]
    public string MsgId { get; set; } = global::NodeRed.Util.Util.GenerateId();
    
    [JsonPropertyName("payload")]
    public object? Payload { get; set; }
    
    [JsonPropertyName("topic")]
    public string? Topic { get; set; }
    
    [JsonPropertyName("parts")]
    public MessageParts? Parts { get; set; }
    
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
    
    /// <summary>
    /// Clone the message (equivalent to RED.util.cloneMessage)
    /// </summary>
    public FlowMessage Clone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<FlowMessage>(json) ?? new FlowMessage();
    }
}

/// <summary>
/// Message parts for sequence handling (split/join nodes)
/// SOURCE: msg.parts structure in Node-RED
/// </summary>
public class MessageParts
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("index")]
    public int Index { get; set; }
    
    [JsonPropertyName("count")]
    public int? Count { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("ch")]
    public string? Ch { get; set; }
    
    [JsonPropertyName("key")]
    public string? Key { get; set; }
}

/// <summary>
/// Node status structure
/// SOURCE: node.status() in Node-RED
/// </summary>
public class NodeStatus
{
    [JsonPropertyName("fill")]
    public string Fill { get; set; } = "";
    
    [JsonPropertyName("shape")]
    public string Shape { get; set; } = "";
    
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

/// <summary>
/// Node configuration base class
/// SOURCE: n object passed to node constructor
/// </summary>
public class NodeConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("z")]
    public string? Z { get; set; }
    
    [JsonPropertyName("wires")]
    public List<List<string>>? Wires { get; set; }
    
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Properties { get; set; }
    
    /// <summary>
    /// Get a property value by name
    /// </summary>
    public T? GetProperty<T>(string name, T? defaultValue = default)
    {
        if (Properties == null || !Properties.TryGetValue(name, out var element))
            return defaultValue;
        
        try
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        catch
        {
            return defaultValue;
        }
    }
}

/// <summary>
/// Input handler delegate for nodes
/// SOURCE: this.on("input", function(msg, send, done))
/// </summary>
public delegate Task NodeInputHandler(FlowMessage msg, Action<object?> send, Action<Exception?> done);

/// <summary>
/// Close handler delegate for nodes
/// SOURCE: this.on("close", function(removed, done))
/// </summary>
public delegate Task NodeCloseHandler(bool removed);

/// <summary>
/// Base class for all nodes - translates Node-RED's Node prototype
/// SOURCE: packages/node_modules/@node-red/runtime/lib/nodes/Node.js
/// </summary>
public abstract class BaseNode : IDisposable
{
    private readonly ConcurrentDictionary<string, List<Delegate>> _eventHandlers = new();
    private readonly List<Timer> _outstandingTimers = new();
    private readonly List<Timer> _outstandingIntervals = new();
    private bool _disposed;
    
    /// <summary>
    /// Node configuration
    /// </summary>
    protected NodeConfig Config { get; }
    
    /// <summary>
    /// Node ID (equivalent to this.id in Node-RED)
    /// </summary>
    public string Id => Config.Id;
    
    /// <summary>
    /// Node type (equivalent to this.type in Node-RED)
    /// </summary>
    public string Type => Config.Type;
    
    /// <summary>
    /// Node name (equivalent to this.name in Node-RED)
    /// </summary>
    public string? Name => Config.Name;
    
    /// <summary>
    /// Flow ID this node belongs to (equivalent to this.z in Node-RED)
    /// </summary>
    public string? Z => Config.Z;
    
    /// <summary>
    /// Wire configuration
    /// </summary>
    public List<List<string>>? Wires => Config.Wires;
    
    /// <summary>
    /// Current node status
    /// </summary>
    public NodeStatus? CurrentStatus { get; private set; }
    
    /// <summary>
    /// Context storage for node (equivalent to this.context() in Node-RED)
    /// </summary>
    public ConcurrentDictionary<string, object?> Context { get; } = new();
    
    /// <summary>
    /// Flow context (equivalent to context.flow in Node-RED)
    /// </summary>
    public static ConcurrentDictionary<string, object?> FlowContext { get; } = new();
    
    /// <summary>
    /// Global context (equivalent to context.global in Node-RED)
    /// </summary>
    public static ConcurrentDictionary<string, object?> GlobalContext { get; } = new();
    
    /// <summary>
    /// Event raised when status changes
    /// </summary>
    public event EventHandler<NodeStatus>? StatusChanged;
    
    /// <summary>
    /// Event raised when node sends a message
    /// </summary>
    public event EventHandler<object?>? MessageSent;
    
    /// <summary>
    /// Constructor - equivalent to RED.nodes.createNode(this, n)
    /// SOURCE: Node.js - function Node(n) { ... }
    /// </summary>
    protected BaseNode(NodeConfig config)
    {
        Config = config;
    }
    
    /// <summary>
    /// Log a message (equivalent to this.log in Node-RED)
    /// SOURCE: Node.prototype.log
    /// </summary>
    public void Log(string message)
    {
        Util.Log.Info($"[{Type}:{Id}] {message}");
    }
    
    /// <summary>
    /// Log a warning (equivalent to this.warn in Node-RED)
    /// SOURCE: Node.prototype.warn
    /// </summary>
    public void Warn(string message)
    {
        Util.Log.Warn($"[{Type}:{Id}] {message}");
    }
    
    /// <summary>
    /// Log an error (equivalent to this.error in Node-RED)
    /// SOURCE: Node.prototype.error
    /// </summary>
    public void Error(string message, FlowMessage? msg = null)
    {
        Util.Log.Error($"[{Type}:{Id}] {message}");
        // In Node-RED, errors with a msg can trigger catch nodes
        if (msg != null)
        {
            Emit("error", new { message, msg });
        }
    }
    
    /// <summary>
    /// Log a debug message (equivalent to this.debug in Node-RED)
    /// SOURCE: Node.prototype.debug
    /// </summary>
    public void Debug(string message)
    {
        Util.Log.Debug($"[{Type}:{Id}] {message}");
    }
    
    /// <summary>
    /// Log a trace message (equivalent to this.trace in Node-RED)
    /// SOURCE: Node.prototype.trace
    /// </summary>
    public void Trace(string message)
    {
        Util.Log.Trace($"[{Type}:{Id}] {message}");
    }
    
    /// <summary>
    /// Set node status (equivalent to this.status in Node-RED)
    /// SOURCE: Node.prototype.status
    /// </summary>
    public void Status(NodeStatus? status)
    {
        CurrentStatus = status ?? new NodeStatus();
        StatusChanged?.Invoke(this, CurrentStatus);
    }
    
    /// <summary>
    /// Set node status with fill, shape, and text
    /// </summary>
    public void Status(string fill, string shape, string text)
    {
        Status(new NodeStatus { Fill = fill, Shape = shape, Text = text });
    }
    
    /// <summary>
    /// Send a message to outputs (equivalent to node.send in Node-RED)
    /// SOURCE: Node.prototype.send
    /// </summary>
    public void Send(object? msg)
    {
        MessageSent?.Invoke(this, msg);
    }
    
    /// <summary>
    /// Receive a message (equivalent to node.receive in Node-RED)
    /// SOURCE: Node.prototype.receive
    /// </summary>
    public async Task ReceiveAsync(FlowMessage? msg = null)
    {
        msg ??= new FlowMessage();
        await EmitAsync("input", msg);
    }
    
    /// <summary>
    /// Register event handler (equivalent to this.on in Node-RED)
    /// SOURCE: Node.js EventEmitter pattern
    /// </summary>
    public void On(string eventName, Delegate handler)
    {
        _eventHandlers.AddOrUpdate(
            eventName,
            _ => new List<Delegate> { handler },
            (_, handlers) => { handlers.Add(handler); return handlers; }
        );
    }
    
    /// <summary>
    /// Register input handler
    /// </summary>
    public void OnInput(Func<FlowMessage, Action<object?>, Action<Exception?>, Task> handler)
    {
        On("input", handler);
    }
    
    /// <summary>
    /// Register close handler
    /// </summary>
    public void OnClose(Func<bool, Task> handler)
    {
        On("close", handler);
    }
    
    /// <summary>
    /// Emit an event (equivalent to this.emit in Node-RED)
    /// SOURCE: Node.js EventEmitter pattern
    /// </summary>
    public void Emit(string eventName, object? data = null)
    {
        if (_eventHandlers.TryGetValue(eventName, out var handlers))
        {
            foreach (var handler in handlers.ToList())
            {
                try
                {
                    handler.DynamicInvoke(data);
                }
                catch (Exception ex)
                {
                    Error($"Error in {eventName} handler: {ex.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// Emit an event asynchronously (for input handlers)
    /// SOURCE: Node.js EventEmitter pattern with async support
    /// </summary>
    public async Task EmitAsync(string eventName, FlowMessage msg)
    {
        if (_eventHandlers.TryGetValue(eventName, out var handlers))
        {
            foreach (var handler in handlers.ToList())
            {
                try
                {
                    if (handler is Func<FlowMessage, Action<object?>, Action<Exception?>, Task> inputHandler)
                    {
                        Exception? error = null;
                        var sendCalled = false;
                        
                        Action<object?> send = (output) =>
                        {
                            sendCalled = true;
                            Send(output);
                        };
                        
                        Action<Exception?> done = (ex) =>
                        {
                            error = ex;
                        };
                        
                        await inputHandler(msg, send, done);
                        
                        if (error != null)
                        {
                            Error(error.Message, msg);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Error($"Error in {eventName} handler: {ex.Message}", msg);
                }
            }
        }
    }
    
    /// <summary>
    /// Create a setTimeout equivalent
    /// SOURCE: sandbox.setTimeout in function node
    /// </summary>
    protected Timer SetTimeout(Action callback, int delayMs)
    {
        Timer? timer = null;
        timer = new Timer(_ =>
        {
            _outstandingTimers.Remove(timer!);
            callback();
        }, null, delayMs, Timeout.Infinite);
        
        _outstandingTimers.Add(timer);
        return timer;
    }
    
    /// <summary>
    /// Create a setInterval equivalent
    /// SOURCE: sandbox.setInterval in function node
    /// </summary>
    protected Timer SetInterval(Action callback, int intervalMs)
    {
        var timer = new Timer(_ => callback(), null, intervalMs, intervalMs);
        _outstandingIntervals.Add(timer);
        return timer;
    }
    
    /// <summary>
    /// Clear a timeout
    /// SOURCE: sandbox.clearTimeout in function node
    /// </summary>
    protected void ClearTimeout(Timer timer)
    {
        timer.Dispose();
        _outstandingTimers.Remove(timer);
    }
    
    /// <summary>
    /// Clear an interval
    /// SOURCE: sandbox.clearInterval in function node
    /// </summary>
    protected void ClearInterval(Timer timer)
    {
        timer.Dispose();
        _outstandingIntervals.Remove(timer);
    }
    
    /// <summary>
    /// Close the node (equivalent to Node.prototype.close)
    /// SOURCE: Node.prototype.close
    /// </summary>
    public virtual async Task CloseAsync(bool removed = false)
    {
        // Emit close event
        if (_eventHandlers.TryGetValue("close", out var handlers))
        {
            foreach (var handler in handlers.ToList())
            {
                try
                {
                    if (handler is Func<bool, Task> closeHandler)
                    {
                        await closeHandler(removed);
                    }
                    else if (handler is Action closeAction)
                    {
                        closeAction();
                    }
                }
                catch (Exception ex)
                {
                    Error($"Error in close handler: {ex.Message}");
                }
            }
        }
        
        // Clear outstanding timers
        foreach (var timer in _outstandingTimers.ToList())
        {
            timer.Dispose();
        }
        _outstandingTimers.Clear();
        
        // Clear outstanding intervals
        foreach (var timer in _outstandingIntervals.ToList())
        {
            timer.Dispose();
        }
        _outstandingIntervals.Clear();
    }
    
    /// <summary>
    /// Dispose the node
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        
        if (disposing)
        {
            CloseAsync(true).Wait();
        }
        
        _disposed = true;
    }
}

/// <summary>
/// Node type registry for registering node types
/// SOURCE: RED.nodes.registerType
/// </summary>
public static class NodeTypeRegistry
{
    private static readonly ConcurrentDictionary<string, Type> _nodeTypes = new();
    private static readonly ConcurrentDictionary<string, Func<NodeConfig, BaseNode>> _nodeFactories = new();
    
    /// <summary>
    /// Register a node type (equivalent to RED.nodes.registerType)
    /// SOURCE: RED.nodes.registerType("inject", InjectNode)
    /// </summary>
    public static void RegisterType<T>(string typeName) where T : BaseNode
    {
        _nodeTypes[typeName] = typeof(T);
    }
    
    /// <summary>
    /// Register a node type with factory
    /// </summary>
    public static void RegisterType(string typeName, Func<NodeConfig, BaseNode> factory)
    {
        _nodeFactories[typeName] = factory;
    }
    
    /// <summary>
    /// Get a registered node type
    /// </summary>
    public static Type? GetType(string typeName)
    {
        return _nodeTypes.TryGetValue(typeName, out var type) ? type : null;
    }
    
    /// <summary>
    /// Create a node instance
    /// </summary>
    public static BaseNode? CreateNode(string typeName, NodeConfig config)
    {
        if (_nodeFactories.TryGetValue(typeName, out var factory))
        {
            return factory(config);
        }
        
        if (_nodeTypes.TryGetValue(typeName, out var type))
        {
            return Activator.CreateInstance(type, config) as BaseNode;
        }
        
        return null;
    }
    
    /// <summary>
    /// Get all registered node types
    /// </summary>
    public static IEnumerable<string> GetAllTypes()
    {
        return _nodeTypes.Keys.Concat(_nodeFactories.Keys).Distinct();
    }
}
