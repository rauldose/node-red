// ============================================================
// INSPIRED BY: @node-red/runtime/lib/nodes/Node.js
// REFERENCE: NODE-RED-ARCHITECTURE-ANALYSIS.md - Node Lifecycle section
// ============================================================
// Base node class that all nodes inherit from
// ============================================================

/*!
 * Original work Copyright JS Foundation and other contributors, http://js.foundation
 * Modified work Copyright 2026 NodeRed.NET Contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 */

using Microsoft.Extensions.Logging;
using NodeRed.Util;

namespace NodeRed.Runtime;

/// <summary>
/// Base class for all Node-RED nodes
/// Maps to: Node class in @node-red/runtime/lib/nodes/Node.js
/// Reference: NODE-RED-ARCHITECTURE-ANALYSIS.md - Node Lifecycle
/// </summary>
public abstract class NodeBase
{
    private readonly List<Action<FlowMessage>> _inputHandlers = new();
    private readonly List<Action<bool, Action?>> _closeHandlers = new();
    
    /// <summary>
    /// Unique identifier for this node instance
    /// </summary>
    public string Id { get; protected set; }

    /// <summary>
    /// Node type (e.g., "inject", "debug", "function")
    /// </summary>
    public string Type { get; protected set; }

    /// <summary>
    /// Node name (user-friendly label)
    /// </summary>
    public string? Name { get; protected set; }

    /// <summary>
    /// Configuration for this node
    /// </summary>
    public NodeConfiguration Configuration { get; protected set; }

    /// <summary>
    /// Logger for this node
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Wires defining connections to other nodes
    /// Array of arrays: wires[outputIndex][wireIndex] = nodeId
    /// </summary>
    public string[][] Wires { get; protected set; } = Array.Empty<string[]>();

    /// <summary>
    /// Node context for storing state
    /// </summary>
    public INodeContext Context { get; protected set; }

    /// <summary>
    /// Current node status
    /// </summary>
    public NodeStatus Status { get; private set; } = new NodeStatus();

    protected NodeBase(string id, string type, NodeConfiguration configuration, ILogger logger, INodeContext context)
    {
        Id = id;
        Type = type;
        Configuration = configuration;
        Logger = logger;
        Context = context;
        Name = configuration.Name;
    }

    /// <summary>
    /// Register an input handler
    /// Maps to: this.on('input', handler) in Node-RED
    /// </summary>
    public void OnInput(Action<FlowMessage> handler)
    {
        _inputHandlers.Add(handler);
    }

    /// <summary>
    /// Register a close handler
    /// Maps to: this.on('close', handler) in Node-RED
    /// </summary>
    public void OnClose(Action<bool, Action?> handler)
    {
        _closeHandlers.Add(handler);
    }

    /// <summary>
    /// Send a message to wired nodes
    /// Maps to: this.send(msg) in Node-RED
    /// </summary>
    public virtual void Send(FlowMessage message)
    {
        if (MessageSent != null)
        {
            MessageSent(this, message);
        }
    }

    /// <summary>
    /// Send multiple messages (for nodes with multiple outputs)
    /// Maps to: this.send([msg1, msg2]) in Node-RED
    /// </summary>
    public virtual void Send(FlowMessage?[] messages)
    {
        if (MessagesSent != null)
        {
            MessagesSent(this, messages);
        }
    }

    /// <summary>
    /// Report an error
    /// Maps to: this.error(err, msg) in Node-RED
    /// </summary>
    public virtual void Error(string error, FlowMessage? message = null, Exception? exception = null)
    {
        Logger.LogError(exception, $"[{Type}:{Name ?? Id}] {error}");
        
        if (ErrorOccurred != null)
        {
            ErrorOccurred(this, new NodeError
            {
                Message = error,
                Exception = exception,
                SourceMessage = message
            });
        }
    }

    /// <summary>
    /// Update node status
    /// Maps to: this.status({fill, shape, text}) in Node-RED
    /// </summary>
    public virtual void UpdateStatus(string? fill = null, string? shape = null, string? text = null)
    {
        Status = new NodeStatus
        {
            Fill = fill,
            Shape = shape,
            Text = text
        };

        if (StatusChanged != null)
        {
            StatusChanged(this, Status);
        }
    }

    /// <summary>
    /// Warn with a message
    /// Maps to: this.warn(msg) in Node-RED
    /// </summary>
    public virtual void Warn(string warning)
    {
        Logger.LogWarning($"[{Type}:{Name ?? Id}] {warning}");
    }

    /// <summary>
    /// Log a debug message
    /// Maps to: this.log(msg) in Node-RED
    /// </summary>
    public virtual void Log(string message)
    {
        Logger.LogInformation($"[{Type}:{Name ?? Id}] {message}");
    }

    /// <summary>
    /// Called when the node receives an input message
    /// </summary>
    internal void ReceiveInput(FlowMessage message)
    {
        foreach (var handler in _inputHandlers)
        {
            try
            {
                handler(message);
            }
            catch (Exception ex)
            {
                Error($"Error processing message: {ex.Message}", message, ex);
            }
        }
    }

    /// <summary>
    /// Called when the node is being closed
    /// </summary>
    internal async Task CloseAsync(bool removed)
    {
        foreach (var handler in _closeHandlers)
        {
            try
            {
                var tcs = new TaskCompletionSource();
                handler(removed, () => tcs.SetResult());
                await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error closing node {Id}");
            }
        }
    }

    /// <summary>
    /// Event raised when a message is sent
    /// </summary>
    public event EventHandler<FlowMessage>? MessageSent;

    /// <summary>
    /// Event raised when multiple messages are sent
    /// </summary>
    public event EventHandler<FlowMessage?[]>? MessagesSent;

    /// <summary>
    /// Event raised when an error occurs
    /// </summary>
    public event EventHandler<NodeError>? ErrorOccurred;

    /// <summary>
    /// Event raised when status changes
    /// </summary>
    public event EventHandler<NodeStatus>? StatusChanged;
}

/// <summary>
/// Node configuration base class
/// </summary>
public class NodeConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Z { get; set; } // Flow/tab ID
    public string[][] Wires { get; set; } = Array.Empty<string[]>();
}

/// <summary>
/// Node status structure
/// Maps to: node status in Node-RED
/// </summary>
public class NodeStatus
{
    public string? Fill { get; set; }  // e.g., "red", "green", "yellow", "blue", "grey"
    public string? Shape { get; set; } // e.g., "ring", "dot"
    public string? Text { get; set; }  // Status text
}

/// <summary>
/// Node error structure
/// </summary>
public class NodeError
{
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public FlowMessage? SourceMessage { get; set; }
}

/// <summary>
/// Interface for node context (state storage)
/// Maps to: node.context() in Node-RED
/// Reference: NODE-RED-ARCHITECTURE-ANALYSIS.md - Context Store
/// </summary>
public interface INodeContext
{
    T? Get<T>(string key);
    void Set<T>(string key, T value);
    IEnumerable<string> Keys();
    void Clear();
}

/// <summary>
/// Simple in-memory implementation of node context
/// </summary>
public class InMemoryNodeContext : INodeContext
{
    private readonly Dictionary<string, object?> _store = new();

    public T? Get<T>(string key)
    {
        if (_store.TryGetValue(key, out var value))
        {
            return (T?)value;
        }
        return default;
    }

    public void Set<T>(string key, T value)
    {
        _store[key] = value;
    }

    public IEnumerable<string> Keys() => _store.Keys;

    public void Clear() => _store.Clear();
}
