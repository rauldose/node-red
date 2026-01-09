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
        // Create a default workspace with sample flow
        var flowId = Guid.NewGuid().ToString();
        var injectNodeId = Guid.NewGuid().ToString();
        var functionNodeId = Guid.NewGuid().ToString();
        var debugNodeId = Guid.NewGuid().ToString();

        var defaultWorkspace = new Workspace
        {
            Id = "default",
            Name = "Default Workspace",
            Flows = new List<Flow>
            {
                new()
                {
                    Id = flowId,
                    Label = "Sample Flow",
                    Order = 0,
                    Nodes = new List<FlowNode>
                    {
                        new()
                        {
                            Id = injectNodeId,
                            Type = "inject",
                            Name = "Timestamp",
                            X = 150,
                            Y = 100,
                            FlowId = flowId,
                            Wires = new List<List<string>> { new() { functionNodeId } },
                            Config = new Dictionary<string, object?>
                            {
                                { "payload", "" },
                                { "payloadType", "date" },
                                { "repeat", "" },
                                { "once", false }
                            }
                        },
                        new()
                        {
                            Id = functionNodeId,
                            Type = "function",
                            Name = "Process",
                            X = 350,
                            Y = 100,
                            FlowId = flowId,
                            Wires = new List<List<string>> { new() { debugNodeId } },
                            Config = new Dictionary<string, object?>
                            {
                                { "func", "msg.Payload = \"Processed: \" + msg.Payload; msg" }
                            }
                        },
                        new()
                        {
                            Id = debugNodeId,
                            Type = "debug",
                            Name = "Output",
                            X = 550,
                            Y = 100,
                            FlowId = flowId,
                            Wires = new List<List<string>>(),
                            Config = new Dictionary<string, object?>
                            {
                                { "active", true },
                                { "tosidebar", true },
                                { "console", false }
                            }
                        }
                    }
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
        // Validate input size to prevent large payload attacks
        const int maxJsonLength = 10 * 1024 * 1024; // 10 MB limit
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON cannot be empty", nameof(json));
        }
        if (json.Length > maxJsonLength)
        {
            throw new ArgumentException($"JSON exceeds maximum allowed size of {maxJsonLength} bytes", nameof(json));
        }

        try
        {
            var workspace = JsonSerializer.Deserialize<Workspace>(json, JsonOptions);
            if (workspace == null)
            {
                throw new ArgumentException("Invalid workspace JSON: deserialization returned null", nameof(json));
            }
            
            // Basic validation of required fields
            if (string.IsNullOrEmpty(workspace.Id))
            {
                workspace.Id = Guid.NewGuid().ToString();
            }
            
            return Task.FromResult(workspace);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid workspace JSON: {ex.Message}", nameof(json), ex);
        }
    }
}
