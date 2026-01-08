// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Core.Events;

/// <summary>
/// Base class for all runtime events.
/// </summary>
public abstract class RuntimeEvent
{
    /// <summary>
    /// Unique identifier for this event.
    /// </summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Event raised when a message is sent between nodes.
/// </summary>
public class MessageSentEvent : RuntimeEvent
{
    /// <summary>
    /// The source node ID.
    /// </summary>
    public required string SourceNodeId { get; init; }

    /// <summary>
    /// The target node ID.
    /// </summary>
    public required string TargetNodeId { get; init; }

    /// <summary>
    /// The output port on the source node.
    /// </summary>
    public int SourcePort { get; init; }

    /// <summary>
    /// The message that was sent.
    /// </summary>
    public required NodeMessage Message { get; init; }
}

/// <summary>
/// Event raised when a node's status changes.
/// </summary>
public class NodeStatusChangedEvent : RuntimeEvent
{
    /// <summary>
    /// The node ID.
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// The new status.
    /// </summary>
    public required NodeStatus Status { get; init; }
}

/// <summary>
/// Event raised when a debug message is generated.
/// </summary>
public class DebugEvent : RuntimeEvent
{
    /// <summary>
    /// The node ID that generated the debug.
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// The node name.
    /// </summary>
    public string NodeName { get; init; } = string.Empty;

    /// <summary>
    /// The debug data.
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// The message ID that triggered this debug.
    /// </summary>
    public string? MessageId { get; init; }
}

/// <summary>
/// Event raised when a flow state changes.
/// </summary>
public class FlowStateChangedEvent : RuntimeEvent
{
    /// <summary>
    /// The flow ID.
    /// </summary>
    public required string FlowId { get; init; }

    /// <summary>
    /// The new state.
    /// </summary>
    public required FlowState State { get; init; }

    /// <summary>
    /// Error message if state is Error.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Event raised when a node encounters an error.
/// </summary>
public class NodeErrorEvent : RuntimeEvent
{
    /// <summary>
    /// The node ID.
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// The error that occurred.
    /// </summary>
    public required Exception Error { get; init; }

    /// <summary>
    /// The message that was being processed when the error occurred.
    /// </summary>
    public NodeMessage? Message { get; init; }
}
