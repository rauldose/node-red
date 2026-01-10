// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Events;
using NodeRed.Core.Exceptions;
using NodeRed.Core.Interfaces;
using NodeRed.Runtime.Nodes.SDK.Common;

namespace NodeRed.Runtime.Execution;

/// <summary>
/// Main flow runtime that manages flow execution.
/// </summary>
public class FlowRuntime : IFlowRuntime
{
    private readonly INodeRegistry _nodeRegistry;
    private readonly Dictionary<string, FlowExecutor> _executors = new();
    private readonly Dictionary<string, object?> _globalContext = new();
    private readonly Dictionary<string, INode> _linkInNodes = new(); // Link node registry for cross-flow routing
    private readonly Dictionary<string, TaskCompletionSource<NodeMessage>> _pendingLinkCalls = new(); // For link call return mode
    private readonly object _deployLock = new(); // Lock for deployment operations
    private Workspace? _workspace;
    private Workspace? _previousWorkspace; // For incremental deployment

    /// <inheritdoc />
    public FlowState State { get; private set; } = FlowState.Stopped;

    /// <inheritdoc />
    public event Action<string, NodeStatus>? OnNodeStatusChanged;

    /// <inheritdoc />
    public event Action<DebugMessage>? OnDebugMessage;

    /// <inheritdoc />
    public event Action<LogEntry>? OnLog;

    public FlowRuntime(INodeRegistry nodeRegistry)
    {
        _nodeRegistry = nodeRegistry;
        
        // Subscribe to DebugNode static events to forward them to the OnDebugMessage event
        DebugNode.OnDebug += HandleDebugNodeEvent;
    }

    private void HandleDebugNodeEvent(DebugEvent evt)
    {
        OnDebugMessage?.Invoke(new DebugMessage
        {
            NodeId = evt.NodeId,
            NodeName = evt.NodeName,
            Data = evt.Data,
            Timestamp = evt.Timestamp,
            MessageId = evt.MessageId
        });
    }

    /// <inheritdoc />
    public async Task LoadAsync(Workspace workspace)
    {
        if (State == FlowState.Running)
        {
            await StopAsync();
        }

        _previousWorkspace = _workspace;
        _workspace = workspace;
        _executors.Clear();
        _linkInNodes.Clear();

        foreach (var flow in workspace.Flows)
        {
            if (flow.Disabled) continue;

            var executor = new FlowExecutor(flow, _nodeRegistry, _globalContext, workspace.Subflows);
            executor.SetRuntime(this);
            executor.OnNodeStatusChanged += (nodeId, status) => OnNodeStatusChanged?.Invoke(nodeId, status);
            executor.OnDebugMessage += msg => OnDebugMessage?.Invoke(msg);
            executor.OnLog += entry => OnLog?.Invoke(entry);
            _executors[flow.Id] = executor;
        }
    }

    /// <inheritdoc />
    public async Task StartAsync()
    {
        if (_workspace == null)
        {
            throw new InvalidOperationException("No workspace loaded. Call LoadAsync first.");
        }

        State = FlowState.Starting;

        try
        {
            foreach (var executor in _executors.Values)
            {
                await executor.InitializeAsync();
            }

            // Register link in nodes for cross-flow routing
            RegisterLinkInNodes();

            foreach (var executor in _executors.Values)
            {
                await executor.StartAsync();
            }

            State = FlowState.Running;
            OnLog?.Invoke(new LogEntry
            {
                Level = LogLevel.Info,
                Message = "Flows started successfully"
            });
        }
        catch (Exception ex)
        {
            State = FlowState.Error;
            OnLog?.Invoke(new LogEntry
            {
                Level = LogLevel.Error,
                Message = $"Failed to start flows: {ex.Message}"
            });
            throw;
        }
    }

    /// <summary>
    /// Registers all link in nodes for cross-flow routing.
    /// </summary>
    private void RegisterLinkInNodes()
    {
        _linkInNodes.Clear();
        foreach (var executor in _executors.Values)
        {
            if (_workspace == null) continue;

            var flow = _workspace.Flows.FirstOrDefault(f => f.Id == executor.FlowId);
            if (flow == null) continue;

            foreach (var nodeConfig in flow.Nodes)
            {
                if (nodeConfig.Type == "link in")
                {
                    var node = executor.GetNode(nodeConfig.Id);
                    if (node != null)
                    {
                        _linkInNodes[nodeConfig.Id] = node;
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        State = FlowState.Stopping;

        foreach (var executor in _executors.Values)
        {
            await executor.StopAsync();
        }

        _linkInNodes.Clear();

        State = FlowState.Stopped;
        OnLog?.Invoke(new LogEntry
        {
            Level = LogLevel.Info,
            Message = "Flows stopped"
        });
    }

    /// <inheritdoc />
    public async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }

    /// <inheritdoc />
    public async Task DeployAsync(Workspace workspace, DeployType deployType = DeployType.Full)
    {
        switch (deployType)
        {
            case DeployType.Full:
                await StopAsync();
                await LoadAsync(workspace);
                await StartAsync();
                break;

            case DeployType.Flows:
                await DeployFlowsAsync(workspace);
                break;

            case DeployType.Nodes:
                await DeployNodesAsync(workspace);
                break;
        }
    }

    /// <summary>
    /// Deploys only changed flows (incremental deployment).
    /// </summary>
    private async Task DeployFlowsAsync(Workspace workspace)
    {
        if (_previousWorkspace == null)
        {
            // No previous workspace - do full deploy
            await DeployAsync(workspace, DeployType.Full);
            return;
        }

        _workspace = workspace;

        // Find changed, added, and removed flows
        var previousFlowIds = new HashSet<string>(_previousWorkspace.Flows.Select(f => f.Id));
        var currentFlowIds = new HashSet<string>(workspace.Flows.Select(f => f.Id));

        // Remove flows that no longer exist
        var removedFlowIds = previousFlowIds.Except(currentFlowIds).ToList();
        foreach (var flowId in removedFlowIds)
        {
            if (_executors.TryGetValue(flowId, out var executor))
            {
                await executor.StopAsync();
                _executors.Remove(flowId);
                OnLog?.Invoke(new LogEntry
                {
                    Level = LogLevel.Info,
                    Message = $"Flow removed: {flowId}"
                });
            }
        }

        // Add new flows
        var addedFlowIds = currentFlowIds.Except(previousFlowIds).ToList();
        foreach (var flowId in addedFlowIds)
        {
            var flow = workspace.Flows.FirstOrDefault(f => f.Id == flowId);
            if (flow != null && !flow.Disabled)
            {
                var executor = new FlowExecutor(flow, _nodeRegistry, _globalContext, workspace.Subflows);
                executor.SetRuntime(this);
                executor.OnNodeStatusChanged += (nodeId, status) => OnNodeStatusChanged?.Invoke(nodeId, status);
                executor.OnDebugMessage += msg => OnDebugMessage?.Invoke(msg);
                executor.OnLog += entry => OnLog?.Invoke(entry);
                await executor.InitializeAsync();
                await executor.StartAsync();
                _executors[flow.Id] = executor;
                OnLog?.Invoke(new LogEntry
                {
                    Level = LogLevel.Info,
                    Message = $"Flow added: {flow.Label}"
                });
            }
        }

        // Restart changed flows
        var existingFlowIds = previousFlowIds.Intersect(currentFlowIds).ToList();
        foreach (var flowId in existingFlowIds)
        {
            var previousFlow = _previousWorkspace.Flows.FirstOrDefault(f => f.Id == flowId);
            var currentFlow = workspace.Flows.FirstOrDefault(f => f.Id == flowId);

            if (previousFlow != null && currentFlow != null && HasFlowChanged(previousFlow, currentFlow))
            {
                // Stop and restart the changed flow
                if (_executors.TryGetValue(flowId, out var executor))
                {
                    await executor.StopAsync();
                }

                if (!currentFlow.Disabled)
                {
                    var newExecutor = new FlowExecutor(currentFlow, _nodeRegistry, _globalContext, workspace.Subflows);
                    newExecutor.SetRuntime(this);
                    newExecutor.OnNodeStatusChanged += (nodeId, status) => OnNodeStatusChanged?.Invoke(nodeId, status);
                    newExecutor.OnDebugMessage += msg => OnDebugMessage?.Invoke(msg);
                    newExecutor.OnLog += entry => OnLog?.Invoke(entry);
                    await newExecutor.InitializeAsync();
                    await newExecutor.StartAsync();
                    _executors[flowId] = newExecutor;
                    OnLog?.Invoke(new LogEntry
                    {
                        Level = LogLevel.Info,
                        Message = $"Flow updated: {currentFlow.Label}"
                    });
                }
            }
        }

        _previousWorkspace = workspace;
        RegisterLinkInNodes();
    }

    /// <summary>
    /// Deploys only changed nodes (most granular incremental deployment).
    /// </summary>
    private async Task DeployNodesAsync(Workspace workspace)
    {
        // For now, nodes deployment falls back to flows deployment
        // A full implementation would track individual node changes and rewire connections
        await DeployFlowsAsync(workspace);
    }

    /// <summary>
    /// Checks if a flow has changed.
    /// </summary>
    private bool HasFlowChanged(Flow previous, Flow current)
    {
        // Check basic properties
        if (previous.Disabled != current.Disabled) return true;
        if (previous.Label != current.Label) return true;

        // Check node counts
        if (previous.Nodes.Count != current.Nodes.Count) return true;

        // Check individual nodes (simplified comparison)
        var previousNodeIds = new HashSet<string>(previous.Nodes.Select(n => n.Id));
        var currentNodeIds = new HashSet<string>(current.Nodes.Select(n => n.Id));

        if (!previousNodeIds.SetEquals(currentNodeIds)) return true;

        // Check each node's configuration
        foreach (var prevNode in previous.Nodes)
        {
            var currNode = current.Nodes.FirstOrDefault(n => n.Id == prevNode.Id);
            if (currNode == null) return true;

            if (HasNodeChanged(prevNode, currNode)) return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a node has changed.
    /// </summary>
    private bool HasNodeChanged(FlowNode previous, FlowNode current)
    {
        if (previous.Type != current.Type) return true;
        if (previous.Disabled != current.Disabled) return true;
        if (previous.Name != current.Name) return true;

        // Check wires
        if (previous.Wires.Count != current.Wires.Count) return true;
        for (int i = 0; i < previous.Wires.Count; i++)
        {
            if (!previous.Wires[i].SequenceEqual(current.Wires[i])) return true;
        }

        // Check config (simplified - compare key counts)
        if (previous.Config.Count != current.Config.Count) return true;

        return false;
    }

    /// <summary>
    /// Routes a message to a link in node (cross-flow).
    /// </summary>
    public void RouteLinkMessage(string linkInId, NodeMessage message)
    {
        if (_linkInNodes.TryGetValue(linkInId, out var linkInNode))
        {
            var clonedMessage = message.Clone();
            _ = Task.Run(async () =>
            {
                try
                {
                    await linkInNode.OnInputAsync(clonedMessage);
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke(new LogEntry
                    {
                        Level = LogLevel.Error,
                        Message = $"Error routing to link in node {linkInId}: {ex.Message}",
                        NodeId = linkInId
                    });
                }
            });
        }
    }

    /// <summary>
    /// Routes a return message back to a link call node.
    /// </summary>
    public void RouteLinkReturn(string linkCallId, NodeMessage message)
    {
        // Find the executor containing the link call node and route back
        foreach (var executor in _executors.Values)
        {
            var node = executor.GetNode(linkCallId);
            if (node != null)
            {
                var clonedMessage = message.Clone();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await node.OnInputAsync(clonedMessage);
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke(new LogEntry
                        {
                            Level = LogLevel.Error,
                            Message = $"Error routing return to link call node {linkCallId}: {ex.Message}",
                            NodeId = linkCallId
                        });
                    }
                });
                return;
            }
        }
    }

    /// <inheritdoc />
    public async Task InjectAsync(string nodeId, NodeMessage? message = null)
    {
        foreach (var executor in _executors.Values)
        {
            await executor.InjectAsync(nodeId, message);
        }
    }

    /// <inheritdoc />
    public NodeStatus? GetNodeStatus(string nodeId)
    {
        foreach (var executor in _executors.Values)
        {
            var status = executor.GetNodeStatus(nodeId);
            if (status != null) return status;
        }
        return null;
    }

    /// <inheritdoc />
    public string? GetCurrentRevision()
    {
        return _workspace?.Revision;
    }

    /// <inheritdoc />
    public async Task<string> DeployWithRevisionAsync(Workspace workspace, DeployType deployType = DeployType.Full, string? expectedRevision = null)
    {
        lock (_deployLock)
        {
            // Check for version conflict
            if (expectedRevision != null && _workspace != null)
            {
                var currentRevision = _workspace.Revision;
                if (currentRevision != expectedRevision)
                {
                    throw new VersionConflictException(expectedRevision, currentRevision);
                }
            }
        }

        // Update the revision before deployment
        workspace.UpdateRevision();

        // Perform the deployment
        await DeployAsync(workspace, deployType);

        return workspace.Revision;
    }
}
