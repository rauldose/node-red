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
    private readonly Dictionary<string, object?> _flowContext;
    private readonly Dictionary<string, object?> _globalContext;

    public NodeContext(
        FlowExecutor executor,
        string flowId,
        Dictionary<string, object?> flowContext,
        Dictionary<string, object?> globalContext)
    {
        _executor = executor;
        _flowId = flowId;
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
            _executor.HandleNodeError(nodeId, error);
        }
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
}
