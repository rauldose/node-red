// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Nodes;

/// <summary>
/// Base class for all node implementations.
/// Provides common functionality and simplifies node development.
/// </summary>
public abstract class NodeBase : INode
{
    private INodeContext _context = null!;

    /// <inheritdoc />
    public abstract NodeDefinition Definition { get; }

    /// <inheritdoc />
    public FlowNode Config { get; private set; } = null!;

    /// <summary>
    /// Gets the node execution context.
    /// </summary>
    protected INodeContext Context => _context;

    /// <inheritdoc />
    public virtual Task InitializeAsync(FlowNode config, INodeContext context)
    {
        Config = config;
        _context = context;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public abstract Task OnInputAsync(NodeMessage message, int inputPort = 0);

    /// <inheritdoc />
    public virtual Task CloseAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a message to the specified output port.
    /// </summary>
    /// <param name="port">The output port index (0-based).</param>
    /// <param name="message">The message to send.</param>
    protected void Send(int port, NodeMessage message)
    {
        _context.Send(Config.Id, port, message);
    }

    /// <summary>
    /// Sends a message to the first output port.
    /// </summary>
    /// <param name="message">The message to send.</param>
    protected void Send(NodeMessage message)
    {
        Send(0, message);
    }

    /// <summary>
    /// Signals that input processing is complete.
    /// </summary>
    /// <param name="error">Optional error if processing failed.</param>
    protected void Done(Exception? error = null)
    {
        _context.Done(Config.Id, error);
    }

    /// <summary>
    /// Updates the node's status display.
    /// </summary>
    /// <param name="status">The new status.</param>
    protected void SetStatus(NodeStatus status)
    {
        _context.SetStatus(Config.Id, status);
    }

    /// <summary>
    /// Logs a message at the specified level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="level">The log level.</param>
    protected void Log(string message, LogLevel level = LogLevel.Info)
    {
        _context.Log(Config.Id, message, level);
    }

    /// <summary>
    /// Gets a configuration value with a default.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The default value if not found.</param>
    protected T GetConfig<T>(string key, T defaultValue = default!)
    {
        if (Config.Config.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }

        // Try to convert
        if (value != null)
        {
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                // Fall through to default
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Gets the display name of this node instance.
    /// </summary>
    protected string DisplayName => string.IsNullOrEmpty(Config.Name) 
        ? Definition.DisplayName 
        : Config.Name;
}
