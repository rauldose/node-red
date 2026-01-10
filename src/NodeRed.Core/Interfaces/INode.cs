// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Core.Interfaces;

/// <summary>
/// Interface for node implementations.
/// All nodes must implement this interface.
/// </summary>
public interface INode
{
    /// <summary>
    /// Gets the node definition for this node type.
    /// </summary>
    NodeDefinition Definition { get; }

    /// <summary>
    /// Gets the flow node configuration.
    /// </summary>
    FlowNode Config { get; }

    /// <summary>
    /// Initializes the node with its configuration.
    /// Called when the node is instantiated.
    /// </summary>
    /// <param name="config">The node configuration.</param>
    /// <param name="context">The node execution context.</param>
    Task InitializeAsync(FlowNode config, INodeContext context);

    /// <summary>
    /// Called when the node receives an input message.
    /// </summary>
    /// <param name="message">The incoming message.</param>
    /// <param name="inputPort">The input port the message arrived on.</param>
    Task OnInputAsync(NodeMessage message, int inputPort = 0);

    /// <summary>
    /// Called when the node is being closed/removed.
    /// Clean up resources here.
    /// </summary>
    Task CloseAsync();
}

/// <summary>
/// Context provided to nodes for accessing runtime services.
/// </summary>
public interface INodeContext
{
    /// <summary>
    /// Sends a message to the specified output port.
    /// </summary>
    /// <param name="nodeId">The source node ID.</param>
    /// <param name="port">The output port index.</param>
    /// <param name="message">The message to send.</param>
    void Send(string nodeId, int port, NodeMessage message);

    /// <summary>
    /// Signals that input processing is complete.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <param name="error">Optional error if processing failed.</param>
    void Done(string nodeId, Exception? error = null);

    /// <summary>
    /// Updates the node's status display.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <param name="status">The new status.</param>
    void SetStatus(string nodeId, NodeStatus status);

    /// <summary>
    /// Logs a message.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="level">The log level.</param>
    void Log(string nodeId, string message, LogLevel level = LogLevel.Info);

    /// <summary>
    /// Gets a value from the flow context.
    /// </summary>
    T? GetFlowContext<T>(string key);

    /// <summary>
    /// Sets a value in the flow context.
    /// </summary>
    void SetFlowContext<T>(string key, T value);

    /// <summary>
    /// Gets a value from the global context.
    /// </summary>
    T? GetGlobalContext<T>(string key);

    /// <summary>
    /// Sets a value in the global context.
    /// </summary>
    void SetGlobalContext<T>(string key, T value);

    /// <summary>
    /// Gets a value from the node-level context.
    /// </summary>
    T? GetNodeContext<T>(string key);

    /// <summary>
    /// Sets a value in the node-level context.
    /// </summary>
    void SetNodeContext<T>(string key, T value);

    /// <summary>
    /// Gets an environment variable value.
    /// </summary>
    object? GetEnv(string name);
}
