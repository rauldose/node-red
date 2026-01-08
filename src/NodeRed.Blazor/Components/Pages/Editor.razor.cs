// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Microsoft.AspNetCore.Components;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;
using Syncfusion.Blazor.Diagram;

namespace NodeRed.Blazor.Components.Pages;

public partial class Editor : IDisposable
{
    private SfDiagramComponent Diagram = null!;
    private Workspace CurrentWorkspace = new();
    private int SelectedFlowIndex = 0;
    private FlowNode? SelectedNode;
    private NodeDefinition? DraggedNodeDefinition;
    private bool ShowNodeEditDialog;
    private string EditNodeName = "";

    private DiagramObjectCollection<Node> DiagramNodes = new();
    private DiagramObjectCollection<Connector> DiagramConnectors = new();
    private List<DebugMessage> DebugMessages = new();
    private HashSet<string> ExpandedCategories = new() { "Common", "Function" };

    private Dictionary<string, List<NodeDefinition>> NodeCategories = new();

    private string RuntimeStateText => FlowRuntime.State switch
    {
        FlowState.Running => "Running",
        FlowState.Stopped => "Stopped",
        FlowState.Starting => "Starting...",
        FlowState.Stopping => "Stopping...",
        _ => "Unknown"
    };

    private string RuntimeStateIcon => FlowRuntime.State switch
    {
        FlowState.Running => "e-icons e-circle-check",
        FlowState.Stopped => "e-icons e-circle-close",
        _ => "e-icons e-clock"
    };

    protected override async Task OnInitializedAsync()
    {
        // Load node definitions and organize by category
        var definitions = NodeRegistry.GetAllDefinitions();
        NodeCategories = definitions
            .GroupBy(d => d.Category.ToString())
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load default workspace
        CurrentWorkspace = await FlowStorage.LoadAsync("default") ?? new Workspace
        {
            Id = "default",
            Name = "Default Workspace",
            Flows = new List<Flow> { new() { Label = "Flow 1" } }
        };

        // Subscribe to runtime events
        FlowRuntime.OnDebugMessage += OnDebugMessage;
        FlowRuntime.OnNodeStatusChanged += OnNodeStatusChanged;

        // Load the first flow into the diagram
        LoadFlowToDiagram(CurrentWorkspace.Flows.FirstOrDefault());
    }

    private void LoadFlowToDiagram(Flow? flow)
    {
        DiagramNodes.Clear();
        DiagramConnectors.Clear();

        if (flow == null) return;

        // Create diagram nodes from flow nodes
        foreach (var node in flow.Nodes)
        {
            var definition = NodeRegistry.GetDefinition(node.Type);
            var diagramNode = CreateDiagramNode(node, definition);
            DiagramNodes.Add(diagramNode);
        }

        // Create connectors from wires
        foreach (var sourceNode in flow.Nodes)
        {
            for (int portIndex = 0; portIndex < sourceNode.Wires.Count; portIndex++)
            {
                foreach (var targetId in sourceNode.Wires[portIndex])
                {
                    var connector = new Connector
                    {
                        ID = $"{sourceNode.Id}_{portIndex}_{targetId}",
                        SourceID = sourceNode.Id,
                        TargetID = targetId,
                        SourcePortID = $"out_{portIndex}",
                        TargetPortID = "in_0",
                        Type = ConnectorSegmentType.Bezier,
                        Style = new ShapeStyle { StrokeColor = "#888", StrokeWidth = 2 }
                    };
                    DiagramConnectors.Add(connector);
                }
            }
        }
    }

    private Node CreateDiagramNode(FlowNode flowNode, NodeDefinition? definition)
    {
        var color = definition?.Color ?? "#87A980";
        var displayName = string.IsNullOrEmpty(flowNode.Name) 
            ? definition?.DisplayName ?? flowNode.Type 
            : flowNode.Name;

        var node = new Node
        {
            ID = flowNode.Id,
            OffsetX = flowNode.X,
            OffsetY = flowNode.Y,
            Width = flowNode.Width > 0 ? flowNode.Width : 120,
            Height = flowNode.Height > 0 ? flowNode.Height : 30,
            Style = new ShapeStyle 
            { 
                Fill = color, 
                StrokeColor = "#666",
                StrokeWidth = 1
            },
            Shape = new BasicShape { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 5 },
            Annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation 
                { 
                    Content = displayName,
                    Style = new TextStyle { Color = "#333", FontSize = 12 }
                }
            },
            Ports = CreateNodePorts(definition)
        };

        // Store flow node reference
        node.AdditionalInfo = new Dictionary<string, object> { { "flowNode", flowNode } };

        return node;
    }

    private DiagramObjectCollection<PointPort> CreateNodePorts(NodeDefinition? definition)
    {
        var ports = new DiagramObjectCollection<PointPort>();

        // Input ports
        var inputs = definition?.Inputs ?? 1;
        for (int i = 0; i < inputs; i++)
        {
            ports.Add(new PointPort
            {
                ID = $"in_{i}",
                Offset = new DiagramPoint { X = 0, Y = 0.5 },
                Visibility = PortVisibility.Visible,
                Style = new ShapeStyle { Fill = "#888", StrokeColor = "#666" },
                Width = 10,
                Height = 10,
                Shape = PortShapes.Circle
            });
        }

        // Output ports
        var outputs = definition?.Outputs ?? 1;
        for (int i = 0; i < outputs; i++)
        {
            var yOffset = outputs == 1 ? 0.5 : (i + 1.0) / (outputs + 1);
            ports.Add(new PointPort
            {
                ID = $"out_{i}",
                Offset = new DiagramPoint { X = 1, Y = yOffset },
                Visibility = PortVisibility.Visible,
                Style = new ShapeStyle { Fill = "#888", StrokeColor = "#666" },
                Width = 10,
                Height = 10,
                Shape = PortShapes.Circle
            });
        }

        return ports;
    }

    private void ToggleCategory(string category)
    {
        if (ExpandedCategories.Contains(category))
            ExpandedCategories.Remove(category);
        else
            ExpandedCategories.Add(category);
    }

    private string GetNodeIcon(NodeDefinition node)
    {
        return node.Type switch
        {
            "inject" => "▶",
            "debug" => "🐛",
            "function" => "ƒ",
            "change" => "↔",
            "switch" => "⑂",
            "delay" => "⏱",
            "template" => "📝",
            "comment" => "💬",
            "catch" => "⚠",
            _ => "●"
        };
    }

    private void OnPaletteNodeDragStart(NodeDefinition definition)
    {
        DraggedNodeDefinition = definition;
    }

    private async void OnDragDrop(DropEventArgs args)
    {
        if (DraggedNodeDefinition != null && args.Position != null)
        {
            var flowNode = new FlowNode
            {
                Type = DraggedNodeDefinition.Type,
                X = args.Position.X,
                Y = args.Position.Y,
                FlowId = CurrentWorkspace.Flows[SelectedFlowIndex].Id,
                Wires = Enumerable.Range(0, DraggedNodeDefinition.Outputs).Select(_ => new List<string>()).ToList()
            };

            // Add to current flow
            CurrentWorkspace.Flows[SelectedFlowIndex].Nodes.Add(flowNode);

            // Add to diagram
            var diagramNode = CreateDiagramNode(flowNode, DraggedNodeDefinition);
            await Diagram.AddDiagramElementsAsync(new DiagramObjectCollection<NodeBase> { diagramNode });

            DraggedNodeDefinition = null;
        }
    }

    private void OnNodeCreating(IDiagramObject obj)
    {
        if (obj is Node node)
        {
            node.Style = new ShapeStyle { Fill = "#87A980", StrokeColor = "#666", StrokeWidth = 1 };
        }
    }

    private void OnConnectorCreating(IDiagramObject obj)
    {
        if (obj is Connector connector)
        {
            connector.Style = new ShapeStyle { StrokeColor = "#888", StrokeWidth = 2 };
            connector.Type = ConnectorSegmentType.Bezier;
        }
    }

    private void OnSelectionChanged(SelectionChangedEventArgs args)
    {
        if (args.NewValue?.Count > 0 && args.NewValue[0] is Node node)
        {
            if (node.AdditionalInfo?.TryGetValue("flowNode", out var flowNodeObj) == true && flowNodeObj is FlowNode flowNode)
            {
                SelectedNode = flowNode;
            }
        }
        else
        {
            SelectedNode = null;
        }
    }

    private async Task OnDeployClick()
    {
        // Save workspace
        await FlowStorage.SaveAsync(CurrentWorkspace);

        // Deploy to runtime
        await FlowRuntime.DeployAsync(CurrentWorkspace);

        // Start if not running
        if (FlowRuntime.State != FlowState.Running)
        {
            await FlowRuntime.StartAsync();
        }

        StateHasChanged();
    }

    private void OnImportClick()
    {
        // TODO: Implement import dialog
    }

    private async Task OnExportClick()
    {
        var json = await FlowStorage.ExportAsync(CurrentWorkspace);
        // TODO: Show export dialog with JSON
    }

    private void AddNewFlow()
    {
        var newFlow = new Flow
        {
            Label = $"Flow {CurrentWorkspace.Flows.Count + 1}",
            Order = CurrentWorkspace.Flows.Count
        };
        CurrentWorkspace.Flows.Add(newFlow);
        SelectedFlowIndex = CurrentWorkspace.Flows.Count - 1;
        LoadFlowToDiagram(newFlow);
    }

    private void OnDebugMessage(DebugMessage message)
    {
        DebugMessages.Add(message);
        InvokeAsync(StateHasChanged);
    }

    private void OnNodeStatusChanged(string nodeId, NodeStatus status)
    {
        // Update node appearance based on status
        InvokeAsync(StateHasChanged);
    }

    private void ClearDebugMessages()
    {
        DebugMessages.Clear();
    }

    private string FormatDebugData(object? data)
    {
        if (data == null) return "null";
        if (data is string s) return s;
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }
        catch
        {
            return data.ToString() ?? "null";
        }
    }

    private void CloseNodeEditDialog()
    {
        ShowNodeEditDialog = false;
    }

    private void SaveNodeEdit()
    {
        if (SelectedNode != null)
        {
            SelectedNode.Name = EditNodeName;
        }
        ShowNodeEditDialog = false;
    }

    public void Dispose()
    {
        FlowRuntime.OnDebugMessage -= OnDebugMessage;
        FlowRuntime.OnNodeStatusChanged -= OnNodeStatusChanged;
    }
}
