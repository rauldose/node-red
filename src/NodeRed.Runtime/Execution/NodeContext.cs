// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Execution;

/// <summary>
/// Provides context for node execution.
/// </summary>
public class NodeContext : INodeContext
{
    private readonly FlowExecutor _executor;
    private readonly string _flowId;
    private readonly string _nodeId;
    private readonly Dictionary<string, object?> _flowContext;
    private readonly Dictionary<string, object?> _globalContext;
    private readonly Dictionary<string, object?> _nodeContext = new();
    private NodeMessage? _currentMessage;

    public NodeContext(
        FlowExecutor executor,
        string flowId,
        string nodeId,
        Dictionary<string, object?> flowContext,
        Dictionary<string, object?> globalContext)
    {
        _executor = executor;
        _flowId = flowId;
        _nodeId = nodeId;
        _flowContext = flowContext;
        _globalContext = globalContext;
    }

    /// <inheritdoc />
    public void Send(string nodeId, int port, NodeMessage message)
    {
        _executor.RouteMessage(nodeId, port, message);
    }

    /// <inheritdoc />
    public void Done(string nodeId, Exception? error = null)
    {
        if (error != null)
        {
            _executor.HandleNodeError(nodeId, error, _currentMessage);
        }
        else if (_currentMessage != null)
        {
            // Notify complete nodes
            _executor.NotifyCompleteNodes(nodeId, _currentMessage);
        }
    }

    /// <summary>
    /// Sets the current message being processed (for complete node tracking).
    /// </summary>
    public void SetCurrentMessage(NodeMessage msg)
    {
        _currentMessage = msg;
    }

    /// <inheritdoc />
    public void SetStatus(string nodeId, NodeStatus status)
    {
        _executor.UpdateNodeStatus(nodeId, status);
    }

    /// <inheritdoc />
    public void Log(string nodeId, string message, LogLevel level = LogLevel.Info)
    {
        _executor.LogMessage(nodeId, message, level);
    }

    /// <inheritdoc />
    public T? GetFlowContext<T>(string key)
    {
        if (_flowContext.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    /// <inheritdoc />
    public void SetFlowContext<T>(string key, T value)
    {
        _flowContext[key] = value;
    }

    /// <inheritdoc />
    public T? GetGlobalContext<T>(string key)
    {
        if (_globalContext.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    /// <inheritdoc />
    public void SetGlobalContext<T>(string key, T value)
    {
        _globalContext[key] = value;
    }

    /// <summary>
    /// Gets a value from the node-level context.
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
    /// Sets a value in the node-level context.
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
        // Check flow environment variables first
        var flowNode = _executor.GetNode(_nodeId);
        if (flowNode?.Config?.Config.TryGetValue("env", out var envObj) == true)
        {
            if (envObj is Dictionary<string, object?> envDict && envDict.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        // Return built-in environment variables
        return name switch
        {
            "NR_NODE_ID" => _nodeId,
            "NR_NODE_NAME" => flowNode?.Config?.Name ?? _nodeId,
            "NR_FLOW_ID" => _flowId,
            "NR_FLOW_NAME" => _flowId, // Would need flow reference for actual name
            _ => null
        };
    }
}
