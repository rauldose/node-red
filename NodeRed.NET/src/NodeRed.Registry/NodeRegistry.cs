// ============================================================
// INSPIRED BY: @node-red/registry/lib/registry.js
// REFERENCE: NODE-RED-ARCHITECTURE-ANALYSIS.md - Registry Pattern
// ============================================================
// Node registry for managing node types
// ============================================================

/*!
 * Original work Copyright JS Foundation and other contributors, http://js.foundation
 * Modified work Copyright 2026 NodeRed.NET Contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 */

using Microsoft.Extensions.Logging;
using NodeRed.Runtime;
using NodeRed.Util;
using System.Collections.Concurrent;

namespace NodeRed.Registry;

/// <summary>
/// Node type metadata
/// </summary>
public class NodeTypeInfo
{
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Color { get; set; } = "#a6bbcf";
    public string Icon { get; set; } = "function.svg";
    public int Inputs { get; set; } = 1;
    public int Outputs { get; set; } = 1;
    public string Label { get; set; } = string.Empty;
    public Type? ImplementationType { get; set; }
}

/// <summary>
/// Node factory delegate
/// </summary>
public delegate NodeBase NodeFactory(NodeConfiguration config, ILogger logger, INodeContext context);

/// <summary>
/// Node registry - manages available node types
/// Maps to: Registry in @node-red/registry/lib/registry.js
/// Reference: NODE-RED-ARCHITECTURE-ANALYSIS.md - Registry Pattern
/// </summary>
public class NodeRegistry
{
    private readonly ConcurrentDictionary<string, NodeTypeInfo> _nodeTypes = new();
    private readonly ConcurrentDictionary<string, NodeFactory> _nodeFactories = new();
    private readonly ILogger<NodeRegistry> _logger;

    public NodeRegistry(ILogger<NodeRegistry> logger)
    {
        _logger = logger;
        RegisterBuiltInNodes();
    }

    /// <summary>
    /// Register a node type
    /// Maps to: RED.nodes.registerType() in Node-RED
    /// </summary>
    public void RegisterNodeType(string type, NodeTypeInfo info, NodeFactory factory)
    {
        _nodeTypes[type] = info;
        _nodeFactories[type] = factory;
        _logger.LogInformation($"Registered node type: {type}");
        
        // Emit event
        RuntimeEvents.Instance.Emit(RuntimeEventNames.NodeAdded, new { Type = type, Info = info });
    }

    /// <summary>
    /// Get node type information
    /// </summary>
    public NodeTypeInfo? GetNodeType(string type)
    {
        return _nodeTypes.TryGetValue(type, out var info) ? info : null;
    }

    /// <summary>
    /// Get all registered node types
    /// </summary>
    public IEnumerable<NodeTypeInfo> GetAllNodeTypes()
    {
        return _nodeTypes.Values;
    }

    /// <summary>
    /// Get node types by category
    /// </summary>
    public IEnumerable<NodeTypeInfo> GetNodeTypesByCategory(string category)
    {
        return _nodeTypes.Values.Where(n => n.Category == category);
    }

    /// <summary>
    /// Create a node instance
    /// </summary>
    public NodeBase? CreateNode(NodeConfiguration config, ILogger logger, INodeContext context)
    {
        if (_nodeFactories.TryGetValue(config.Type, out var factory))
        {
            try
            {
                var node = factory(config, logger, context);
                _logger.LogDebug($"Created node: {config.Type} ({config.Id})");
                return node;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating node {config.Type}");
                return null;
            }
        }

        _logger.LogWarning($"Unknown node type: {config.Type}");
        return null;
    }

    /// <summary>
    /// Check if a node type is registered
    /// </summary>
    public bool IsNodeTypeRegistered(string type)
    {
        return _nodeTypes.ContainsKey(type);
    }

    /// <summary>
    /// Unregister a node type
    /// </summary>
    public bool UnregisterNodeType(string type)
    {
        var removed = _nodeTypes.TryRemove(type, out _) && _nodeFactories.TryRemove(type, out _);
        if (removed)
        {
            _logger.LogInformation($"Unregistered node type: {type}");
            RuntimeEvents.Instance.Emit(RuntimeEventNames.NodeRemoved, new { Type = type });
        }
        return removed;
    }

    /// <summary>
    /// Register built-in core nodes
    /// </summary>
    private void RegisterBuiltInNodes()
    {
        // Note: In full implementation, these would be discovered and loaded dynamically
        // For now, we'll register them manually

        _logger.LogInformation("Registering built-in nodes...");

        // Register inject node
        RegisterNodeType("inject", new NodeTypeInfo
        {
            Type = "inject",
            Category = "input",
            Color = "#c7e9c0",
            Icon = "inject.svg",
            Inputs = 0,
            Outputs = 1,
            Label = "inject"
        }, (config, logger, context) =>
        {
            // Would need to cast config to InjectNodeConfiguration
            // For now, return null - full impl would create InjectNode
            throw new NotImplementedException("Node factory not implemented for inject");
        });

        // Register debug node
        RegisterNodeType("debug", new NodeTypeInfo
        {
            Type = "debug",
            Category = "output",
            Color = "#87a980",
            Icon = "debug.svg",
            Inputs = 1,
            Outputs = 0,
            Label = "debug"
        }, (config, logger, context) =>
        {
            throw new NotImplementedException("Node factory not implemented for debug");
        });

        // Register function node
        RegisterNodeType("function", new NodeTypeInfo
        {
            Type = "function",
            Category = "function",
            Color = "#fdd0a2",
            Icon = "function.svg",
            Inputs = 1,
            Outputs = 1,
            Label = "function"
        }, (config, logger, context) =>
        {
            throw new NotImplementedException("Node factory not implemented for function");
        });

        _logger.LogInformation($"Registered {_nodeTypes.Count} built-in node types");
    }
}
