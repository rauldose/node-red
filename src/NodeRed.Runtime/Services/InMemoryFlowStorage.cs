// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using NodeRed.Core.Entities;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Services;

/// <summary>
/// In-memory flow storage implementation.
/// For production, implement file-based or database storage.
/// </summary>
public class InMemoryFlowStorage : IFlowStorage
{
    private readonly Dictionary<string, Workspace> _workspaces = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public InMemoryFlowStorage()
    {
        // Create a default workspace
        var defaultWorkspace = new Workspace
        {
            Id = "default",
            Name = "Default Workspace",
            Flows = new List<Flow>
            {
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Label = "Flow 1",
                    Order = 0
                }
            }
        };
        _workspaces[defaultWorkspace.Id] = defaultWorkspace;
    }

    /// <inheritdoc />
    public Task<Workspace?> LoadAsync(string workspaceId)
    {
        return Task.FromResult(_workspaces.GetValueOrDefault(workspaceId));
    }

    /// <inheritdoc />
    public Task SaveAsync(Workspace workspace)
    {
        workspace.LastModified = DateTimeOffset.UtcNow;
        _workspaces[workspace.Id] = workspace;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IEnumerable<WorkspaceInfo>> ListAsync()
    {
        var result = _workspaces.Values.Select(w => new WorkspaceInfo
        {
            Id = w.Id,
            Name = w.Name,
            LastModified = w.LastModified,
            FlowCount = w.Flows.Count
        });
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string workspaceId)
    {
        _workspaces.Remove(workspaceId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string> ExportAsync(Workspace workspace)
    {
        var json = JsonSerializer.Serialize(workspace, JsonOptions);
        return Task.FromResult(json);
    }

    /// <inheritdoc />
    public Task<Workspace> ImportAsync(string json)
    {
        var workspace = JsonSerializer.Deserialize<Workspace>(json, JsonOptions);
        if (workspace == null)
        {
            throw new ArgumentException("Invalid workspace JSON", nameof(json));
        }
        return Task.FromResult(workspace);
    }
}
