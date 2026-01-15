// ============================================================
// INSPIRED BY: @node-red/runtime/lib/flows/Flow.js
// REFERENCE: NODE-RED-ARCHITECTURE-ANALYSIS.md - Flow Engine section
// ============================================================
// Flow execution engine managing node instances and message routing
// ============================================================

/*!
 * Original work Copyright JS Foundation and other contributors, http://js.foundation
 * Modified work Copyright 2026 NodeRed.NET Contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 */

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace NodeRed.Runtime;

/// <summary>
/// Flow configuration from JSON
/// Maps to: Flow configuration in Node-RED
/// </summary>
public class FlowConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public List<NodeConfiguration> Nodes { get; set; } = new();
    public Dictionary<string, object>? Env { get; set; }
}

/// <summary>
/// Flow execution engine
/// Maps to: Flow class in @node-red/runtime/lib/flows/Flow.js
/// Reference: NODE-RED-ARCHITECTURE-ANALYSIS.md - Flow Engine
/// </summary>
public class FlowEngine
{
    private readonly ILogger<FlowEngine> _logger;
    private readonly ConcurrentDictionary<string, NodeBase> _nodes = new();
    private readonly ConcurrentDictionary<string, INodeContext> _flowContexts = new();
    private readonly INodeContext _globalContext = new InMemoryNodeContext();
    private bool _isStarted = false;

    public FlowEngine(ILogger<FlowEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Load flows from configuration
    /// Maps to: Flow initialization in Node-RED
    /// </summary>
    public async Task LoadFlowsAsync(List<FlowConfiguration> flows)
    {
        _logger.LogInformation("Loading flows...");

        foreach (var flow in flows)
        {
            _logger.LogInformation($"Loading flow: {flow.Label} ({flow.Id})");
            
            // Create flow context
            var flowContext = new InMemoryNodeContext();
            _flowContexts[flow.Id] = flowContext;

            // Create nodes (simplified - would need node factory/registry in full impl)
            foreach (var nodeConfig in flow.Nodes)
            {
                // This would normally use the registry to create the right node type
                _logger.LogDebug($"Would create node: {nodeConfig.Type} ({nodeConfig.Id})");
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Start the flow engine
    /// Maps to: Flow.start() in Node-RED
    /// </summary>
    public async Task StartAsync()
    {
        if (_isStarted)
        {
            _logger.LogWarning("Flow engine already started");
            return;
        }

        _logger.LogInformation("Starting flow engine...");

        // Start all nodes
        foreach (var node in _nodes.Values)
        {
            try
            {
                _logger.LogDebug($"Starting node: {node.Type} ({node.Id})");
                // Nodes are started via their constructors and event handlers
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error starting node {node.Id}");
            }
        }

        _isStarted = true;
        _logger.LogInformation("Flow engine started");
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stop the flow engine
    /// Maps to: Flow.stop() in Node-RED
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isStarted)
        {
            return;
        }

        _logger.LogInformation("Stopping flow engine...");

        // Stop all nodes in reverse order
        var nodeList = _nodes.Values.ToList();
        nodeList.Reverse();

        foreach (var node in nodeList)
        {
            try
            {
                _logger.LogDebug($"Stopping node: {node.Type} ({node.Id})");
                await node.CloseAsync(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error stopping node {node.Id}");
            }
        }

        _isStarted = false;
        _logger.LogInformation("Flow engine stopped");
    }

    /// <summary>
    /// Add a node to the flow
    /// </summary>
    public void AddNode(NodeBase node)
    {
        _nodes[node.Id] = node;
        
        // Wire up message routing
        node.MessageSent += (sender, message) => RouteMessage(node, message);
        node.MessagesSent += (sender, messages) => RouteMessages(node, messages);
        
        _logger.LogDebug($"Added node: {node.Type} ({node.Id})");
    }

    /// <summary>
    /// Route a message from a node to its wired nodes
    /// Implements Node-RED's message routing logic
    /// </summary>
    private void RouteMessage(NodeBase sourceNode, FlowMessage message)
    {
        if (sourceNode.Wires == null || sourceNode.Wires.Length == 0)
        {
            return;
        }

        // Node-RED clones messages when sending to multiple nodes
        var firstOutput = sourceNode.Wires[0];
        foreach (var targetNodeId in firstOutput)
        {
            if (_nodes.TryGetValue(targetNodeId, out var targetNode))
            {
                // Clone message for each target (except the last)
                var msgToSend = message;
                if (firstOutput.Last() != targetNodeId)
                {
                    msgToSend = message.Clone();
                }
                
                targetNode.ReceiveInput(msgToSend);
            }
        }
    }

    /// <summary>
    /// Route messages from multiple outputs
    /// </summary>
    private void RouteMessages(NodeBase sourceNode, FlowMessage?[] messages)
    {
        if (sourceNode.Wires == null || sourceNode.Wires.Length == 0)
        {
            return;
        }

        for (int outputIndex = 0; outputIndex < Math.Min(messages.Length, sourceNode.Wires.Length); outputIndex++)
        {
            var message = messages[outputIndex];
            if (message == null)
                continue;

            var outputWires = sourceNode.Wires[outputIndex];
            foreach (var targetNodeId in outputWires)
            {
                if (_nodes.TryGetValue(targetNodeId, out var targetNode))
                {
                    var msgToSend = outputWires.Last() != targetNodeId ? message.Clone() : message;
                    targetNode.ReceiveInput(msgToSend);
                }
            }
        }
    }

    /// <summary>
    /// Get global context
    /// </summary>
    public INodeContext GetGlobalContext() => _globalContext;

    /// <summary>
    /// Get flow context
    /// </summary>
    public INodeContext GetFlowContext(string flowId)
    {
        return _flowContexts.GetOrAdd(flowId, _ => new InMemoryNodeContext());
    }
}
