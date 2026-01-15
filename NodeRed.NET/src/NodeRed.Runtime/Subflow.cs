// ============================================================
// INSPIRED BY: @node-red/runtime/lib/flows/Subflow.js
// REFERENCE: NODE-RED-ARCHITECTURE-ANALYSIS.md - Subflows section
// ============================================================
// Subflow - reusable flow component with inputs and outputs
// ============================================================

/*!
 * Original work Copyright JS Foundation and other contributors, http://js.foundation
 * Modified work Copyright 2026 NodeRed.NET Contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 */

using Microsoft.Extensions.Logging;

namespace NodeRed.Runtime;

/// <summary>
/// Subflow input/output configuration
/// </summary>
public class SubflowPort
{
    public double X { get; set; }
    public double Y { get; set; }
    public List<SubflowWire> Wires { get; set; } = new();
}

/// <summary>
/// Subflow wire connection
/// </summary>
public class SubflowWire
{
    public string Id { get; set; } = string.Empty;
    public int Port { get; set; } = 0;
}

/// <summary>
/// Subflow definition configuration
/// Maps to: Subflow definition in Node-RED
/// </summary>
public class SubflowDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "subflow";
    public string Name { get; set; } = string.Empty;
    public string? Info { get; set; }
    public string? Category { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public List<SubflowPort> In { get; set; } = new();
    public List<SubflowPort> Out { get; set; } = new();
    public List<NodeConfiguration> Nodes { get; set; } = new(); // Internal nodes
    public List<GroupConfiguration> Groups { get; set; } = new(); // Internal groups
    public Dictionary<string, object>? Env { get; set; } // Default env variables
}

/// <summary>
/// Subflow instance configuration
/// Maps to: Subflow instance node in Node-RED
/// </summary>
public class SubflowInstanceConfiguration : NodeConfiguration
{
    public Dictionary<string, object>? Env { get; set; } // Instance-specific env
}

/// <summary>
/// Subflow - reusable flow component
/// Maps to: Subflow class in @node-red/runtime/lib/flows/Subflow.js
/// Reference: NODE-RED-ARCHITECTURE-ANALYSIS.md - Advanced Features
/// 
/// Subflows provide:
/// - Encapsulation of flow logic
/// - Reusability across multiple flows
/// - Input/output ports like a function
/// - Instance-specific configuration
/// - Can be exported and shared
/// </summary>
public class Subflow
{
    private readonly SubflowDefinition _definition;
    private readonly SubflowInstanceConfiguration _instance;
    private readonly ILogger _logger;
    private readonly Dictionary<string, NodeBase> _internalNodes = new();
    private readonly Dictionary<string, Group> _internalGroups = new();
    private readonly Dictionary<string, object?> _env = new();
    private bool _started = false;

    public string Id => _instance.Id;
    public string DefinitionId => _definition.Id;
    public string Name => _instance.Name ?? _definition.Name;
    public string Type => _instance.Type;

    public Subflow(SubflowDefinition definition, SubflowInstanceConfiguration instance, ILogger logger)
    {
        _definition = definition;
        _instance = instance;
        _logger = logger;
    }

    /// <summary>
    /// Start the subflow and all internal nodes
    /// Maps to: Subflow constructor and start logic in Node-RED
    /// </summary>
    public async Task StartAsync()
    {
        if (_started)
        {
            _logger.LogWarning($"Subflow {Name} already started");
            return;
        }

        _logger.LogInformation($"Starting subflow: {Name} ({Id})");

        // Merge environment variables (definition defaults + instance overrides)
        if (_definition.Env != null)
        {
            foreach (var kvp in _definition.Env)
            {
                _env[kvp.Key] = kvp.Value;
            }
        }

        if (_instance.Env != null)
        {
            foreach (var kvp in _instance.Env)
            {
                _env[kvp.Key] = kvp.Value; // Instance overrides definition
            }
        }

        // Create internal groups
        foreach (var groupConfig in _definition.Groups)
        {
            var group = new Group(groupConfig, _logger);
            await group.StartAsync();
            _internalGroups[group.Id] = group;
        }

        // Create and start internal nodes
        // In full implementation, would use node registry to instantiate
        // For now, just log
        _logger.LogDebug($"Subflow {Name} has {_definition.Nodes.Count} internal nodes");

        _started = true;
    }

    /// <summary>
    /// Stop the subflow and cleanup
    /// </summary>
    public async Task StopAsync()
    {
        if (!_started)
        {
            return;
        }

        _logger.LogInformation($"Stopping subflow: {Name} ({Id})");

        // Stop all internal nodes in reverse order
        var nodes = _internalNodes.Values.ToList();
        nodes.Reverse();

        foreach (var node in nodes)
        {
            try
            {
                await node.CloseAsync(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error stopping node {node.Id} in subflow {Name}");
            }
        }

        _started = false;
    }

    /// <summary>
    /// Handle input message to subflow
    /// Routes to internal nodes connected to input port
    /// </summary>
    public void HandleInput(FlowMessage message, int inputPort = 0)
    {
        if (!_started)
        {
            _logger.LogWarning($"Cannot handle input - subflow {Name} not started");
            return;
        }

        if (inputPort >= _definition.In.Count)
        {
            _logger.LogWarning($"Invalid input port {inputPort} for subflow {Name}");
            return;
        }

        // Route message to nodes connected to this input port
        var inputConfig = _definition.In[inputPort];
        foreach (var wire in inputConfig.Wires)
        {
            if (_internalNodes.TryGetValue(wire.Id, out var targetNode))
            {
                targetNode.ReceiveInput(message.Clone());
            }
        }
    }

    /// <summary>
    /// Get subflow environment variable
    /// </summary>
    public object? GetEnv(string key)
    {
        return _env.ContainsKey(key) ? _env[key] : null;
    }

    /// <summary>
    /// Add internal node (for runtime use)
    /// </summary>
    internal void AddInternalNode(NodeBase node)
    {
        _internalNodes[node.Id] = node;
    }

    /// <summary>
    /// Get subflow definition
    /// </summary>
    public SubflowDefinition GetDefinition()
    {
        return _definition;
    }

    /// <summary>
    /// Get subflow instance configuration
    /// </summary>
    public SubflowInstanceConfiguration GetInstance()
    {
        return _instance;
    }
}
