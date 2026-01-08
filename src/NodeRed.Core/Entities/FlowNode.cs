// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Core.Entities;

/// <summary>
/// Represents an instance of a node in a flow.
/// This is a node configuration as stored in a flow file.
/// </summary>
public class FlowNode
{
    /// <summary>
    /// Unique identifier for this node instance.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The type of node (matches NodeDefinition.Type).
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// User-defined display name for this node instance.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// X position in the flow editor.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y position in the flow editor.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Width of the node in the editor.
    /// </summary>
    public double Width { get; set; } = 120;

    /// <summary>
    /// Height of the node in the editor.
    /// </summary>
    public double Height { get; set; } = 30;

    /// <summary>
    /// The ID of the flow/tab this node belongs to.
    /// </summary>
    public string? FlowId { get; set; }

    /// <summary>
    /// Whether this node is disabled.
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Configuration properties specific to this node type.
    /// </summary>
    public Dictionary<string, object?> Config { get; set; } = new();

    /// <summary>
    /// IDs of nodes this node sends output to (wires).
    /// Each inner list represents outputs from a single output port.
    /// </summary>
    public List<List<string>> Wires { get; set; } = new();

    /// <summary>
    /// Creates a copy of this node with a new ID.
    /// </summary>
    public FlowNode Clone()
    {
        return new FlowNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = Type,
            Name = Name,
            X = X + 20,
            Y = Y + 20,
            Width = Width,
            Height = Height,
            FlowId = FlowId,
            Disabled = Disabled,
            Config = new Dictionary<string, object?>(Config),
            Wires = Wires.Select(w => new List<string>(w)).ToList()
        };
    }
}
