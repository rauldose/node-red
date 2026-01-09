// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Events;
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
    private Workspace? _workspace;

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

        _workspace = workspace;
        _executors.Clear();

        foreach (var flow in workspace.Flows)
        {
            if (flow.Disabled) continue;

            var executor = new FlowExecutor(flow, _nodeRegistry, _globalContext);
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

    /// <inheritdoc />
    public async Task StopAsync()
    {
        State = FlowState.Stopping;

        foreach (var executor in _executors.Values)
        {
            await executor.StopAsync();
        }

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
            case DeployType.Nodes:
                // For now, treat all deploy types as full
                // TODO: Implement incremental deployment
                await DeployAsync(workspace, DeployType.Full);
                break;
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
}
