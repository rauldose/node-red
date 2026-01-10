// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Blazor.Models;

namespace NodeRed.Blazor.Services;

/// <summary>
/// Service for managing editor state (flows, nodes, connectors, groups).
/// This centralizes the state management that was previously in Editor.razor.cs,
/// equivalent to RED.nodes in the JavaScript Node-RED implementation.
/// </summary>
public interface IEditorStateService
{
    /// <summary>
    /// Event raised when the state changes
    /// </summary>
    event Action? OnChange;

    /// <summary>
    /// All flows in the workspace
    /// </summary>
    List<FlowTab> Flows { get; }

    /// <summary>
    /// Current flow ID
    /// </summary>
    string CurrentFlowId { get; set; }

    /// <summary>
    /// All nodes across all flows, keyed by node ID
    /// </summary>
    Dictionary<string, NodeData> AllNodes { get; }

    /// <summary>
    /// All connectors across all flows, keyed by connector ID
    /// </summary>
    Dictionary<string, ConnectorData> AllConnectors { get; }

    /// <summary>
    /// All groups across all flows, keyed by group ID
    /// </summary>
    Dictionary<string, GroupInfo> AllGroups { get; }

    /// <summary>
    /// Whether there are unsaved changes
    /// </summary>
    bool HasUnsavedChanges { get; set; }

    /// <summary>
    /// Node counter for unique IDs
    /// </summary>
    int NodeCount { get; set; }

    /// <summary>
    /// Connector counter for unique IDs
    /// </summary>
    int ConnectorCount { get; set; }

    /// <summary>
    /// Flow counter for unique IDs
    /// </summary>
    int FlowCount { get; set; }

    // Flow operations
    void AddFlow(FlowTab flow);
    void RemoveFlow(string flowId);
    FlowTab? GetFlow(string flowId);
    FlowTab? GetCurrentFlow();

    // Node operations
    void AddNode(NodeData node);
    void RemoveNode(string nodeId);
    NodeData? GetNode(string nodeId);
    void UpdateNodePosition(string nodeId, double x, double y);
    void UpdateNodeProps(string nodeId, string name, Dictionary<string, object?>? props);
    IEnumerable<NodeData> GetNodesForFlow(string flowId);

    // Connector operations
    void AddConnector(ConnectorData connector);
    void RemoveConnector(string connectorId);
    ConnectorData? GetConnector(string connectorId);
    IEnumerable<ConnectorData> GetConnectorsForFlow(string flowId);

    // Group operations
    void AddGroup(GroupInfo group);
    void RemoveGroup(string groupId);
    GroupInfo? GetGroup(string groupId);
    IEnumerable<GroupInfo> GetGroupsForFlow(string flowId);

    // State management
    void Clear();
    void NotifyStateChanged();
}

/// <summary>
/// Implementation of the editor state service
/// </summary>
public class EditorStateService : IEditorStateService
{
    public event Action? OnChange;

    public List<FlowTab> Flows { get; } = new();
    public string CurrentFlowId { get; set; } = "flow1";
    public Dictionary<string, NodeData> AllNodes { get; } = new();
    public Dictionary<string, ConnectorData> AllConnectors { get; } = new();
    public Dictionary<string, GroupInfo> AllGroups { get; } = new();
    public bool HasUnsavedChanges { get; set; }
    public int NodeCount { get; set; }
    public int ConnectorCount { get; set; }
    public int FlowCount { get; set; } = 1;

    public void AddFlow(FlowTab flow)
    {
        Flows.Add(flow);
        FlowCount++;
        HasUnsavedChanges = true;
        NotifyStateChanged();
    }

    public void RemoveFlow(string flowId)
    {
        var flow = Flows.FirstOrDefault(f => f.Id == flowId);
        if (flow != null)
        {
            Flows.Remove(flow);
            
            // Remove all nodes and connectors for this flow
            var nodeIds = AllNodes.Values.Where(n => n.Z == flowId).Select(n => n.Id).ToList();
            foreach (var nodeId in nodeIds)
            {
                AllNodes.Remove(nodeId);
            }
            
            var connectorIds = AllConnectors.Values.Where(c => c.Z == flowId).Select(c => c.Id).ToList();
            foreach (var connectorId in connectorIds)
            {
                AllConnectors.Remove(connectorId);
            }
            
            var groupIds = AllGroups.Values.Where(g => g.FlowId == flowId).Select(g => g.Id).ToList();
            foreach (var groupId in groupIds)
            {
                AllGroups.Remove(groupId);
            }
            
            HasUnsavedChanges = true;
            NotifyStateChanged();
        }
    }

    public FlowTab? GetFlow(string flowId)
    {
        return Flows.FirstOrDefault(f => f.Id == flowId);
    }

    public FlowTab? GetCurrentFlow()
    {
        return GetFlow(CurrentFlowId);
    }

    public void AddNode(NodeData node)
    {
        AllNodes[node.Id] = node;
        NodeCount++;
        HasUnsavedChanges = true;
        NotifyStateChanged();
    }

    public void RemoveNode(string nodeId)
    {
        if (AllNodes.Remove(nodeId))
        {
            HasUnsavedChanges = true;
            NotifyStateChanged();
        }
    }

    public NodeData? GetNode(string nodeId)
    {
        return AllNodes.TryGetValue(nodeId, out var node) ? node : null;
    }

    public void UpdateNodePosition(string nodeId, double x, double y)
    {
        if (AllNodes.TryGetValue(nodeId, out var node))
        {
            node.X = x;
            node.Y = y;
            node.Dirty = true;
            HasUnsavedChanges = true;
        }
    }

    public void UpdateNodeProps(string nodeId, string name, Dictionary<string, object?>? props)
    {
        if (AllNodes.TryGetValue(nodeId, out var node))
        {
            node.Name = name;
            if (props != null)
            {
                foreach (var kvp in props)
                {
                    node.Props[kvp.Key] = kvp.Value;
                }
            }
            node.Changed = true;
            node.Dirty = true;
            HasUnsavedChanges = true;
            NotifyStateChanged();
        }
    }

    public IEnumerable<NodeData> GetNodesForFlow(string flowId)
    {
        return AllNodes.Values.Where(n => n.Z == flowId);
    }

    public void AddConnector(ConnectorData connector)
    {
        AllConnectors[connector.Id] = connector;
        ConnectorCount++;
        HasUnsavedChanges = true;
        NotifyStateChanged();
    }

    public void RemoveConnector(string connectorId)
    {
        if (AllConnectors.Remove(connectorId))
        {
            HasUnsavedChanges = true;
            NotifyStateChanged();
        }
    }

    public ConnectorData? GetConnector(string connectorId)
    {
        return AllConnectors.TryGetValue(connectorId, out var connector) ? connector : null;
    }

    public IEnumerable<ConnectorData> GetConnectorsForFlow(string flowId)
    {
        return AllConnectors.Values.Where(c => c.Z == flowId);
    }

    public void AddGroup(GroupInfo group)
    {
        AllGroups[group.Id] = group;
        HasUnsavedChanges = true;
        NotifyStateChanged();
    }

    public void RemoveGroup(string groupId)
    {
        if (AllGroups.Remove(groupId))
        {
            HasUnsavedChanges = true;
            NotifyStateChanged();
        }
    }

    public GroupInfo? GetGroup(string groupId)
    {
        return AllGroups.TryGetValue(groupId, out var group) ? group : null;
    }

    public IEnumerable<GroupInfo> GetGroupsForFlow(string flowId)
    {
        return AllGroups.Values.Where(g => g.FlowId == flowId);
    }

    public void Clear()
    {
        Flows.Clear();
        AllNodes.Clear();
        AllConnectors.Clear();
        AllGroups.Clear();
        CurrentFlowId = "flow1";
        NodeCount = 0;
        ConnectorCount = 0;
        FlowCount = 1;
        HasUnsavedChanges = false;
        NotifyStateChanged();
    }

    public void NotifyStateChanged()
    {
        OnChange?.Invoke();
    }
}
