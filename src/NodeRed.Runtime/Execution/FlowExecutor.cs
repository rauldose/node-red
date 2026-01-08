// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Execution;

/// <summary>
/// Executes a single flow (tab).
/// </summary>
public class FlowExecutor
{
    private readonly Flow _flow;
    private readonly INodeRegistry _nodeRegistry;
    private readonly Dictionary<string, INode> _nodes = new();
    private readonly Dictionary<string, NodeStatus> _nodeStatuses = new();
    private readonly Dictionary<string, object?> _flowContext = new();
    private readonly Dictionary<string, object?> _globalContext;

    /// <summary>
    /// Event raised when a node's status changes.
    /// </summary>
    public event Action<string, NodeStatus>? OnNodeStatusChanged;

    /// <summary>
    /// Event raised when a debug message is generated.
    /// Reserved for future implementation of debug node integration.
    /// </summary>
#pragma warning disable CS0067 // Event is never used - reserved for future debug node integration
    public event Action<DebugMessage>? OnDebugMessage;
#pragma warning restore CS0067

    /// <summary>
    /// Event raised when a log entry is created.
    /// </summary>
    public event Action<LogEntry>? OnLog;

    public FlowExecutor(
        Flow flow,
        INodeRegistry nodeRegistry,
        Dictionary<string, object?> globalContext)
    {
        _flow = flow;
        _nodeRegistry = nodeRegistry;
        _globalContext = globalContext;
    }

    /// <summary>
    /// Initializes all nodes in the flow.
    /// </summary>
    public async Task InitializeAsync()
    {
        foreach (var nodeConfig in _flow.Nodes)
        {
            if (nodeConfig.Disabled) continue;

            var node = _nodeRegistry.CreateNode(nodeConfig.Type);
            if (node == null)
            {
                LogMessage(nodeConfig.Id, $"Unknown node type: {nodeConfig.Type}", LogLevel.Warning);
                continue;
            }

            var context = new NodeContext(this, _flow.Id, _flowContext, _globalContext);
            await node.InitializeAsync(nodeConfig, context);
            _nodes[nodeConfig.Id] = node;
        }
    }

    /// <summary>
    /// Starts the flow execution.
    /// </summary>
    public async Task StartAsync()
    {
        // Nodes that need to start on their own (like inject nodes with "once" configured)
        // are already running from initialization
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops all nodes in the flow.
    /// </summary>
    public async Task StopAsync()
    {
        foreach (var node in _nodes.Values)
        {
            await node.CloseAsync();
        }
        _nodes.Clear();
    }

    /// <summary>
    /// Routes a message from a node's output to connected nodes.
    /// </summary>
    public void RouteMessage(string sourceNodeId, int port, NodeMessage message)
    {
        var sourceNode = _flow.Nodes.FirstOrDefault(n => n.Id == sourceNodeId);
        if (sourceNode == null) return;

        if (port >= sourceNode.Wires.Count) return;

        var targetNodeIds = sourceNode.Wires[port];
        foreach (var targetId in targetNodeIds)
        {
            if (_nodes.TryGetValue(targetId, out var targetNode))
            {
                // Clone message for each target to prevent shared state issues
                var clonedMessage = message.Clone();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await targetNode.OnInputAsync(clonedMessage);
                    }
                    catch (Exception ex)
                    {
                        HandleNodeError(targetId, ex);
                    }
                });
            }
        }
    }

    /// <summary>
    /// Injects a message into a specific node.
    /// </summary>
    public async Task InjectAsync(string nodeId, NodeMessage? message = null)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            var msg = message ?? new NodeMessage { Payload = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            await node.OnInputAsync(msg);
        }
    }

    /// <summary>
    /// Handles a node error.
    /// </summary>
    public void HandleNodeError(string nodeId, Exception error)
    {
        LogMessage(nodeId, $"Error: {error.Message}", LogLevel.Error);
        UpdateNodeStatus(nodeId, NodeStatus.Error(error.Message));

        // Notify catch nodes
        // TODO: Implement catch node notification
    }

    /// <summary>
    /// Updates the status of a node.
    /// </summary>
    public void UpdateNodeStatus(string nodeId, NodeStatus status)
    {
        _nodeStatuses[nodeId] = status;
        OnNodeStatusChanged?.Invoke(nodeId, status);
    }

    /// <summary>
    /// Gets the status of a node.
    /// </summary>
    public NodeStatus? GetNodeStatus(string nodeId)
    {
        return _nodeStatuses.GetValueOrDefault(nodeId);
    }

    /// <summary>
    /// Logs a message from a node.
    /// </summary>
    public void LogMessage(string nodeId, string message, LogLevel level)
    {
        OnLog?.Invoke(new LogEntry
        {
            Level = level,
            Message = message,
            NodeId = nodeId
        });
    }
}
