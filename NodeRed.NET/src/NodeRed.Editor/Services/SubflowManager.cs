// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/ui/subflow.js
// ============================================================
// TRANSLATION: JavaScript subflow module to C# service
// ============================================================

namespace NodeRed.Editor.Services;

/// <summary>
/// Subflow management service for creating and editing subflows.
/// Translated from RED.subflow module.
/// </summary>
public class SubflowManager
{
    private readonly EditorState _state;
    private readonly History _history;

    public SubflowManager(EditorState state, History history)
    {
        _state = state;
        _history = history;
    }

    /// <summary>
    /// Create a subflow from selected nodes.
    /// Translated from createSubflow() in subflow.js
    /// </summary>
    public Subflow? CreateSubflow(List<FlowNode>? nodes = null)
    {
        var nodesToConvert = nodes?.ToList() ?? new List<FlowNode>();
        
        // Count existing subflows for naming
        var existingSubflows = _state.Nodes.GetAllSubflows();
        var lastIndex = 0;
        foreach (var sf in existingSubflows)
        {
            var match = System.Text.RegularExpressions.Regex.Match(sf.Name ?? "", @"^Subflow (\d+)$");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var index))
            {
                lastIndex = Math.Max(lastIndex, index);
            }
        }
        
        var subflowId = Guid.NewGuid().ToString();
        var subflowName = $"Subflow {lastIndex + 1}";
        var subflow = new Subflow
        {
            Id = subflowId,
            Type = "subflow",
            Name = subflowName,
            In = new List<SubflowPort>(),
            Out = new List<SubflowPort>(),
            Info = "",
            Color = "#DDAA99"
        };
        
        // Add subflow to state
        _state.Nodes.AddSubflow(subflow);
        
        // Create a workspace tab for the subflow
        var workspace = new Workspace
        {
            Id = subflowId,
            Type = "subflow",
            Label = subflow.Name,
            Disabled = false,
            Info = ""
        };
        _state.Workspaces.Add(workspace);
        
        // If we have nodes to convert, move them into the subflow workspace
        if (nodesToConvert.Count > 0)
        {
            var originalWorkspaceId = nodesToConvert.First().Z;
            
            // Calculate center point of selected nodes for positioning the instance node
            var centerX = nodesToConvert.Average(n => n.X);
            var centerY = nodesToConvert.Average(n => n.Y);
            
            // Analyze connections to determine input/output ports
            AnalyzeAndCreatePorts(subflow, nodesToConvert, originalWorkspaceId);
            
            // Move nodes into the subflow workspace
            foreach (var node in nodesToConvert)
            {
                node.Z = subflowId;  // Change workspace to subflow
                node.Dirty = true;
            }
            
            // Create a subflow instance node to replace the selected nodes on the original workspace
            var instanceNode = new FlowNode
            {
                Id = Guid.NewGuid().ToString(),
                Type = $"subflow:{subflowId}",
                Name = subflowName,
                X = centerX,
                Y = centerY,
                Z = originalWorkspaceId,
                Width = 120,
                Height = 60,
                Wires = new List<List<string>>(),
                Dirty = true
            };
            
            // Wire up the instance node to preserve external connections
            RewireSubflowInstance(instanceNode, nodesToConvert, subflow);
            
            // Add the instance node to the original workspace
            _state.Nodes.Add(instanceNode);
            
            // Emit event to update palette with new subflow type
            _state.Events.Emit("registry:node-type-added", subflowId);
        }
        
        // Don't automatically switch to subflow workspace - matches original Node-RED behavior
        // User can click "Edit Subflow Template" button to open it

        // Record history
        _history.Push(new HistoryEvent
        {
            Type = HistoryEventType.CreateSubflow,
            SubflowId = subflow.Id,
            NodeIds = nodesToConvert.Select(n => n.Id).ToList()
        });

        return subflow;
    }
    
    /// <summary>
    /// Analyze node connections to determine subflow input/output ports.
    /// </summary>
    private void AnalyzeAndCreatePorts(Subflow subflow, List<FlowNode> nodes, string originalWorkspace)
    {
        var nodeIds = new HashSet<string>(nodes.Select(n => n.Id));
        var inputIndex = 0;
        var outputIndex = 0;
        
        // Find all nodes in the original workspace
        var allWorkspaceNodes = _state.Nodes.GetNodes().Where(n => n.Z == originalWorkspace).ToList();
        
        // Analyze inputs: connections FROM external nodes TO selected nodes
        foreach (var node in nodes)
        {
            var incomingFromExternal = allWorkspaceNodes
                .Where(n => !nodeIds.Contains(n.Id))
                .Where(n => n.Wires?.Any(wireList => wireList.Any(targetId => targetId == node.Id)) == true);
            
            if (incomingFromExternal.Any() && !subflow.In.Any())
            {
                // Create input port
                subflow.In.Add(new SubflowPort
                {
                    X = 50,
                    Y = 50,
                    Wires = new List<object> 
                    { 
                        new Dictionary<string, string> { { "id", node.Id } } 
                    }
                });
            }
        }
        
        // Analyze outputs: connections FROM selected nodes TO external nodes
        foreach (var node in nodes)
        {
            if (node.Wires != null)
            {
                var outgoingToExternal = node.Wires
                    .SelectMany(wireList => wireList)
                    .Where(targetId => !nodeIds.Contains(targetId))
                    .Distinct();
                
                if (outgoingToExternal.Any())
                {
                    // Create output port for each wire output
                    for (int portIndex = 0; portIndex < node.Wires.Count; portIndex++)
                    {
                        var hasExternalConnection = node.Wires[portIndex].Any(targetId => !nodeIds.Contains(targetId));
                        if (hasExternalConnection)
                        {
                            subflow.Out.Add(new SubflowPort
                            {
                                X = 250,
                                Y = 50 + (outputIndex * 25),
                                Wires = new List<object>
                                {
                                    new Dictionary<string, string> 
                                    { 
                                        { "id", node.Id },
                                        { "port", portIndex.ToString() }
                                    }
                                }
                            });
                            outputIndex++;
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Rewire subflow instance to preserve external connections.
    /// </summary>
    private void RewireSubflowInstance(FlowNode instanceNode, List<FlowNode> originalNodes, Subflow subflow)
    {
        var nodeIds = new HashSet<string>(originalNodes.Select(n => n.Id));
        var originalWorkspace = instanceNode.Z;
        
        // Get all nodes in the original workspace
        var allWorkspaceNodes = _state.Nodes.GetNodes().Where(n => n.Z == originalWorkspace).ToList();
        
        // Setup output wires for instance node (one list per output port)
        for (int i = 0; i < subflow.Out.Count; i++)
        {
            instanceNode.Wires.Add(new List<string>());
        }
        
        // Find all external nodes that were wired TO the selected nodes (inputs)
        foreach (var externalNode in allWorkspaceNodes.Where(n => !nodeIds.Contains(n.Id)))
        {
            if (externalNode.Wires != null)
            {
                for (int outputPort = 0; outputPort < externalNode.Wires.Count; outputPort++)
                {
                    var wires = externalNode.Wires[outputPort];
                    // Replace connections to original nodes with connection to instance
                    for (int wireIndex = 0; wireIndex < wires.Count; wireIndex++)
                    {
                        if (nodeIds.Contains(wires[wireIndex]))
                        {
                            wires[wireIndex] = instanceNode.Id;
                            externalNode.Dirty = true;
                        }
                    }
                    // Remove duplicates
                    externalNode.Wires[outputPort] = wires.Distinct().ToList();
                }
            }
        }
        
        // Find all external nodes that the selected nodes were wired TO (outputs)
        var outputPortIndex = 0;
        foreach (var originalNode in originalNodes)
        {
            if (originalNode.Wires != null)
            {
                foreach (var wireList in originalNode.Wires)
                {
                    var externalTargets = wireList.Where(targetId => !nodeIds.Contains(targetId)).Distinct();
                    if (externalTargets.Any() && outputPortIndex < instanceNode.Wires.Count)
                    {
                        instanceNode.Wires[outputPortIndex].AddRange(externalTargets);
                        outputPortIndex++;
                    }
                }
            }
        }
        
        // Clean up wires in original nodes (remove external connections, keep internal ones)
        foreach (var node in originalNodes)
        {
            if (node.Wires != null)
            {
                for (int i = 0; i < node.Wires.Count; i++)
                {
                    node.Wires[i] = node.Wires[i].Where(targetId => nodeIds.Contains(targetId)).ToList();
                }
            }
        }
    }

    /// <summary>
    /// Create a subflow from selected nodes with a name.
    /// Translated from createSubflow() in subflow.js
    /// </summary>
    public Subflow? CreateSubflow(string name, IEnumerable<FlowNode>? nodes = null)
    {
        // TODO: Full implementation would:
        // 1. Calculate input/output ports from external connections
        // 2. Create subflow workspace
        // 3. Move nodes into subflow workspace
        var subflow = new Subflow
        {
            Id = Guid.NewGuid().ToString(),
            Type = "subflow",
            Name = name
        };

        return subflow;
    }

    /// <summary>
    /// Convert a node to a subflow.
    /// Translated from convertToSubflow() in subflow.js
    /// </summary>
    public Subflow? ConvertToSubflow(FlowNode node)
    {
        if (node == null) return null;
        
        var subflow = new Subflow
        {
            Id = Guid.NewGuid().ToString(),
            Type = "subflow",
            Name = !string.IsNullOrEmpty(node.Name) ? node.Name : node.Type
        };

        // Record history
        _history.Push(new HistoryEvent
        {
            Type = HistoryEventType.CreateSubflow,
            SubflowId = subflow.Id,
            NodeIds = new List<string> { node.Id }
        });

        return subflow;
    }

    /// <summary>
    /// Convert subflow to regular nodes.
    /// Translated from convertToNodes() in subflow.js
    /// Note: Full implementation requires EditorNodes access to get subflow nodes.
    /// </summary>
    public List<FlowNode> ConvertToNodes(Subflow subflow)
    {
        // TODO: Full implementation would clone nodes from subflow workspace to current flow
        var convertedNodes = new List<FlowNode>();
        return convertedNodes;
    }

    /// <summary>
    /// Delete a subflow.
    /// Translated from delete() in subflow.js
    /// Note: Full implementation requires EditorNodes integration.
    /// </summary>
    public void DeleteSubflow(string subflowId)
    {
        // TODO: Full implementation would:
        // 1. Check for instances in use
        // 2. Delete subflow nodes
        // 3. Remove subflow workspace
        // 4. Remove subflow definition
    }

    /// <summary>
    /// Update subflow properties.
    /// Translated from update() in subflow.js
    /// </summary>
    public void UpdateSubflow(Subflow subflow, string? name = null)
    {
        if (name != null) subflow.Name = name;
    }

    /// <summary>
    /// Get subflow instance count.
    /// Note: Full implementation requires EditorNodes access to count subflow instances.
    /// </summary>
    public int GetInstanceCount(string subflowId)
    {
        // TODO: Full implementation would count nodes with type "subflow:{subflowId}"
        return 0;
    }

    /// <summary>
    /// Create an instance of a subflow.
    /// </summary>
    public FlowNode CreateInstance(Subflow subflow, double x, double y, string? z = null)
    {
        var instance = new FlowNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = $"subflow:{subflow.Id}",
            Name = "",
            X = x,
            Y = y,
            Z = z ?? _state.Workspaces.Active(),
            Dirty = true
        };

        return instance;
    }
}
