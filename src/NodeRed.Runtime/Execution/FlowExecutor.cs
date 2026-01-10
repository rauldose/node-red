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
    private readonly Dictionary<string, SubflowExecutor> _subflowExecutors = new();
    private readonly List<Subflow> _subflowDefs;

    // Catch, Status, and Complete node lists for event handling
    private readonly List<INode> _catchNodes = new();
    private readonly List<INode> _statusNodes = new();
    private readonly Dictionary<string, List<INode>> _completeNodeMap = new();

    // Reference to the runtime for cross-flow operations
    private IFlowRuntime? _runtime;

    /// <summary>
    /// Event raised when a node's status changes.
    /// </summary>
    public event Action<string, NodeStatus>? OnNodeStatusChanged;

    /// <summary>
    /// Event raised when a debug message is generated.
    /// </summary>
    public event Action<DebugMessage>? OnDebugMessage;

    /// <summary>
    /// Event raised when a log entry is created.
    /// </summary>
    public event Action<LogEntry>? OnLog;

    public FlowExecutor(
        Flow flow,
        INodeRegistry nodeRegistry,
        Dictionary<string, object?> globalContext,
        List<Subflow>? subflowDefs = null)
    {
        _flow = flow;
        _nodeRegistry = nodeRegistry;
        _globalContext = globalContext;
        _subflowDefs = subflowDefs ?? new List<Subflow>();
    }

    /// <summary>
    /// Sets the runtime reference for cross-flow operations.
    /// </summary>
    public void SetRuntime(IFlowRuntime runtime)
    {
        _runtime = runtime;
    }

    /// <summary>
    /// Gets the flow ID.
    /// </summary>
    public string FlowId => _flow.Id;

    /// <summary>
    /// Gets a node by ID.
    /// </summary>
    public INode? GetNode(string nodeId)
    {
        return _nodes.GetValueOrDefault(nodeId);
    }

    /// <summary>
    /// Initializes all nodes in the flow.
    /// </summary>
    public async Task InitializeAsync()
    {
        foreach (var nodeConfig in _flow.Nodes)
        {
            if (nodeConfig.Disabled) continue;

            // Check if this is a subflow instance
            if (nodeConfig.Type.StartsWith("subflow:"))
            {
                await InitializeSubflowInstance(nodeConfig);
                continue;
            }

            var node = _nodeRegistry.CreateNode(nodeConfig.Type);
            if (node == null)
            {
                LogMessage(nodeConfig.Id, $"Unknown node type: {nodeConfig.Type}", LogLevel.Warning);
                continue;
            }

            var context = new NodeContext(this, _flow.Id, nodeConfig.Id, _flowContext, _globalContext);
            await node.InitializeAsync(nodeConfig, context);
            _nodes[nodeConfig.Id] = node;

            // Track special node types
            switch (nodeConfig.Type)
            {
                case "catch":
                    _catchNodes.Add(node);
                    break;
                case "status":
                    _statusNodes.Add(node);
                    break;
                case "complete":
                    RegisterCompleteNode(nodeConfig, node);
                    break;
            }
        }

        // Sort catch nodes: scoped nodes first, then uncaught-only nodes last
        _catchNodes.Sort((a, b) =>
        {
            var aScope = GetNodeConfigValue(a.Config, "scope", "all");
            var bScope = GetNodeConfigValue(b.Config, "scope", "all");
            var aUncaught = GetNodeConfigValue(a.Config, "uncaught", false);
            var bUncaught = GetNodeConfigValue(b.Config, "uncaught", false);

            if (aScope != "all" && bScope == "all") return -1;
            if (aScope == "all" && bScope != "all") return 1;
            if (aUncaught && !bUncaught) return 1;
            if (!aUncaught && bUncaught) return -1;
            return 0;
        });
    }

    /// <summary>
    /// Initializes a subflow instance node.
    /// </summary>
    private async Task InitializeSubflowInstance(FlowNode nodeConfig)
    {
        var subflowId = nodeConfig.Type.Substring(8); // Remove "subflow:" prefix
        var subflowDef = _subflowDefs.FirstOrDefault(s => s.Id == subflowId);

        if (subflowDef == null)
        {
            LogMessage(nodeConfig.Id, $"Subflow definition not found: {subflowId}", LogLevel.Warning);
            return;
        }

        var subflowExecutor = new SubflowExecutor(subflowDef, nodeConfig, this, _nodeRegistry, _globalContext);
        subflowExecutor.OnNodeStatusChanged += (id, status) => OnNodeStatusChanged?.Invoke(id, status);
        subflowExecutor.OnLog += entry => OnLog?.Invoke(entry);

        await subflowExecutor.InitializeAsync();
        _subflowExecutors[nodeConfig.Id] = subflowExecutor;

        // Create a wrapper node for the subflow instance
        var wrapperNode = new SubflowInstanceNode(subflowExecutor, nodeConfig);
        _nodes[nodeConfig.Id] = wrapperNode;
    }

    /// <summary>
    /// Registers a complete node for specific target nodes.
    /// </summary>
    private void RegisterCompleteNode(FlowNode nodeConfig, INode node)
    {
        var scope = GetNodeConfigValue(nodeConfig, "scope", "all");
        if (scope == "all")
        {
            // Will be handled dynamically for all nodes
            return;
        }

        // Get target node IDs from scope config
        if (nodeConfig.Config.TryGetValue("scope", out var scopeValue) && scopeValue is IEnumerable<object> targets)
        {
            foreach (var target in targets)
            {
                var targetId = target?.ToString();
                if (!string.IsNullOrEmpty(targetId))
                {
                    if (!_completeNodeMap.ContainsKey(targetId))
                    {
                        _completeNodeMap[targetId] = new List<INode>();
                    }
                    _completeNodeMap[targetId].Add(node);
                }
            }
        }
    }

    /// <summary>
    /// Gets a config value with a default.
    /// </summary>
    private T GetNodeConfigValue<T>(FlowNode config, string key, T defaultValue)
    {
        if (config.Config.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
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
        // Stop subflow executors
        foreach (var subflow in _subflowExecutors.Values)
        {
            await subflow.StopAsync();
        }
        _subflowExecutors.Clear();

        // Stop regular nodes
        foreach (var node in _nodes.Values)
        {
            await node.CloseAsync();
        }
        _nodes.Clear();
        _catchNodes.Clear();
        _statusNodes.Clear();
        _completeNodeMap.Clear();
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
            // Check for link out nodes and handle cross-flow routing
            var targetConfig = _flow.Nodes.FirstOrDefault(n => n.Id == targetId);
            if (targetConfig?.Type == "link out")
            {
                HandleLinkOut(targetConfig, message);
                continue;
            }

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
                        HandleNodeError(targetId, ex, clonedMessage);
                    }
                });
            }
        }
    }

    /// <summary>
    /// Handles a link out node - routes messages to connected link in nodes.
    /// </summary>
    private void HandleLinkOut(FlowNode linkOutConfig, NodeMessage message)
    {
        var mode = GetNodeConfigValue(linkOutConfig, "mode", "link");

        if (mode == "return")
        {
            // Return mode - send back to the link call node
            if (message.Properties.TryGetValue("_linkSource", out var linkSourceObj) && linkSourceObj is string linkSource)
            {
                RouteLinkReturn(linkSource, message);
            }
            return;
        }

        // Normal link mode - route to connected link in nodes
        if (linkOutConfig.Config.TryGetValue("links", out var linksObj) && linksObj is IEnumerable<object> links)
        {
            foreach (var link in links)
            {
                var linkInId = link?.ToString();
                if (!string.IsNullOrEmpty(linkInId))
                {
                    RouteLinkIn(linkInId, message);
                }
            }
        }
    }

    /// <summary>
    /// Routes a message to a link in node (cross-flow).
    /// </summary>
    private void RouteLinkIn(string linkInId, NodeMessage message)
    {
        // First check local flow
        if (_nodes.TryGetValue(linkInId, out var localNode))
        {
            var clonedMessage = message.Clone();
            _ = Task.Run(async () =>
            {
                try
                {
                    await localNode.OnInputAsync(clonedMessage);
                }
                catch (Exception ex)
                {
                    HandleNodeError(linkInId, ex, clonedMessage);
                }
            });
            return;
        }

        // Cross-flow routing via runtime
        if (_runtime is FlowRuntime runtime)
        {
            runtime.RouteLinkMessage(linkInId, message);
        }
    }

    /// <summary>
    /// Routes a message back to a link call node.
    /// </summary>
    private void RouteLinkReturn(string linkCallId, NodeMessage message)
    {
        // First check local flow
        if (_nodes.TryGetValue(linkCallId, out var localNode))
        {
            var clonedMessage = message.Clone();
            clonedMessage.Properties.Remove("_linkSource");
            _ = Task.Run(async () =>
            {
                try
                {
                    await localNode.OnInputAsync(clonedMessage);
                }
                catch (Exception ex)
                {
                    HandleNodeError(linkCallId, ex, clonedMessage);
                }
            });
            return;
        }

        // Cross-flow routing via runtime
        if (_runtime is FlowRuntime runtime)
        {
            message.Properties.Remove("_linkSource");
            runtime.RouteLinkReturn(linkCallId, message);
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
    /// Handles a node error and triggers catch nodes.
    /// </summary>
    public void HandleNodeError(string nodeId, Exception error, NodeMessage? originalMsg = null)
    {
        LogMessage(nodeId, $"Error: {error.Message}", LogLevel.Error);
        UpdateNodeStatus(nodeId, NodeStatus.Error(error.Message));

        // Find the source node config for error details
        var sourceNodeConfig = _flow.Nodes.FirstOrDefault(n => n.Id == nodeId);
        var sourceName = sourceNodeConfig?.Name ?? nodeId;
        var sourceType = sourceNodeConfig?.Type ?? "unknown";

        // Build error message for catch nodes
        var errorMsg = originalMsg?.Clone() ?? new NodeMessage();
        errorMsg.Properties["error"] = new Dictionary<string, object?>
        {
            { "message", error.Message },
            { "source", new Dictionary<string, object?>
                {
                    { "id", nodeId },
                    { "type", sourceType },
                    { "name", sourceName },
                    { "count", GetErrorCount(originalMsg) + 1 }
                }
            }
        };

        if (error is AggregateException ae)
        {
            errorMsg.Properties["error"] = new Dictionary<string, object?>
            {
                { "message", ae.InnerException?.Message ?? error.Message },
                { "stack", ae.StackTrace }
            };
        }

        // Trigger catch nodes
        bool handled = false;
        foreach (var catchNode in _catchNodes)
        {
            var scope = GetNodeConfigValue(catchNode.Config, "scope", "all");
            var uncaught = GetNodeConfigValue(catchNode.Config, "uncaught", false);

            // Skip uncaught-only nodes if already handled
            if (uncaught && handled) continue;

            // Check scope
            if (scope != "all" && scope != "group")
            {
                // Scoped to specific nodes
                if (catchNode.Config.Config.TryGetValue("scope", out var scopeValue) && scopeValue is IEnumerable<object> targets)
                {
                    if (!targets.Any(t => t?.ToString() == nodeId)) continue;
                }
            }

            // Send error to catch node
            var clonedError = errorMsg.Clone();
            _ = Task.Run(async () =>
            {
                try
                {
                    await catchNode.OnInputAsync(clonedError);
                }
                catch (Exception ex)
                {
                    LogMessage(catchNode.Config.Id, $"Error in catch node: {ex.Message}", LogLevel.Error);
                }
            });

            handled = true;
            if (!uncaught) break; // Stop at first non-uncaught handler
        }
    }

    /// <summary>
    /// Gets the error count from a message for loop detection.
    /// </summary>
    private int GetErrorCount(NodeMessage? msg)
    {
        if (msg?.Properties.TryGetValue("error", out var errorObj) == true &&
            errorObj is Dictionary<string, object?> errorDict &&
            errorDict.TryGetValue("source", out var sourceObj) &&
            sourceObj is Dictionary<string, object?> sourceDict &&
            sourceDict.TryGetValue("count", out var countObj))
        {
            return Convert.ToInt32(countObj);
        }
        return 0;
    }

    /// <summary>
    /// Handles an error from a subflow.
    /// </summary>
    public void HandleSubflowError(string subflowInstanceId, string internalNodeId, Exception error)
    {
        HandleNodeError(subflowInstanceId, error);
    }

    /// <summary>
    /// Handles a status update from a subflow.
    /// </summary>
    public void HandleSubflowStatus(string subflowInstanceId, string internalNodeId, NodeStatus status)
    {
        // Route status to status nodes in this flow
        NotifyStatusNodes(subflowInstanceId, status);
    }

    /// <summary>
    /// Handles a complete notification from a subflow.
    /// </summary>
    public void HandleSubflowComplete(string subflowInstanceId, string internalNodeId, NodeMessage msg)
    {
        NotifyCompleteNodes(subflowInstanceId, msg);
    }

    /// <summary>
    /// Updates the status of a node and notifies status nodes.
    /// </summary>
    public void UpdateNodeStatus(string nodeId, NodeStatus status)
    {
        _nodeStatuses[nodeId] = status;
        OnNodeStatusChanged?.Invoke(nodeId, status);

        // Notify status nodes
        NotifyStatusNodes(nodeId, status);
    }

    /// <summary>
    /// Notifies status nodes of a status change.
    /// </summary>
    private void NotifyStatusNodes(string sourceNodeId, NodeStatus status)
    {
        var sourceNodeConfig = _flow.Nodes.FirstOrDefault(n => n.Id == sourceNodeId);

        foreach (var statusNode in _statusNodes)
        {
            var scope = GetNodeConfigValue(statusNode.Config, "scope", "all");

            // Check scope
            if (scope != "all")
            {
                if (statusNode.Config.Config.TryGetValue("scope", out var scopeValue) && scopeValue is IEnumerable<object> targets)
                {
                    if (!targets.Any(t => t?.ToString() == sourceNodeId)) continue;
                }
            }

            // Build status message
            var statusMsg = new NodeMessage
            {
                Payload = status.Text
            };
            statusMsg.Properties["status"] = new Dictionary<string, object?>
            {
                { "text", status.Text },
                { "fill", status.Color.ToString().ToLower() },
                { "shape", status.Shape.ToString().ToLower() },
                { "source", new Dictionary<string, object?>
                    {
                        { "id", sourceNodeId },
                        { "type", sourceNodeConfig?.Type },
                        { "name", sourceNodeConfig?.Name }
                    }
                }
            };

            _ = Task.Run(async () =>
            {
                try
                {
                    await statusNode.OnInputAsync(statusMsg);
                }
                catch (Exception ex)
                {
                    LogMessage(statusNode.Config.Id, $"Error in status node: {ex.Message}", LogLevel.Error);
                }
            });
        }
    }

    /// <summary>
    /// Notifies complete nodes that a node has finished processing.
    /// </summary>
    public void NotifyCompleteNodes(string nodeId, NodeMessage msg)
    {
        var sourceNodeConfig = _flow.Nodes.FirstOrDefault(n => n.Id == nodeId);

        // Find complete nodes that should be triggered
        var completeNodes = new List<INode>();

        // Check specific mappings
        if (_completeNodeMap.TryGetValue(nodeId, out var mappedNodes))
        {
            completeNodes.AddRange(mappedNodes);
        }

        // Check "all" scope complete nodes
        foreach (var node in _nodes.Values)
        {
            if (node.Definition.Type == "complete")
            {
                var scope = GetNodeConfigValue(node.Config, "scope", "all");
                if (scope == "all" && !completeNodes.Contains(node))
                {
                    completeNodes.Add(node);
                }
            }
        }

        foreach (var completeNode in completeNodes)
        {
            var completeMsg = msg.Clone();
            completeMsg.Properties["complete"] = new Dictionary<string, object?>
            {
                { "source", new Dictionary<string, object?>
                    {
                        { "id", nodeId },
                        { "type", sourceNodeConfig?.Type },
                        { "name", sourceNodeConfig?.Name }
                    }
                }
            };

            _ = Task.Run(async () =>
            {
                try
                {
                    await completeNode.OnInputAsync(completeMsg);
                }
                catch (Exception ex)
                {
                    LogMessage(completeNode.Config.Id, $"Error in complete node: {ex.Message}", LogLevel.Error);
                }
            });
        }
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

/// <summary>
/// A wrapper node that represents a subflow instance.
/// </summary>
internal class SubflowInstanceNode : INode
{
    private readonly SubflowExecutor _executor;
    private readonly FlowNode _config;

    public SubflowInstanceNode(SubflowExecutor executor, FlowNode config)
    {
        _executor = executor;
        _config = config;
    }

    public NodeDefinition Definition => new NodeDefinition
    {
        Type = _config.Type,
        DisplayName = _config.Name,
        Category = NodeCategory.Common,
        Inputs = 1,
        Outputs = 1
    };

    public FlowNode Config => _config;

    public Task InitializeAsync(FlowNode config, INodeContext context)
    {
        // Already initialized by FlowExecutor
        return Task.CompletedTask;
    }

    public async Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        await _executor.OnInputAsync(message, inputPort);
    }

    public async Task CloseAsync()
    {
        await _executor.StopAsync();
    }
}
