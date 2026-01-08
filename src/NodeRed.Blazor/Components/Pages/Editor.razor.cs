// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;
using Syncfusion.Blazor.Diagram;
using System.Collections.ObjectModel;

namespace NodeRed.Blazor.Components.Pages;

public partial class Editor : IDisposable
{
    // Diagram reference
    public SfDiagramComponent? DiagramInstance { get; set; }
    
    // Diagram collections
    private DiagramObjectCollection<Node>? DiagramNodes { get; set; } = new DiagramObjectCollection<Node>();
    private DiagramObjectCollection<Connector>? DiagramConnectors { get; set; } = new DiagramObjectCollection<Connector>();
    
    // Grid line intervals
    public double[]? GridLineIntervals { get; set; }
    
    // Application state
    private Workspace CurrentWorkspace = new();
    private FlowNode? SelectedFlowNode;
    private NodeDefinition? SelectedPaletteNode;
    private List<DebugMessage> DebugMessages = new();
    private int ConnectorCount = 0;
    private int NodeCount = 0;
    
    // Node categories for palette
    private Dictionary<string, List<NodeDefinition>> NodeCategories = new();
    private HashSet<string> ExpandedCategories = new() { "Common", "Function" };

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

    protected override void OnInitialized()
    {
        GridLineIntervals = new double[] {
            1, 9, 0.25, 9.75, 0.25, 9.75, 0.25, 9.75, 0.25, 9.75, 0.25, 9.75, 0.25, 9.75, 0.25, 9.75, 0.25, 9.75, 0.25, 9.75
        };
        
        // Load node definitions and organize by category
        var definitions = NodeRegistry.GetAllDefinitions();
        NodeCategories = definitions
            .GroupBy(d => d.Category.ToString())
            .ToDictionary(g => g.Key, g => g.ToList());
        
        InitDiagramModel();
        
        // Subscribe to runtime events
        FlowRuntime.OnDebugMessage += OnDebugMessage;
        FlowRuntime.OnNodeStatusChanged += OnNodeStatusChanged;
    }

    private void InitDiagramModel()
    {
        // Create sample flow nodes
        CreateNode("inject1", 150, 100, "inject", "Timestamp", "#DE7479");
        CreateNode("function1", 350, 100, "function", "Process", "#F0C483");
        CreateNode("debug1", 550, 100, "debug", "Output", "#87A980");
        
        // Create connectors
        CreateConnector("inject1", "function1");
        CreateConnector("function1", "debug1");
    }

    private void CreateNode(string id, double x, double y, string nodeType, string label, string color)
    {
        var node = new Node()
        {
            ID = id,
            OffsetX = x,
            OffsetY = y,
            Width = 120,
            Height = 30,
            Ports = CreatePorts(),
            Style = new ShapeStyle { Fill = color, StrokeColor = "#666666", StrokeWidth = 1 },
            Shape = new BasicShape() { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 5 },
            Annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation
                {
                    Content = label,
                    Style = new TextStyle() { Color = "#333333", FontSize = 12 }
                }
            },
            // Store node type for later retrieval
            AdditionalInfo = new Dictionary<string, object> { { "nodeType", nodeType } }
        };
        DiagramNodes!.Add(node);
        NodeCount++; // Track node count dynamically
    }

    private DiagramObjectCollection<PointPort> CreatePorts()
    {
        var ports = new DiagramObjectCollection<PointPort>();
        ports.Add(new PointPort()
        {
            ID = "port1",
            Shape = PortShapes.Circle,
            Offset = new DiagramPoint() { X = 0, Y = 0.5 },
            Visibility = PortVisibility.Visible,
            Style = new ShapeStyle { Fill = "#888", StrokeColor = "#666" },
            Width = 10,
            Height = 10
        });
        ports.Add(new PointPort()
        {
            ID = "port2",
            Shape = PortShapes.Circle,
            Offset = new DiagramPoint() { X = 1, Y = 0.5 },
            Visibility = PortVisibility.Visible,
            Style = new ShapeStyle { Fill = "#888", StrokeColor = "#666" },
            Width = 10,
            Height = 10
        });
        return ports;
    }

    private void CreateConnector(string sourceId, string targetId)
    {
        var connector = new Connector()
        {
            ID = $"connector{++ConnectorCount}",
            SourceID = sourceId,
            TargetID = targetId,
            SourcePortID = "port2",
            TargetPortID = "port1",
            Type = ConnectorSegmentType.Bezier,
            Style = new ShapeStyle { StrokeColor = "#888", StrokeWidth = 2 },
            TargetDecorator = new DecoratorSettings { Shape = DecoratorShape.Arrow }
        };
        DiagramConnectors!.Add(connector);
    }

    private void OnCreated()
    {
        if (DiagramInstance?.Nodes?.Count > 0)
        {
            DiagramInstance.Select(new ObservableCollection<IDiagramObject>() { DiagramInstance.Nodes[0] });
            FitOptions options = new FitOptions() { Mode = FitMode.Both, Region = DiagramRegion.Content };
            DiagramInstance.FitToPage(options);
        }
    }

    private void OnNodeCreating(IDiagramObject obj)
    {
        if (obj is Node node)
        {
            node.Style ??= new ShapeStyle();
            node.Style.StrokeWidth = 1;
            node.Style.StrokeColor = "#666666";
        }
    }

    private void OnConnectorCreating(IDiagramObject obj)
    {
        if (obj is Connector connector)
        {
            connector.Style ??= new ShapeStyle();
            connector.Style.StrokeColor = "#888";
            connector.Style.StrokeWidth = 2;
            connector.Type = ConnectorSegmentType.Bezier;
        }
    }

    private void OnSelectionChanged(Syncfusion.Blazor.Diagram.SelectionChangedEventArgs args)
    {
        if (args.NewValue?.Count > 0 && args.NewValue[0] is Node node)
        {
            // Get node type from AdditionalInfo if available
            var nodeType = node.AdditionalInfo?.TryGetValue("nodeType", out var typeObj) == true 
                ? typeObj as string ?? node.ID ?? "" 
                : node.ID ?? "";
            SelectedFlowNode = new FlowNode { Id = node.ID ?? "", Type = nodeType };
        }
        else
        {
            SelectedFlowNode = null;
        }
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
            "template" => "��",
            "comment" => "💬",
            "catch" => "⚠",
            _ => "●"
        };
    }

    private void OnPaletteNodeClick(NodeDefinition definition)
    {
        if (SelectedPaletteNode?.Type == definition.Type)
        {
            SelectedPaletteNode = null;
        }
        else
        {
            SelectedPaletteNode = definition;
        }
    }

    private void CancelNodePlacement()
    {
        SelectedPaletteNode = null;
    }

    private async void OnDiagramClick(ClickEventArgs args)
    {
        if (SelectedPaletteNode != null && args.Position != null)
        {
            await AddNodeAtPosition(args.Position.X, args.Position.Y);
        }
    }

    private async Task AddNodeAtPosition(double x, double y)
    {
        if (SelectedPaletteNode == null || DiagramInstance == null) return;

        var nodeId = $"{SelectedPaletteNode.Type}{++NodeCount}";
        var node = new Node()
        {
            ID = nodeId,
            OffsetX = x,
            OffsetY = y,
            Width = 120,
            Height = 30,
            Ports = CreatePorts(),
            Style = new ShapeStyle { Fill = SelectedPaletteNode.Color, StrokeColor = "#666666", StrokeWidth = 1 },
            Shape = new BasicShape() { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 5 },
            Annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation
                {
                    Content = SelectedPaletteNode.DisplayName,
                    Style = new TextStyle() { Color = "#333333", FontSize = 12 }
                }
            },
            // Store node type for later retrieval
            AdditionalInfo = new Dictionary<string, object> { { "nodeType", SelectedPaletteNode.Type } }
        };

        await DiagramInstance.AddDiagramElementsAsync(new DiagramObjectCollection<NodeBase> { node });
        
        SelectedPaletteNode = null;
        StateHasChanged();
    }

    private async Task ToolbarClick(Syncfusion.Blazor.Navigations.ClickEventArgs args)
    {
        var value = args.Item.TooltipText;
        switch (value)
        {
            case "Deploy":
                await OnDeployClick();
                break;
            case "Import":
                OnImportClick();
                break;
            case "Export":
                await OnExportClick();
                break;
        }
    }

    private async Task OnDeployClick()
    {
        await FlowStorage.SaveAsync(CurrentWorkspace);
        await FlowRuntime.DeployAsync(CurrentWorkspace);

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

    private void OnDebugMessage(DebugMessage message)
    {
        DebugMessages.Add(message);
        InvokeAsync(StateHasChanged);
    }

    private void OnNodeStatusChanged(string nodeId, NodeStatus status)
    {
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

    public void Dispose()
    {
        FlowRuntime.OnDebugMessage -= OnDebugMessage;
        FlowRuntime.OnNodeStatusChanged -= OnNodeStatusChanged;
    }
}
