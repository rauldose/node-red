// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;

namespace NodeRed.Core.Interfaces;

/// <summary>
/// Interface for flow storage operations.
/// </summary>
public interface IFlowStorage
{
    /// <summary>
    /// Loads a workspace from storage.
    /// </summary>
    /// <param name="workspaceId">The workspace ID to load.</param>
    Task<Workspace?> LoadAsync(string workspaceId);

    /// <summary>
    /// Saves a workspace to storage.
    /// </summary>
    /// <param name="workspace">The workspace to save.</param>
    Task SaveAsync(Workspace workspace);

    /// <summary>
    /// Lists all available workspaces.
    /// </summary>
    Task<IEnumerable<WorkspaceInfo>> ListAsync();

    /// <summary>
    /// Deletes a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace ID to delete.</param>
    Task DeleteAsync(string workspaceId);

    /// <summary>
    /// Exports a workspace to JSON.
    /// </summary>
    /// <param name="workspace">The workspace to export.</param>
    Task<string> ExportAsync(Workspace workspace);

    /// <summary>
    /// Imports a workspace from JSON.
    /// </summary>
    /// <param name="json">The JSON to import.</param>
    Task<Workspace> ImportAsync(string json);
}

/// <summary>
/// Basic information about a workspace.
/// </summary>
public class WorkspaceInfo
{
    /// <summary>
    /// Workspace ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Workspace name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Last modified timestamp.
    /// </summary>
    public DateTimeOffset LastModified { get; init; }

    /// <summary>
    /// Number of flows in the workspace.
    /// </summary>
    public int FlowCount { get; init; }
}
