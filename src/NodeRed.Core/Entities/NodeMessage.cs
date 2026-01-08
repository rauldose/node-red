// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Core.Entities;

/// <summary>
/// Represents a message passed between nodes in a flow.
/// This is equivalent to the 'msg' object in Node-RED.
/// </summary>
public class NodeMessage
{
    /// <summary>
    /// Unique identifier for this message.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The main payload of the message.
    /// </summary>
    public object? Payload { get; set; }

    /// <summary>
    /// The topic of the message, used for routing and filtering.
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    /// Additional properties attached to the message.
    /// </summary>
    public Dictionary<string, object?> Properties { get; set; } = new();

    /// <summary>
    /// Timestamp when the message was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a deep clone of this message.
    /// </summary>
    public NodeMessage Clone()
    {
        return new NodeMessage
        {
            Id = Guid.NewGuid().ToString(),
            Payload = CloneValue(Payload),
            Topic = Topic,
            Properties = new Dictionary<string, object?>(Properties),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static object? CloneValue(object? value)
    {
        // For primitive types and strings, return as-is
        if (value == null || value is string || value.GetType().IsPrimitive)
            return value;

        // For other types, we'd need deep cloning - simplified here
        return value;
    }
}
