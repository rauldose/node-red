// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Core.Interfaces;

/// <summary>
/// Interface for the flow execution runtime.
/// </summary>
public interface IFlowRuntime
{
    /// <summary>
    /// Gets the current state of the runtime.
    /// </summary>
    FlowState State { get; }

    /// <summary>
    /// Loads a workspace into the runtime.
    /// </summary>
    /// <param name="workspace">The workspace to load.</param>
    Task LoadAsync(Workspace workspace);

    /// <summary>
    /// Starts executing all flows.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stops all flow execution.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Restarts all flows (stop then start).
    /// </summary>
    Task RestartAsync();

    /// <summary>
    /// Deploys changes to the flows.
    /// </summary>
    /// <param name="workspace">The updated workspace.</param>
    /// <param name="deployType">Type of deployment (full, flows, nodes).</param>
    Task DeployAsync(Workspace workspace, DeployType deployType = DeployType.Full);

    /// <summary>
    /// Injects a message into a specific node.
    /// </summary>
    /// <param name="nodeId">The target node ID.</param>
    /// <param name="message">The message to inject.</param>
    Task InjectAsync(string nodeId, NodeMessage? message = null);

    /// <summary>
    /// Gets the status of a specific node.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    NodeStatus? GetNodeStatus(string nodeId);

    /// <summary>
    /// Event raised when a node's status changes.
    /// </summary>
    event Action<string, NodeStatus>? OnNodeStatusChanged;

    /// <summary>
    /// Event raised when a debug message is generated.
    /// </summary>
    event Action<DebugMessage>? OnDebugMessage;

    /// <summary>
    /// Event raised when a log entry is created.
    /// </summary>
    event Action<LogEntry>? OnLog;
}

/// <summary>
/// Type of deployment.
/// </summary>
public enum DeployType
{
    /// <summary>
    /// Full deployment - restart all flows.
    /// </summary>
    Full,

    /// <summary>
    /// Flows deployment - restart only changed flows.
    /// </summary>
    Flows,

    /// <summary>
    /// Nodes deployment - restart only changed nodes.
    /// </summary>
    Nodes
}

/// <summary>
/// Represents a debug message from a node.
/// </summary>
public class DebugMessage
{
    /// <summary>
    /// ID of the node that generated the message.
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// Name of the node.
    /// </summary>
    public string NodeName { get; init; } = string.Empty;

    /// <summary>
    /// The debug data.
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// Timestamp of the message.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The message ID that triggered this debug.
    /// </summary>
    public string? MessageId { get; init; }
}

/// <summary>
/// Represents a log entry from the runtime.
/// </summary>
public class LogEntry
{
    /// <summary>
    /// The log level.
    /// </summary>
    public LogLevel Level { get; init; }

    /// <summary>
    /// The log message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The source node ID, if applicable.
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// Timestamp of the log entry.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
