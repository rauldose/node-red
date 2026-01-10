// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Core.Entities;

/// <summary>
/// Represents a subflow definition - a reusable flow template.
/// </summary>
public class Subflow
{
    /// <summary>
    /// Unique identifier for this subflow.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display name of the subflow.
    /// </summary>
    public string Name { get; set; } = "Subflow";

    /// <summary>
    /// Description of what this subflow does.
    /// </summary>
    public string Info { get; set; } = string.Empty;

    /// <summary>
    /// Category in the palette.
    /// </summary>
    public string Category { get; set; } = "subflows";

    /// <summary>
    /// Color for the subflow nodes in the palette.
    /// </summary>
    public string Color { get; set; } = "#DDAA99";

    /// <summary>
    /// Number of input ports (0 or 1).
    /// </summary>
    public int Inputs { get; set; } = 1;

    /// <summary>
    /// Number of output ports.
    /// </summary>
    public int Outputs { get; set; } = 1;

    /// <summary>
    /// Nodes contained in this subflow template.
    /// </summary>
    public List<FlowNode> Nodes { get; set; } = new();

    /// <summary>
    /// Input port definitions for routing messages into the subflow.
    /// </summary>
    public List<SubflowPort> In { get; set; } = new();

    /// <summary>
    /// Output port definitions for routing messages out of the subflow.
    /// </summary>
    public List<SubflowPort> Out { get; set; } = new();

    /// <summary>
    /// Environment variables defined for this subflow.
    /// </summary>
    public List<SubflowEnv> Env { get; set; } = new();

    /// <summary>
    /// Whether this subflow has status output enabled.
    /// </summary>
    public bool Status { get; set; }

    /// <summary>
    /// Status port definition.
    /// </summary>
    public SubflowPort? StatusPort { get; set; }
}

/// <summary>
/// Represents a subflow input/output port definition.
/// </summary>
public class SubflowPort
{
    /// <summary>
    /// X position in the editor.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y position in the editor.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Wires from this port to internal nodes.
    /// </summary>
    public List<SubflowWire> Wires { get; set; } = new();
}

/// <summary>
/// Represents a wire connection in a subflow.
/// </summary>
public class SubflowWire
{
    /// <summary>
    /// Target node ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Target port index.
    /// </summary>
    public int Port { get; set; }
}

/// <summary>
/// Represents an environment variable in a subflow.
/// </summary>
public class SubflowEnv
{
    /// <summary>
    /// Name of the environment variable.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of the value (str, num, bool, json, etc.).
    /// </summary>
    public string Type { get; set; } = "str";

    /// <summary>
    /// Value of the environment variable.
    /// </summary>
    public object? Value { get; set; }
}
