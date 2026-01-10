// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;

namespace NodeRed.SDK;

/// <summary>
/// Base class for all custom nodes. Inherit from this class to create a new node type.
/// 
/// Example:
/// <code>
/// [NodeType("my-node", "My Node", Category = NodeCategory.Function)]
/// public class MyNode : NodeBase
/// {
///     protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
///     {
///         msg.Payload = "Hello from my node!";
///         send(0, msg);
///         done();
///         return Task.CompletedTask;
///     }
/// }
/// </code>
/// </summary>
public abstract class NodeBase : INode
{
    private INodeContext? _context;
    private FlowNode? _config;
    private NodeDefinition? _definition;

    /// <summary>
    /// The node's runtime configuration.
    /// </summary>
    public FlowNode Config => _config ?? throw new InvalidOperationException("Node not initialized");

    /// <summary>
    /// The node's type definition.
    /// Can be accessed before initialization for registration purposes.
    /// </summary>
    public NodeDefinition Definition => _definition ??= BuildDefinition();

    /// <summary>
    /// The node's unique ID.
    /// </summary>
    protected string Id => Config.Id;

    /// <summary>
    /// The node's name (user-defined).
    /// </summary>
    protected string Name => Config.Name ?? Config.Type;

    /// <summary>
    /// Access to flow context storage.
    /// </summary>
    protected IContextAccessor Flow => new ContextAccessor(_context!, ContextScope.Flow);

    /// <summary>
    /// Access to global context storage.
    /// </summary>
    protected IContextAccessor Global => new ContextAccessor(_context!, ContextScope.Global);

    /// <summary>
    /// Access to node-level context storage.
    /// </summary>
    protected IContextAccessor Node => new ContextAccessor(_context!, ContextScope.Node);

    /// <summary>
    /// Gets an environment variable value.
    /// </summary>
    /// <param name="name">The environment variable name.</param>
    /// <returns>The environment variable value or null.</returns>
    protected object? GetEnv(string name) => _context?.GetEnv(name);

    /// <summary>
    /// Delegate for sending messages to output ports.
    /// </summary>
    /// <param name="port">Output port index (0-based)</param>
    /// <param name="message">Message to send</param>
    public delegate void SendDelegate(int port, NodeMessage message);

    /// <summary>
    /// Delegate for signaling completion.
    /// </summary>
    /// <param name="error">Optional error if processing failed</param>
    public delegate void DoneDelegate(Exception? error = null);

    /// <summary>
    /// Initializes the node. Override OnInitializeAsync for custom initialization.
    /// </summary>
    public async Task InitializeAsync(FlowNode config, INodeContext context)
    {
        _config = config;
        _context = context;
        _definition = BuildDefinition();

        await OnInitializeAsync();
    }

    /// <summary>
    /// Called when the node receives an input message.
    /// </summary>
    public async Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        void Send(int port, NodeMessage msg) => _context!.Send(Id, port, msg);
        void Done(Exception? error = null) => _context!.Done(Id, error);

        try
        {
            await OnInputAsync(message, Send, Done);
        }
        catch (Exception ex)
        {
            Error(ex.Message, message);
            Done(ex);
        }
    }

    /// <summary>
    /// Called when the node is being closed. Override OnCloseAsync for cleanup.
    /// </summary>
    public async Task CloseAsync()
    {
        await OnCloseAsync();
    }

    #region Override Methods

    /// <summary>
    /// Override to handle incoming messages.
    /// </summary>
    /// <param name="msg">The incoming message</param>
    /// <param name="send">Delegate to send messages to output ports</param>
    /// <param name="done">Delegate to signal completion</param>
    protected virtual Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        done();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Override to perform custom initialization.
    /// </summary>
    protected virtual Task OnInitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Override to perform cleanup when the node is removed or flow is stopped.
    /// </summary>
    protected virtual Task OnCloseAsync() => Task.CompletedTask;

    /// <summary>
    /// Override to define the node's properties, help text, etc.
    /// Default implementation uses NodeTypeAttribute.
    /// </summary>
    protected virtual NodeDefinition BuildDefinition()
    {
        var attr = GetType().GetCustomAttributes(typeof(NodeTypeAttribute), false)
            .FirstOrDefault() as NodeTypeAttribute;

        if (attr == null)
        {
            throw new InvalidOperationException(
                $"Node class {GetType().Name} must have [NodeType] attribute or override BuildDefinition()");
        }

        return new NodeDefinition
        {
            Type = attr.Type,
            DisplayName = attr.DisplayName,
            Category = attr.Category,
            Color = attr.Color,
            Icon = attr.Icon,
            Inputs = attr.Inputs,
            Outputs = attr.Outputs,
            Properties = DefineProperties(),
            Help = DefineHelp(),
            HasButton = attr.HasButton,
            Button = attr.HasButton ? DefineButton() : null,
            Defaults = DefineDefaults()
        };
    }

    /// <summary>
    /// Override to define the node's editable properties.
    /// </summary>
    protected virtual List<NodePropertyDefinition> DefineProperties() => new();

    /// <summary>
    /// Override to define default values for properties.
    /// </summary>
    protected virtual Dictionary<string, object?> DefineDefaults() => new();

    /// <summary>
    /// Override to define help text for the node.
    /// </summary>
    protected virtual NodeHelpText DefineHelp() => new();

    /// <summary>
    /// Override to define button configuration for nodes with buttons.
    /// </summary>
    protected virtual NodeButtonDefinition? DefineButton() => null;

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets a configuration property value.
    /// </summary>
    protected T? GetConfig<T>(string name, T? defaultValue = default)
    {
        if (Config.Config.TryGetValue(name, out var value))
        {
            if (value is T typedValue)
                return typedValue;

            // Try conversion
            try
            {
                return (T)Convert.ChangeType(value, typeof(T))!;
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// Creates a new message.
    /// </summary>
    protected NodeMessage NewMessage(object? payload = null, string? topic = null)
    {
        return new NodeMessage
        {
            Id = Guid.NewGuid().ToString(),
            Payload = payload,
            Topic = topic ?? ""
        };
    }

    /// <summary>
    /// Clones an existing message.
    /// </summary>
    protected NodeMessage CloneMessage(NodeMessage original)
    {
        return new NodeMessage
        {
            Id = Guid.NewGuid().ToString(),
            Payload = original.Payload,
            Topic = original.Topic,
            Properties = new Dictionary<string, object?>(original.Properties)
        };
    }

    /// <summary>
    /// Sets the node's status display.
    /// </summary>
    protected void Status(string text, StatusFill fill = StatusFill.Grey, SdkStatusShape shape = SdkStatusShape.Dot)
    {
        var statusColor = fill switch
        {
            StatusFill.Red => Core.Enums.StatusColor.Red,
            StatusFill.Green => Core.Enums.StatusColor.Green,
            StatusFill.Yellow => Core.Enums.StatusColor.Yellow,
            StatusFill.Blue => Core.Enums.StatusColor.Blue,
            _ => Core.Enums.StatusColor.Grey
        };

        var statusShape = shape switch
        {
            SdkStatusShape.Ring => Core.Enums.StatusShape.Ring,
            _ => Core.Enums.StatusShape.Dot
        };

        _context?.SetStatus(Id, new NodeStatus
        {
            Color = statusColor,
            Shape = statusShape,
            Text = text
        });
    }

    /// <summary>
    /// Clears the node's status display.
    /// </summary>
    protected void ClearStatus()
    {
        _context?.SetStatus(Id, new NodeStatus());
    }

    /// <summary>
    /// Logs an info message.
    /// </summary>
    protected void Log(string message) => _context?.Log(Id, message, LogLevel.Info);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    protected void Warn(string message) => _context?.Log(Id, message, LogLevel.Warning);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    protected void Error(string message, NodeMessage? msg = null)
    {
        _context?.Log(Id, message, LogLevel.Error);
    }

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    protected void Debug(string message) => _context?.Log(Id, message, LogLevel.Debug);

    /// <summary>
    /// Logs a trace message.
    /// </summary>
    protected void Trace(string message) => _context?.Log(Id, message, LogLevel.Trace);

    /// <summary>
    /// Sends a message to the specified output port.
    /// Use this when you need to send messages outside of OnInputAsync (e.g., from callbacks).
    /// </summary>
    /// <param name="port">Output port index (0-based)</param>
    /// <param name="message">Message to send</param>
    protected void Send(int port, NodeMessage message)
    {
        _context?.Send(Id, port, message);
    }

    #endregion
}

/// <summary>
/// Status fill colors (SDK wrapper for Core.Enums.StatusColor).
/// </summary>
public enum StatusFill
{
    Red,
    Green,
    Yellow,
    Blue,
    Grey
}

/// <summary>
/// Status indicator shapes (SDK wrapper for Core.Enums.StatusShape).
/// </summary>
public enum SdkStatusShape
{
    Ring,
    Dot
}

/// <summary>
/// Context scope for storage.
/// </summary>
public enum ContextScope
{
    Flow,
    Global,
    Node
}

/// <summary>
/// Interface for accessing context storage.
/// </summary>
public interface IContextAccessor
{
    T? Get<T>(string key);
    void Set<T>(string key, T value);
    object? Get(string key);
    void Set(string key, object? value);
}

internal class ContextAccessor : IContextAccessor
{
    private readonly INodeContext _context;
    private readonly ContextScope _scope;

    public ContextAccessor(INodeContext context, ContextScope scope)
    {
        _context = context;
        _scope = scope;
    }

    public T? Get<T>(string key) => _scope switch
    {
        ContextScope.Flow => _context.GetFlowContext<T>(key),
        ContextScope.Global => _context.GetGlobalContext<T>(key),
        ContextScope.Node => _context.GetNodeContext<T>(key),
        _ => default
    };

    public void Set<T>(string key, T value)
    {
        switch (_scope)
        {
            case ContextScope.Flow:
                _context.SetFlowContext(key, value);
                break;
            case ContextScope.Global:
                _context.SetGlobalContext(key, value);
                break;
            case ContextScope.Node:
                _context.SetNodeContext(key, value);
                break;
        }
    }

    public object? Get(string key) => Get<object>(key);

    public void Set(string key, object? value) => Set<object?>(key, value);
}
