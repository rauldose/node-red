// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;
using Syncfusion.Blazor.Diagram;
using Syncfusion.Blazor.Diagram.SymbolPalette;
using Syncfusion.Blazor.Inputs;
using System.Collections.ObjectModel;

namespace NodeRed.Blazor.Components.Pages;

public partial class Editor : IDisposable
{
    // Diagram references
    public SfDiagramComponent? DiagramInstance { get; set; }
    public SfSymbolPaletteComponent? PaletteInstance { get; set; }
    
    // Selection settings - hide resize handles
    public DiagramSelectionSettings SelectionSettings { get; set; } = new DiagramSelectionSettings()
    {
        // Remove resize and rotate handles from selection
        Constraints = SelectorConstraints.All & ~SelectorConstraints.ResizeAll & ~SelectorConstraints.Rotate
    };
    
    // Symbol palette settings
    public DiagramSize? SymbolPreview { get; set; }
    public SymbolMargin? SymbolMargin { get; set; } = new SymbolMargin { Left = 5, Right = 5, Top = 5, Bottom = 5 };
    
    // Diagram collections
    private DiagramObjectCollection<Node>? DiagramNodes { get; set; } = new DiagramObjectCollection<Node>();
    private DiagramObjectCollection<Connector>? DiagramConnectors { get; set; } = new DiagramObjectCollection<Connector>();
    
    // Palette collections
    private DiagramObjectCollection<Palette>? DiagramPalettes { get; set; } = new DiagramObjectCollection<Palette>();
    private DiagramObjectCollection<NodeBase>? CommonNodes { get; set; } = new DiagramObjectCollection<NodeBase>();
    private DiagramObjectCollection<NodeBase>? FunctionNodes { get; set; } = new DiagramObjectCollection<NodeBase>();
    
    // Drawing object
    private IDiagramObject? DiagramDrawingObject { get; set; }
    private DiagramInteractions DiagramTool { get; set; } = DiagramInteractions.Default;
    
    // Grid line intervals
    public double[]? GridLineIntervals { get; set; }
    
    // Toolbar state
    public string ZoomItemDropdownContent { get; set; } = "100%";
    public bool IsEnablePasteButton { get; set; } = true;
    public bool IsEnableCutButton { get; set; } = true;
    public bool IsEnableCopyButton { get; set; } = true;
    public bool IsEnableUndoButton { get; set; } = true;
    public bool IsEnableRedoButton { get; set; } = true;
    public bool IsDeleteDisable { get; set; } = true;
    
    // Undo/Redo stacks
    private List<string> undoStack = new List<string>();
    private List<string> redoStack = new List<string>();
    
    // Flow management
    private List<FlowTab> Flows = new List<FlowTab>();
    private string CurrentFlowId = "flow1";
    private int FlowCounter = 1;
    
    // Selection state
    private Node? SelectedDiagramNode;
    private string SelectedNodeName = "";
    private string SelectedNodeFunctionCode = "";
    private int SelectedSidebarTab = 0;
    
    // Position properties for property panel binding
    private double PositionX
    {
        get => SelectedDiagramNode?.OffsetX ?? 0;
        set { if (SelectedDiagramNode != null) SelectedDiagramNode.OffsetX = value; }
    }
    
    private double PositionY
    {
        get => SelectedDiagramNode?.OffsetY ?? 0;
        set { if (SelectedDiagramNode != null) SelectedDiagramNode.OffsetY = value; }
    }
    
    // Debug messages
    private List<DebugMessage> DebugMessages = new();
    
    // Node counter for unique IDs
    private int NodeCount = 0;
    private int ConnectorCount = 0;
    
    // Default node constraints: disable resize and rotate for Node-RED style nodes
    // Use explicit constraints instead of removing from Default to ensure no resize handles appear
    private static readonly NodeConstraints DefaultNodeConstraints = 
        NodeConstraints.Select | NodeConstraints.Drag | NodeConstraints.Delete | 
        NodeConstraints.InConnect | NodeConstraints.OutConnect | 
        NodeConstraints.PointerEvents | NodeConstraints.AllowDrop;
    
    // Application state
    private Workspace CurrentWorkspace = new();

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
        
        // Initialize flows
        Flows.Add(new FlowTab { Id = "flow1", Label = "Flow 1" });
        
        // Initialize symbol preview
        SymbolPreview = new DiagramSize { Width = 80, Height = 80 };
        
        // Initialize palette
        InitPaletteModel();
        
        // Initialize diagram with sample flow
        InitDiagramModel();
        
        // Subscribe to runtime events
        FlowRuntime.OnDebugMessage += OnDebugMessage;
        FlowRuntime.OnNodeStatusChanged += OnNodeStatusChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender && PaletteInstance != null && DiagramInstance != null)
        {
            PaletteInstance.Targets = new DiagramObjectCollection<SfDiagramComponent?>
            {
                DiagramInstance
            };
        }
    }
    
    // Get symbol info for palette - displays description below each symbol
    private SymbolInfo GetSymbolInfo(IDiagramObject symbol)
    {
        var info = new SymbolInfo
        {
            Fit = true
        };
        
        // Show the node type as description below the symbol
        if (symbol is Node node && node.AdditionalInfo?.TryGetValue("nodeType", out var typeObj) == true)
        {
            info.Description = new SymbolDescription
            {
                Text = typeObj as string ?? node.ID ?? "Unknown",
                Style = new TextStyle { FontSize = 12, Color = "#333" }
            };
        }
        
        return info;
    }

    private void InitPaletteModel()
    {
        CommonNodes = new DiagramObjectCollection<NodeBase>();
        FunctionNodes = new DiagramObjectCollection<NodeBase>();
        
        // Common nodes
        CreatePaletteNode(CommonNodes, "inject", "inject", "#DE7479", "▶");
        CreatePaletteNode(CommonNodes, "debug", "debug", "#87A980", "🐛");
        CreatePaletteNode(CommonNodes, "comment", "comment", "#F0F0F0", "💬");
        CreatePaletteNode(CommonNodes, "catch", "catch", "#E3B881", "⚠");
        
        // Function nodes
        CreatePaletteNode(FunctionNodes, "function", "function", "#F0C483", "ƒ");
        CreatePaletteNode(FunctionNodes, "change", "change", "#E6E39D", "↔");
        CreatePaletteNode(FunctionNodes, "switch", "switch", "#E2D96E", "⑂");
        CreatePaletteNode(FunctionNodes, "delay", "delay", "#E6C4E0", "⏱");
        CreatePaletteNode(FunctionNodes, "template", "template", "#CC9966", "📝");
        
        DiagramPalettes = new DiagramObjectCollection<Palette>()
        {
            new Palette() { Symbols = CommonNodes, Title = "Common", ID = "Common", IconCss = "e-icons e-circle", IsExpanded = true },
            new Palette() { Symbols = FunctionNodes, Title = "Function", ID = "Function", IconCss = "e-icons e-function", IsExpanded = true },
        };
    }

    private void CreatePaletteNode(DiagramObjectCollection<NodeBase> collection, string id, string nodeType, string color, string icon)
    {
        // Create ports for palette node - these will be cloned when dragged
        // port1 = input port (left side) - accepts incoming connections
        // port2 = output port (right side) - can draw outgoing connections
        var palettePorts = new DiagramObjectCollection<PointPort>();
        
        // Input port (left side) - accepts incoming connections, cannot draw from it
        palettePorts.Add(new PointPort()
        {
            ID = "port1",
            Shape = PortShapes.Circle,
            Offset = new DiagramPoint() { X = 0, Y = 0.5 },
            Visibility = PortVisibility.Visible,
            Style = new ShapeStyle { Fill = "#888", StrokeColor = "#666" },
            Width = 8,
            Height = 8,
            Constraints = PortConstraints.InConnect
        });
        
        // Output port (right side) - can draw connections from here
        palettePorts.Add(new PointPort()
        {
            ID = "port2",
            Shape = PortShapes.Circle,
            Offset = new DiagramPoint() { X = 1, Y = 0.5 },
            Visibility = PortVisibility.Visible,
            Style = new ShapeStyle { Fill = "#888", StrokeColor = "#666" },
            Width = 8,
            Height = 8,
            Constraints = PortConstraints.Draw | PortConstraints.OutConnect
        });
        
        var node = new Node()
        {
            ID = id,
            Width = 100,
            Height = 30,
            Style = new ShapeStyle { Fill = color, StrokeColor = "#666666", StrokeWidth = 1 },
            Shape = new BasicShape() { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 5 },
            Ports = palettePorts,
            Constraints = DefaultNodeConstraints,
            Annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation
                {
                    Content = $"{icon} {nodeType}",
                    Style = new TextStyle() { Color = "#333333", FontSize = 11 }
                }
            },
            // Tooltip to show node type on hover in palette
            Tooltip = new DiagramTooltip() { Content = nodeType },
            AdditionalInfo = new Dictionary<string, object> { { "nodeType", nodeType }, { "color", color } }
        };
        collection.Add(node);
    }

    private void InitDiagramModel()
    {
        // Create sample flow nodes
        CreateNode("inject1", 150, 150, "inject", "Timestamp", "#DE7479");
        CreateNode("function1", 350, 150, "function", "Process", "#F0C483");
        CreateNode("debug1", 550, 150, "debug", "Output", "#87A980");
        
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
            Constraints = DefaultNodeConstraints,
            Annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation
                {
                    Content = label,
                    Style = new TextStyle() { Color = "#333333", FontSize = 12 }
                }
            },
            AdditionalInfo = new Dictionary<string, object> { { "nodeType", nodeType }, { "color", color } }
        };
        DiagramNodes!.Add(node);
        NodeCount++;
    }

    private DiagramObjectCollection<PointPort> CreatePorts()
    {
        var ports = new DiagramObjectCollection<PointPort>();
        // Input port (left side) - accepts incoming connections, cannot draw from here
        ports.Add(new PointPort()
        {
            ID = "port1",
            Shape = PortShapes.Circle,
            Offset = new DiagramPoint() { X = 0, Y = 0.5 },
            Visibility = PortVisibility.Visible,
            Style = new ShapeStyle { Fill = "#888", StrokeColor = "#666" },
            Width = 10,
            Height = 10,
            Constraints = PortConstraints.InConnect
        });
        // Output port (right side) - can draw connections from here
        ports.Add(new PointPort()
        {
            ID = "port2",
            Shape = PortShapes.Circle,
            Offset = new DiagramPoint() { X = 1, Y = 0.5 },
            Visibility = PortVisibility.Visible,
            Style = new ShapeStyle { Fill = "#888", StrokeColor = "#666" },
            Width = 10,
            Height = 10,
            Constraints = PortConstraints.Draw | PortConstraints.OutConnect
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
            TargetDecorator = new DecoratorSettings { Shape = DecoratorShape.Arrow, Style = new ShapeStyle { Fill = "#888", StrokeColor = "#888" } }
        };
        DiagramConnectors!.Add(connector);
    }

    private void OnHistoryChange(HistoryChangedEventArgs arg)
    {
        if (arg.ActionTrigger == HistoryChangedAction.CustomAction)
        {
            if (redoStack.Count > 0)
            {
                redoStack.Clear();
            }
            string entryLog = arg.EntryType.ToString();
            undoStack.Add(entryLog);
        }
        else if (arg.ActionTrigger == HistoryChangedAction.Redo && redoStack.Count > 0)
        {
            undoStack.Add(redoStack[^1]);
            redoStack.RemoveAt(redoStack.Count - 1);
        }
        else if (arg.ActionTrigger == HistoryChangedAction.Undo && undoStack.Count > 0)
        {
            redoStack.Add(undoStack[^1]);
            undoStack.RemoveAt(undoStack.Count - 1);
        }
        IsEnableUndoButton = undoStack.Count == 0;
        IsEnableRedoButton = redoStack.Count == 0;
    }

    private void DragDropEvent(DropEventArgs args)
    {
        // Handle dropped nodes - just clear tooltips and track count
        if (args.Element is Node node && node.Tooltip != null)
        {
            node.Tooltip = null;
            node.Constraints &= ~NodeConstraints.Tooltip;
        }
        else if (args.Element is Connector connector && connector.Tooltip != null)
        {
            connector.Tooltip = null;
            connector.Constraints &= ~ConnectorConstraints.Tooltip;
        }
    }

    private void UpdateToolbarItems()
    {
        int nodeCount = DiagramInstance?.SelectionSettings?.Nodes?.Count ?? 0;
        int connectorCount = DiagramInstance?.SelectionSettings?.Connectors?.Count ?? 0;
        
        if (nodeCount + connectorCount == 0)
        {
            IsEnableCopyButton = true;
            IsEnableCutButton = true;
            IsDeleteDisable = true;
        }
        else
        {
            IsEnableCopyButton = false;
            IsEnableCutButton = false;
            IsDeleteDisable = false;
        }
    }

    private void OnSelectionChanged(Syncfusion.Blazor.Diagram.SelectionChangedEventArgs args)
    {
        UpdateToolbarItems();
        
        if (args.NewValue?.Count > 0 && args.NewValue[0] is Node node)
        {
            SelectedDiagramNode = node;
            SelectedNodeName = node.Annotations?.FirstOrDefault()?.Content ?? "";
            
            // Load function code if it's a function node
            if (node.AdditionalInfo?.TryGetValue("functionCode", out var codeObj) == true)
            {
                SelectedNodeFunctionCode = codeObj as string ?? "";
            }
            else
            {
                SelectedNodeFunctionCode = "";
            }
            
            // Switch to properties tab when node is selected
            SelectedSidebarTab = 0;
        }
        else
        {
            SelectedDiagramNode = null;
            SelectedNodeName = "";
            SelectedNodeFunctionCode = "";
        }
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
            
            // Apply default node constraints (no resize/rotate)
            node.Constraints = DefaultNodeConstraints;
            
            // Configure port constraints for Node-RED style connections
            // port1 (input) = can receive connections, cannot draw
            // port2 (output) = can draw connections
            if (node.Ports != null)
            {
                foreach (var port in node.Ports)
                {
                    port.Visibility = PortVisibility.Visible;
                    port.Width = 10;
                    port.Height = 10;
                    port.Style = new ShapeStyle { Fill = "#888", StrokeColor = "#666" };
                    
                    if (port.ID == "port1")
                    {
                        // Input port: accept connections but cannot draw from it
                        port.Constraints = PortConstraints.InConnect;
                    }
                    else if (port.ID == "port2")
                    {
                        // Output port: can draw connections from it
                        port.Constraints = PortConstraints.Draw | PortConstraints.OutConnect;
                    }
                }
            }
        }
    }

    private void OnConnectorCreating(IDiagramObject obj)
    {
        if (obj is Connector connector)
        {
            connector.Style ??= new ShapeStyle();
            connector.Style.StrokeColor = "#888";
            connector.Style.StrokeWidth = 2;
            connector.TargetDecorator ??= new DecoratorSettings();
            connector.TargetDecorator.Style ??= new ShapeStyle();
            connector.TargetDecorator.Style.Fill = "#888";
            connector.TargetDecorator.Style.StrokeColor = "#888";
            
            // Set default connector type to Bezier for Node-RED style
            connector.Type = ConnectorSegmentType.Bezier;
        }
    }
    
    /// <summary>
    /// Validates connections to ensure they follow Node-RED rules:
    /// - Connections must go from output port (port2) to input port (port1)
    /// - Cannot connect a node to itself
    /// - Must connect to a port, not directly to a node
    /// </summary>
    private void OnConnectionChanging(ConnectionChangingEventArgs args)
    {
        // Get connector being modified
        var connector = args.Connector;
        if (connector == null) return;
        
        // Prevent connecting a node to itself
        if (!string.IsNullOrEmpty(connector.SourceID) && 
            !string.IsNullOrEmpty(connector.TargetID) && 
            connector.SourceID == connector.TargetID)
        {
            args.Cancel = true;
            return;
        }
        
        // After connection is made, validate port directions
        // Source should be from output port (port2), Target should be to input port (port1)
        string? sourcePortId = connector.SourcePortID;
        string? targetPortId = connector.TargetPortID;
        
        // Check new connection values if available
        if (args.NewValue != null)
        {
            // Update source/target port based on what's changing
            sourcePortId = args.NewValue.SourcePortID ?? sourcePortId;
            targetPortId = args.NewValue.TargetPortID ?? targetPortId;
            
            // Check for self-connection
            var newSourceId = args.NewValue.SourceID ?? connector.SourceID;
            var newTargetId = args.NewValue.TargetID ?? connector.TargetID;
            if (!string.IsNullOrEmpty(newSourceId) && newSourceId == newTargetId)
            {
                args.Cancel = true;
                return;
            }
        }
        
        // If both ports are set, validate the connection direction
        if (!string.IsNullOrEmpty(sourcePortId) && !string.IsNullOrEmpty(targetPortId))
        {
            // Valid: port2 (output) -> port1 (input)
            // Invalid: port1 -> port1, port2 -> port2, port1 -> port2
            if (sourcePortId == "port1" || targetPortId == "port2")
            {
                args.Cancel = true;
                return;
            }
        }
    }

    // Flow tab management
    private void AddNewFlow()
    {
        FlowCounter++;
        var newFlow = new FlowTab { Id = $"flow{FlowCounter}", Label = $"Flow {FlowCounter}" };
        Flows.Add(newFlow);
        CurrentFlowId = newFlow.Id;
        
        // Clear current diagram for new flow
        DiagramNodes?.Clear();
        DiagramConnectors?.Clear();
        NodeCount = 0;
        ConnectorCount = 0;
    }

    private void SwitchFlow(string flowId)
    {
        // TODO: Save current flow state before switching
        CurrentFlowId = flowId;
        // TODO: Load flow state for the selected flow
    }

    private void DeleteFlow(string flowId)
    {
        if (Flows.Count <= 1) return;
        
        var flowToRemove = Flows.FirstOrDefault(f => f.Id == flowId);
        if (flowToRemove != null)
        {
            Flows.Remove(flowToRemove);
            if (CurrentFlowId == flowId)
            {
                CurrentFlowId = Flows.First().Id;
            }
        }
    }

    private string GetCurrentFlowLabel()
    {
        return Flows.FirstOrDefault(f => f.Id == CurrentFlowId)?.Label ?? "Unknown";
    }

    private string GetSelectedNodeType()
    {
        if (SelectedDiagramNode?.AdditionalInfo?.TryGetValue("nodeType", out var typeObj) == true)
        {
            return typeObj as string ?? "unknown";
        }
        return "unknown";
    }

    // Property change handlers
    private void OnNodeNameChanged(ChangedEventArgs args)
    {
        if (SelectedDiagramNode != null && args.Value != null)
        {
            SelectedNodeName = args.Value;
            if (SelectedDiagramNode.Annotations?.Count > 0)
            {
                SelectedDiagramNode.Annotations[0].Content = args.Value;
            }
        }
    }

    // Toolbar handlers
    private async Task ToolbarEditorClick(Syncfusion.Blazor.Navigations.ClickEventArgs args)
    {
        var value = args.Item.TooltipText;
        switch (value)
        {
            case "Deploy":
                await OnDeployClick();
                break;
            case "Cut":
                DiagramInstance?.Cut();
                IsEnablePasteButton = false;
                break;
            case "Copy":
                DiagramInstance?.Copy();
                IsEnablePasteButton = false;
                break;
            case "Paste":
                DiagramInstance?.Paste();
                break;
            case "Undo":
                DiagramInstance?.Undo();
                UpdateToolbarItems();
                break;
            case "Redo":
                DiagramInstance?.Redo();
                break;
            case "Pan":
                DiagramTool = DiagramInteractions.ZoomPan;
                break;
            case "Select":
                DiagramTool = DiagramInteractions.SingleSelect | DiagramInteractions.MultipleSelect;
                break;
            case "Delete":
                DiagramInstance?.Delete();
                break;
        }
    }

    private void SelectedZoomItem(Syncfusion.Blazor.SplitButtons.MenuEventArgs args)
    {
        var value = args.Item.Text;
        var currentZoom = DiagramInstance?.ScrollSettings?.CurrentZoom ?? 1;
        switch (value)
        {
            case "Zoom In":
            case "Zoom Out":
                var zoomFactor = 0.2;
                zoomFactor = value == "Zoom Out" ? 1 / (1 + zoomFactor) : (1 + zoomFactor);
                DiagramInstance?.Zoom(zoomFactor, null);
                break;
            case "Zoom to Fit":
                FitOptions fitoption = new FitOptions()
                {
                    Mode = FitMode.Both,
                    Region = DiagramRegion.PageSettings,
                };
                DiagramInstance?.FitToPage(fitoption);
                break;
            case "Zoom to 50%":
                DiagramInstance?.Zoom(0.5 / currentZoom, null);
                break;
            case "Zoom to 100%":
                DiagramInstance?.Zoom(1 / currentZoom, null);
                break;
            case "Zoom to 200%":
                DiagramInstance?.Zoom(2 / currentZoom, null);
                break;
        }
        ZoomItemDropdownContent = FormattableString.Invariant($"{Math.Round((DiagramInstance?.ScrollSettings?.CurrentZoom ?? 1) * 100)}") + "%";
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

    // Helper class for flow tabs
    private class FlowTab
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
    }
}
