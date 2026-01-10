// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Blazor.Services;

/// <summary>
/// Service for accessing Node-RED context data (node, flow, global contexts).
/// Matches the functionality of the JS tab-context.js
/// </summary>
public interface IContextDataService
{
    /// <summary>
    /// Gets the context data for a specific node.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>Dictionary of context key-value pairs.</returns>
    Task<Dictionary<string, object?>> GetNodeContextAsync(string nodeId);

    /// <summary>
    /// Gets the context data for a specific flow.
    /// </summary>
    /// <param name="flowId">The flow ID.</param>
    /// <returns>Dictionary of context key-value pairs.</returns>
    Task<Dictionary<string, object?>> GetFlowContextAsync(string flowId);

    /// <summary>
    /// Gets the global context data.
    /// </summary>
    /// <returns>Dictionary of context key-value pairs.</returns>
    Task<Dictionary<string, object?>> GetGlobalContextAsync();

    /// <summary>
    /// Sets a value in node context.
    /// </summary>
    Task SetNodeContextAsync(string nodeId, string key, object? value);

    /// <summary>
    /// Sets a value in flow context.
    /// </summary>
    Task SetFlowContextAsync(string flowId, string key, object? value);

    /// <summary>
    /// Sets a value in global context.
    /// </summary>
    Task SetGlobalContextAsync(string key, object? value);
}

/// <summary>
/// In-memory implementation of context data service.
/// In a production system, this would integrate with a persistent context store.
/// </summary>
public class ContextDataService : IContextDataService
{
    // Context storage - in production this would be backed by a real store
    private readonly Dictionary<string, Dictionary<string, object?>> _nodeContexts = new();
    private readonly Dictionary<string, Dictionary<string, object?>> _flowContexts = new();
    private readonly Dictionary<string, object?> _globalContext = new();

    /// <inheritdoc/>
    public Task<Dictionary<string, object?>> GetNodeContextAsync(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return Task.FromResult(new Dictionary<string, object?>());

        if (_nodeContexts.TryGetValue(nodeId, out var context))
            return Task.FromResult(new Dictionary<string, object?>(context));

        return Task.FromResult(new Dictionary<string, object?>());
    }

    /// <inheritdoc/>
    public Task<Dictionary<string, object?>> GetFlowContextAsync(string flowId)
    {
        if (string.IsNullOrEmpty(flowId))
            return Task.FromResult(new Dictionary<string, object?>());

        if (_flowContexts.TryGetValue(flowId, out var context))
            return Task.FromResult(new Dictionary<string, object?>(context));

        return Task.FromResult(new Dictionary<string, object?>());
    }

    /// <inheritdoc/>
    public Task<Dictionary<string, object?>> GetGlobalContextAsync()
    {
        return Task.FromResult(new Dictionary<string, object?>(_globalContext));
    }

    /// <inheritdoc/>
    public Task SetNodeContextAsync(string nodeId, string key, object? value)
    {
        if (string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(key))
            return Task.CompletedTask;

        if (!_nodeContexts.ContainsKey(nodeId))
            _nodeContexts[nodeId] = new Dictionary<string, object?>();

        _nodeContexts[nodeId][key] = value;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetFlowContextAsync(string flowId, string key, object? value)
    {
        if (string.IsNullOrEmpty(flowId) || string.IsNullOrEmpty(key))
            return Task.CompletedTask;

        if (!_flowContexts.ContainsKey(flowId))
            _flowContexts[flowId] = new Dictionary<string, object?>();

        _flowContexts[flowId][key] = value;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetGlobalContextAsync(string key, object? value)
    {
        if (string.IsNullOrEmpty(key))
            return Task.CompletedTask;

        _globalContext[key] = value;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears all context data. Used during testing or reset.
    /// </summary>
    public void ClearAll()
    {
        _nodeContexts.Clear();
        _flowContexts.Clear();
        _globalContext.Clear();
    }

    /// <summary>
    /// Clears context data for a specific node.
    /// </summary>
    public void ClearNodeContext(string nodeId)
    {
        _nodeContexts.Remove(nodeId);
    }

    /// <summary>
    /// Clears context data for a specific flow.
    /// </summary>
    public void ClearFlowContext(string flowId)
    {
        _flowContexts.Remove(flowId);
    }
}
