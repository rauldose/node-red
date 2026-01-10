// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Services;

/// <summary>
/// Service for validating flow configurations before deployment.
/// Based on packages/node_modules/@node-red/runtime/lib/flows/util.js
/// </summary>
public class FlowValidator : IFlowValidator
{
    private readonly INodeRegistry _nodeRegistry;

    public FlowValidator(INodeRegistry nodeRegistry)
    {
        _nodeRegistry = nodeRegistry;
    }

    /// <inheritdoc />
    public ValidationResult ValidateWorkspace(Workspace workspace)
    {
        var result = new ValidationResult();
        var availableNodes = _nodeRegistry.GetAllDefinitions().Select(d => d.Type).ToHashSet();

        // Validate all flows
        foreach (var flow in workspace.Flows)
        {
            var flowResult = ValidateFlow(flow, availableNodes);
            result.Merge(flowResult);
        }

        // Validate config nodes
        foreach (var configNode in workspace.ConfigNodes)
        {
            var nodeResult = ValidateNode(configNode, _nodeRegistry.GetDefinition(configNode.Type));
            result.Merge(nodeResult);
        }

        // Validate subflow definitions
        foreach (var subflow in workspace.Subflows)
        {
            var subflowResult = ValidateSubflow(subflow, availableNodes);
            result.Merge(subflowResult);
        }

        // Check for duplicate IDs
        var allIds = new HashSet<string>();
        foreach (var flow in workspace.Flows)
        {
            if (!allIds.Add(flow.Id))
            {
                result.Errors.Add(new ValidationError
                {
                    Message = $"Duplicate flow ID: {flow.Id}",
                    NodeId = flow.Id,
                    Code = "duplicate_id"
                });
            }
            foreach (var node in flow.Nodes)
            {
                if (!allIds.Add(node.Id))
                {
                    result.Errors.Add(new ValidationError
                    {
                        Message = $"Duplicate node ID: {node.Id}",
                        NodeId = node.Id,
                        Code = "duplicate_id"
                    });
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public ValidationResult ValidateFlow(Flow flow, IEnumerable<string> availableNodes)
    {
        var result = new ValidationResult();
        var availableNodeSet = availableNodes.ToHashSet();
        var nodeIds = flow.Nodes.Select(n => n.Id).ToHashSet();

        // Validate each node
        foreach (var node in flow.Nodes)
        {
            // Check if node type exists (unless it's a subflow type)
            if (!node.Type.StartsWith("subflow:") && !availableNodeSet.Contains(node.Type))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = $"Unknown node type: {node.Type}",
                    NodeId = node.Id,
                    Property = "type"
                });
            }

            var nodeDefinition = _nodeRegistry.GetDefinition(node.Type);
            var nodeResult = ValidateNode(node, nodeDefinition);
            result.Merge(nodeResult);
        }

        // Validate wires
        var wireResult = ValidateWires(flow);
        result.Merge(wireResult);

        return result;
    }

    /// <inheritdoc />
    public ValidationResult ValidateNode(FlowNode node, NodeDefinition? nodeDefinition)
    {
        var result = new ValidationResult();

        // Validate required properties
        if (string.IsNullOrEmpty(node.Id))
        {
            result.Errors.Add(new ValidationError
            {
                Message = "Node must have an ID",
                NodeId = node.Id,
                Property = "id",
                Code = "missing_id"
            });
        }

        if (string.IsNullOrEmpty(node.Type))
        {
            result.Errors.Add(new ValidationError
            {
                Message = "Node must have a type",
                NodeId = node.Id,
                Property = "type",
                Code = "missing_type"
            });
        }

        // If we have a definition, validate against it
        if (nodeDefinition != null)
        {
            // Check for required properties from the property definitions
            foreach (var propDef in nodeDefinition.Properties)
            {
                if (propDef.Required && !node.Config.ContainsKey(propDef.Name))
                {
                    result.Warnings.Add(new ValidationWarning
                    {
                        Message = $"Missing recommended property: {propDef.Name}",
                        NodeId = node.Id,
                        Property = propDef.Name
                    });
                }
            }
        }

        // Validate environment variable references
        var envVarResult = ValidateEnvVarReferences(node);
        result.Merge(envVarResult);

        return result;
    }

    /// <inheritdoc />
    public ValidationResult ValidateWires(Flow flow)
    {
        var result = new ValidationResult();
        var nodeIds = flow.Nodes.Select(n => n.Id).ToHashSet();

        foreach (var node in flow.Nodes)
        {
            if (node.Wires == null) continue;

            for (int portIndex = 0; portIndex < node.Wires.Count; portIndex++)
            {
                var wires = node.Wires[portIndex];
                if (wires == null) continue;

                foreach (var targetId in wires)
                {
                    if (string.IsNullOrEmpty(targetId))
                    {
                        result.Warnings.Add(new ValidationWarning
                        {
                            Message = $"Empty wire target on port {portIndex}",
                            NodeId = node.Id,
                            Property = $"wires[{portIndex}]"
                        });
                        continue;
                    }

                    if (!nodeIds.Contains(targetId))
                    {
                        result.Errors.Add(new ValidationError
                        {
                            Message = $"Wire references non-existent node: {targetId}",
                            NodeId = node.Id,
                            Property = $"wires[{portIndex}]",
                            Code = "invalid_wire_target"
                        });
                    }
                }
            }
        }

        // Check for circular dependencies (simple check - could be more comprehensive)
        var circularResult = CheckCircularDependencies(flow);
        result.Merge(circularResult);

        return result;
    }

    /// <inheritdoc />
    public bool DiffNodes(FlowNode? oldNode, FlowNode? newNode)
    {
        if (oldNode == null) return true;
        if (newNode == null) return true;

        // Ignore position and wires for diff detection (like the JS implementation)
        var ignoreKeys = new HashSet<string> { "x", "y", "wires" };
        var groupIgnoreKeys = new HashSet<string> { "x", "y", "wires", "nodes", "style", "w", "h" };

        var keysToIgnore = oldNode.Type == "group" ? groupIgnoreKeys : ignoreKeys;

        // Compare all properties except ignored ones
        if (oldNode.Id != newNode.Id) return true;
        if (oldNode.Type != newNode.Type) return true;
        if (oldNode.Name != newNode.Name) return true;
        if (oldNode.Disabled != newNode.Disabled) return true;
        if (oldNode.FlowId != newNode.FlowId) return true;

        // Compare config properties
        var oldKeys = oldNode.Config.Keys.Where(k => !keysToIgnore.Contains(k)).ToHashSet();
        var newKeys = newNode.Config.Keys.Where(k => !keysToIgnore.Contains(k)).ToHashSet();

        if (!oldKeys.SetEquals(newKeys)) return true;

        foreach (var key in oldKeys)
        {
            var oldValue = oldNode.Config[key];
            var newValue = newNode.Config[key];

            if (!CompareValues(oldValue, newValue)) return true;
        }

        return false;
    }

    /// <summary>
    /// Validates a subflow definition.
    /// </summary>
    private ValidationResult ValidateSubflow(Subflow subflow, HashSet<string> availableNodes)
    {
        var result = new ValidationResult();

        if (string.IsNullOrEmpty(subflow.Id))
        {
            result.Errors.Add(new ValidationError
            {
                Message = "Subflow must have an ID",
                Code = "missing_id"
            });
        }

        if (string.IsNullOrEmpty(subflow.Name))
        {
            result.Warnings.Add(new ValidationWarning
            {
                Message = "Subflow should have a name"
            });
        }

        // Validate nodes within the subflow
        var nodeIds = subflow.Nodes.Select(n => n.Id).ToHashSet();
        foreach (var node in subflow.Nodes)
        {
            if (!node.Type.StartsWith("subflow:") && !availableNodes.Contains(node.Type))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = $"Unknown node type in subflow: {node.Type}",
                    NodeId = node.Id
                });
            }
        }

        // Validate input/output ports reference valid nodes
        foreach (var inPort in subflow.In)
        {
            foreach (var wire in inPort.Wires)
            {
                if (!nodeIds.Contains(wire.Id))
                {
                    result.Errors.Add(new ValidationError
                    {
                        Message = $"Subflow input references non-existent node: {wire.Id}",
                        Code = "invalid_subflow_input"
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Validates environment variable references in node configuration.
    /// </summary>
    private ValidationResult ValidateEnvVarReferences(FlowNode node)
    {
        var result = new ValidationResult();
        var envVarPattern = new System.Text.RegularExpressions.Regex(@"\$\{(\S+)\}|\$\((\S+)\)");

        foreach (var kvp in node.Config)
        {
            if (kvp.Value is string strValue)
            {
                var matches = envVarPattern.Matches(strValue);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    // Just note that env vars are used - we can't validate they exist at design time
                    // since they may be set at runtime
                    var varName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                    if (string.IsNullOrEmpty(varName))
                    {
                        result.Warnings.Add(new ValidationWarning
                        {
                            Message = "Empty environment variable reference",
                            NodeId = node.Id,
                            Property = kvp.Key
                        });
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Simple circular dependency check.
    /// </summary>
    private ValidationResult CheckCircularDependencies(Flow flow)
    {
        var result = new ValidationResult();
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        
        // Build node map, handling duplicates gracefully
        var nodeMap = new Dictionary<string, FlowNode>();
        foreach (var node in flow.Nodes)
        {
            if (!nodeMap.ContainsKey(node.Id))
            {
                nodeMap[node.Id] = node;
            }
            // If duplicate, skip - this is caught by the duplicate ID check
        }

        foreach (var node in flow.Nodes)
        {
            if (HasCycle(node.Id, nodeMap, visited, recursionStack))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = "Possible circular dependency detected in flow",
                    NodeId = node.Id
                });
                break; // Only report once
            }
        }

        return result;
    }

    private bool HasCycle(string nodeId, Dictionary<string, FlowNode> nodeMap, HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(nodeId)) return true;
        if (visited.Contains(nodeId)) return false;

        visited.Add(nodeId);
        recursionStack.Add(nodeId);

        if (nodeMap.TryGetValue(nodeId, out var node) && node.Wires != null)
        {
            foreach (var wires in node.Wires)
            {
                foreach (var targetId in wires)
                {
                    if (HasCycle(targetId, nodeMap, visited, recursionStack))
                    {
                        return true;
                    }
                }
            }
        }

        recursionStack.Remove(nodeId);
        return false;
    }

    /// <summary>
    /// Deep comparison of two values.
    /// </summary>
    private bool CompareValues(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.GetType() != b.GetType()) return false;

        if (a is IDictionary<string, object?> dictA && b is IDictionary<string, object?> dictB)
        {
            if (dictA.Count != dictB.Count) return false;
            foreach (var key in dictA.Keys)
            {
                if (!dictB.ContainsKey(key) || !CompareValues(dictA[key], dictB[key]))
                {
                    return false;
                }
            }
            return true;
        }

        if (a is IList<object?> listA && b is IList<object?> listB)
        {
            if (listA.Count != listB.Count) return false;
            for (int i = 0; i < listA.Count; i++)
            {
                if (!CompareValues(listA[i], listB[i]))
                {
                    return false;
                }
            }
            return true;
        }

        return a.Equals(b);
    }
}
