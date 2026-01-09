// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Enums;

namespace NodeRed.Core.Entities;

/// <summary>
/// Represents a workspace containing multiple flows.
/// This is the top-level container that gets saved/loaded.
/// </summary>
public class Workspace
{
    /// <summary>
    /// Unique identifier for this workspace.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Name of the workspace.
    /// </summary>
    public string Name { get; set; } = "Default Workspace";

    /// <summary>
    /// All flows (tabs) in this workspace.
    /// </summary>
    public List<Flow> Flows { get; set; } = new();

    /// <summary>
    /// Global configuration nodes.
    /// </summary>
    public List<FlowNode> ConfigNodes { get; set; } = new();

    /// <summary>
    /// Current state of the flows.
    /// </summary>
    public FlowState State { get; set; } = FlowState.Stopped;

    /// <summary>
    /// Global context variables.
    /// </summary>
    public Dictionary<string, object?> GlobalContext { get; set; } = new();

    /// <summary>
    /// Version of the workspace format.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Last modified timestamp.
    /// </summary>
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;
}
