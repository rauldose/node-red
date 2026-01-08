// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Core.Entities;

/// <summary>
/// Represents a flow (tab) containing nodes and their connections.
/// </summary>
public class Flow
{
    /// <summary>
    /// Unique identifier for this flow.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display name for this flow (tab name).
    /// </summary>
    public string Label { get; set; } = "Flow 1";

    /// <summary>
    /// Type identifier (always "tab" for flows).
    /// </summary>
    public string Type { get; set; } = "tab";

    /// <summary>
    /// Whether this flow is disabled.
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Additional information/documentation about the flow.
    /// </summary>
    public string Info { get; set; } = string.Empty;

    /// <summary>
    /// Environment variables for this flow.
    /// </summary>
    public Dictionary<string, object?> Env { get; set; } = new();

    /// <summary>
    /// Nodes contained in this flow.
    /// </summary>
    public List<FlowNode> Nodes { get; set; } = new();

    /// <summary>
    /// Order of this flow in the tab bar.
    /// </summary>
    public int Order { get; set; }
}
