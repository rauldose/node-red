// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Execution;

/// <summary>
/// Executes a subflow instance by cloning its template nodes and remapping IDs.
/// Similar to the JavaScript Subflow.js implementation.
/// </summary>
public class SubflowExecutor
{
    private readonly Subflow _subflowDef;
    private readonly FlowNode _subflowInstance;
    private readonly FlowExecutor _parentExecutor;
    private readonly INodeRegistry _nodeRegistry;
    private readonly Dictionary<string, INode> _nodes = new();
    private readonly Dictionary<string, string> _nodeMap = new(); // originalId -> newId
    private readonly Dictionary<string, object?> _flowContext = new();
    private readonly Dictionary<string, object?> _globalContext;
    private readonly Dictionary<string, object?> _env = new();

    /// <summary>
    /// Event raised when a node's status changes.
    /// </summary>
    public event Action<string, NodeStatus>? OnNodeStatusChanged;

    /// <summary>
    /// Event raised when a log entry is created.
    /// </summary>
    public event Action<LogEntry>? OnLog;

    public SubflowExecutor(
        Subflow subflowDef,
        FlowNode subflowInstance,
        FlowExecutor parentExecutor,
        INodeRegistry nodeRegistry,
        Dictionary<string, object?> globalContext)
    {
        _subflowDef = subflowDef;
        _subflowInstance = subflowInstance;
        _parentExecutor = parentExecutor;
        _nodeRegistry = nodeRegistry;
        _globalContext = globalContext;

        // Initialize environment variables from subflow definition and instance overrides
        InitializeEnvironment();
    }

    /// <summary>
    /// Initializes environment variables, combining template defaults with instance overrides.
    /// </summary>
    private void InitializeEnvironment()
    {
        // Load defaults from subflow definition
        foreach (var envVar in _subflowDef.Env)
        {
            _env[envVar.Name] = EvaluateEnvValue(envVar.Value, envVar.Type);
        }

        // Override with instance-specific values from the subflow instance config
        if (_subflowInstance.Config.TryGetValue("env", out var instanceEnv) && instanceEnv is IEnumerable<object> envList)
        {
            foreach (var item in envList)
            {
                if (item is Dictionary<string, object?> envDict)
                {
                    var name = envDict.GetValueOrDefault("name")?.ToString();
                    var type = envDict.GetValueOrDefault("type")?.ToString() ?? "str";
                    var value = envDict.GetValueOrDefault("value");
                    if (!string.IsNullOrEmpty(name))
                    {
                        _env[name] = EvaluateEnvValue(value, type);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Evaluates an environment variable value based on its type.
    /// </summary>
    private object? EvaluateEnvValue(object? value, string type)
    {
        if (value == null) return null;

        return type switch
        {
            "bool" => value is bool b ? b : (value.ToString()?.ToLower() == "true"),
            "num" => Convert.ToDouble(value),
            "json" => value, // Keep as-is for JSON objects
            _ => value?.ToString()
        };
    }

    /// <summary>
    /// Initializes all cloned nodes in the subflow.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Clone all nodes from the subflow template with new unique IDs
        foreach (var templateNode in _subflowDef.Nodes)
        {
            var clonedNode = CloneNodeForSubflow(templateNode);
            _nodeMap[templateNode.Id] = clonedNode.Id;

            var node = _nodeRegistry.CreateNode(clonedNode.Type);
            if (node == null)
            {
                LogMessage(clonedNode.Id, $"Unknown node type in subflow: {clonedNode.Type}", LogLevel.Warning);
                continue;
            }

            var context = new SubflowNodeContext(this, _parentExecutor, _subflowDef.Id, _flowContext, _globalContext, _env);
            await node.InitializeAsync(clonedNode, context);
            _nodes[clonedNode.Id] = node;
        }

        // Remap all wire connections to use new IDs
        RemapWires();
    }

    /// <summary>
    /// Clones a node from the template with a new unique ID.
    /// </summary>
    private FlowNode CloneNodeForSubflow(FlowNode template)
    {
        var newId = $"{_subflowInstance.Id}-{template.Id}";
        return new FlowNode
        {
            Id = newId,
            Type = template.Type,
            Name = template.Name,
            X = template.X,
            Y = template.Y,
            Width = template.Width,
            Height = template.Height,
            FlowId = _subflowInstance.Id,
            Disabled = template.Disabled,
            Config = new Dictionary<string, object?>(template.Config),
            Wires = template.Wires.Select(w => new List<string>(w)).ToList()
        };
    }

    /// <summary>
    /// Remaps all wire connections in the cloned nodes to use new IDs.
    /// </summary>
    private void RemapWires()
    {
        foreach (var node in _nodes.Values)
        {
            for (int portIndex = 0; portIndex < node.Config.Wires.Count; portIndex++)
            {
                for (int wireIndex = 0; wireIndex < node.Config.Wires[portIndex].Count; wireIndex++)
                {
                    var originalTargetId = node.Config.Wires[portIndex][wireIndex];
                    if (_nodeMap.TryGetValue(originalTargetId, out var newTargetId))
                    {
                        node.Config.Wires[portIndex][wireIndex] = newTargetId;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles an incoming message to the subflow instance.
    /// Routes the message to the subflow's internal input nodes.
    /// </summary>
    public async Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        if (inputPort >= _subflowDef.In.Count) return;

        var inputDef = _subflowDef.In[inputPort];
        foreach (var wire in inputDef.Wires)
        {
            if (_nodeMap.TryGetValue(wire.Id, out var remappedId))
            {
                if (_nodes.TryGetValue(remappedId, out var targetNode))
                {
                    var clonedMessage = message.Clone();
                    await targetNode.OnInputAsync(clonedMessage, wire.Port);
                }
            }
        }
    }

    /// <summary>
    /// Routes a message from an internal node to connected nodes or subflow outputs.
    /// </summary>
    public void RouteMessage(string sourceNodeId, int port, NodeMessage message)
    {
        if (!_nodes.TryGetValue(sourceNodeId, out var sourceNode)) return;

        if (port >= sourceNode.Config.Wires.Count) return;

        var targetNodeIds = sourceNode.Config.Wires[port];
        foreach (var targetId in targetNodeIds)
        {
            // Check if target is an internal node
            if (_nodes.TryGetValue(targetId, out var targetNode))
            {
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

        // Check if this is connected to a subflow output
        CheckSubflowOutput(sourceNodeId, port, message);
    }

    /// <summary>
    /// Checks if a node output is connected to a subflow output and routes accordingly.
    /// </summary>
    private void CheckSubflowOutput(string sourceNodeId, int port, NodeMessage message)
    {
        // Find the original node ID
        var originalId = _nodeMap.FirstOrDefault(kv => kv.Value == sourceNodeId).Key;
        if (string.IsNullOrEmpty(originalId)) return;

        // Check each subflow output
        for (int outputIndex = 0; outputIndex < _subflowDef.Out.Count; outputIndex++)
        {
            var outputDef = _subflowDef.Out[outputIndex];
            foreach (var wire in outputDef.Wires)
            {
                if (wire.Id == originalId && wire.Port == port)
                {
                    // Route message to parent flow
                    _parentExecutor.RouteMessage(_subflowInstance.Id, outputIndex, message.Clone());
                }
            }
        }
    }

    /// <summary>
    /// Handles a node error within the subflow.
    /// </summary>
    public void HandleNodeError(string nodeId, Exception error)
    {
        LogMessage(nodeId, $"Error: {error.Message}", LogLevel.Error);
        UpdateNodeStatus(nodeId, NodeStatus.Error(error.Message));

        // Propagate error to parent executor for catch node handling
        _parentExecutor.HandleSubflowError(_subflowInstance.Id, nodeId, error);
    }

    /// <summary>
    /// Updates the status of a node within the subflow.
    /// </summary>
    public void UpdateNodeStatus(string nodeId, NodeStatus status)
    {
        OnNodeStatusChanged?.Invoke(nodeId, status);

        // If subflow has status output enabled, route status to parent
        if (_subflowDef.Status)
        {
            _parentExecutor.HandleSubflowStatus(_subflowInstance.Id, nodeId, status);
        }
    }

    /// <summary>
    /// Notifies that a node has completed processing a message.
    /// </summary>
    public void NotifyComplete(string nodeId, NodeMessage msg)
    {
        // Propagate to parent executor for complete node handling
        _parentExecutor.HandleSubflowComplete(_subflowInstance.Id, nodeId, msg);
    }

    /// <summary>
    /// Logs a message from a node within the subflow.
    /// </summary>
    public void LogMessage(string nodeId, string message, LogLevel level)
    {
        OnLog?.Invoke(new LogEntry
        {
            Level = level,
            Message = $"[Subflow:{_subflowDef.Name}] {message}",
            NodeId = nodeId
        });
    }

    /// <summary>
    /// Stops all nodes in the subflow.
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
    /// Gets an environment variable value.
    /// </summary>
    public object? GetEnv(string name)
    {
        return _env.GetValueOrDefault(name);
    }
}

/// <summary>
/// Node context for nodes running inside a subflow.
/// </summary>
internal class SubflowNodeContext : INodeContext
{
    private readonly SubflowExecutor _executor;
    private readonly FlowExecutor _parentExecutor;
    private readonly string _subflowId;
    private readonly Dictionary<string, object?> _flowContext;
    private readonly Dictionary<string, object?> _globalContext;
    private readonly Dictionary<string, object?> _env;
    private readonly Dictionary<string, object?> _nodeContext = new();

    public SubflowNodeContext(
        SubflowExecutor executor,
        FlowExecutor parentExecutor,
        string subflowId,
        Dictionary<string, object?> flowContext,
        Dictionary<string, object?> globalContext,
        Dictionary<string, object?> env)
    {
        _executor = executor;
        _parentExecutor = parentExecutor;
        _subflowId = subflowId;
        _flowContext = flowContext;
        _globalContext = globalContext;
        _env = env;
    }

    public void Send(string nodeId, int port, NodeMessage message)
    {
        _executor.RouteMessage(nodeId, port, message);
    }

    public void Done(string nodeId, Exception? error = null)
    {
        if (error != null)
        {
            _executor.HandleNodeError(nodeId, error);
        }
    }

    public void SetStatus(string nodeId, NodeStatus status)
    {
        _executor.UpdateNodeStatus(nodeId, status);
    }

    public void Log(string nodeId, string message, LogLevel level = LogLevel.Info)
    {
        _executor.LogMessage(nodeId, message, level);
    }

    public T? GetFlowContext<T>(string key)
    {
        if (_flowContext.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    public void SetFlowContext<T>(string key, T value)
    {
        _flowContext[key] = value;
    }

    public T? GetGlobalContext<T>(string key)
    {
        if (_globalContext.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    public void SetGlobalContext<T>(string key, T value)
    {
        _globalContext[key] = value;
    }

    /// <summary>
    /// Gets a node context value (subflow-specific).
    /// </summary>
    public T? GetNodeContext<T>(string key)
    {
        if (_nodeContext.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    /// <summary>
    /// Sets a node context value (subflow-specific).
    /// </summary>
    public void SetNodeContext<T>(string key, T value)
    {
        _nodeContext[key] = value;
    }

    /// <summary>
    /// Gets an environment variable value.
    /// </summary>
    public object? GetEnv(string name)
    {
        return _env.GetValueOrDefault(name);
    }
}
