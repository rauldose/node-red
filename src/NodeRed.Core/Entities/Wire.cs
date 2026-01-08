// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Core.Entities;

/// <summary>
/// Represents a connection (wire) between two nodes.
/// </summary>
public class Wire
{
    /// <summary>
    /// Unique identifier for this wire.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ID of the source node.
    /// </summary>
    public required string SourceNodeId { get; set; }

    /// <summary>
    /// Output port index on the source node (0-based).
    /// </summary>
    public int SourcePort { get; set; }

    /// <summary>
    /// ID of the target node.
    /// </summary>
    public required string TargetNodeId { get; set; }

    /// <summary>
    /// Input port index on the target node (0-based).
    /// </summary>
    public int TargetPort { get; set; }
}
