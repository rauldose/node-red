// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Enums;

namespace NodeRed.Core.Entities;

/// <summary>
/// Defines the type and configuration of a node.
/// This is the metadata about a node type, not an instance.
/// </summary>
public class NodeDefinition
{
    /// <summary>
    /// Unique identifier for this node type (e.g., "inject", "debug", "function").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Display name for the node type.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Category this node belongs to.
    /// </summary>
    public NodeCategory Category { get; init; } = NodeCategory.Common;

    /// <summary>
    /// Color of the node in the editor (hex color).
    /// </summary>
    public string Color { get; init; } = "#87A980";

    /// <summary>
    /// Icon name for the node.
    /// </summary>
    public string Icon { get; init; } = "node";

    /// <summary>
    /// Number of input ports.
    /// </summary>
    public int Inputs { get; init; } = 1;

    /// <summary>
    /// Number of output ports.
    /// </summary>
    public int Outputs { get; init; } = 1;

    /// <summary>
    /// Labels for output ports.
    /// </summary>
    public string[] OutputLabels { get; init; } = [];

    /// <summary>
    /// Labels for input ports.
    /// </summary>
    public string[] InputLabels { get; init; } = [];

    /// <summary>
    /// Whether this is a configuration node.
    /// </summary>
    public bool IsConfigNode { get; init; }

    /// <summary>
    /// Default configuration values for new instances.
    /// </summary>
    public Dictionary<string, object?> Defaults { get; init; } = new();

    /// <summary>
    /// Help text / documentation for this node.
    /// </summary>
    public string HelpText { get; init; } = string.Empty;
}
