// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;
using Syncfusion.Blazor.Diagram;
using System.Collections.ObjectModel;

namespace NodeRed.Blazor.Components.Pages;

public partial class Editor : IDisposable
{
    // Default group styling constants
    private const string DefaultGroupFillColor = "rgba(255, 204, 204, 0.3)";
    private const string DefaultGroupStrokeColor = "#FF9999";
    
    // Fallback connector point coordinates (used when source/target nodes haven't been initialized)
    private const double FallbackSourcePointX = 0;
    private const double FallbackSourcePointY = 0;
    private const double FallbackTargetPointX = 100;
    private const double FallbackTargetPointY = 0;
    
    // Diagram reference
    public SfDiagramComponent? DiagramInstance { get; set; }

    // Selection settings
    public DiagramSelectionSettings SelectionSettings { get; set; } = new DiagramSelectionSettings()
    {
        Constraints = SelectorConstraints.All & ~SelectorConstraints.ResizeAll & ~SelectorConstraints.Rotate
    };

    // Diagram collections
    private DiagramObjectCollection<Node>? DiagramNodes { get; set; } = new DiagramObjectCollection<Node>();
    private DiagramObjectCollection<Connector>? DiagramConnectors { get; set; } = new DiagramObjectCollection<Connector>();

    // Grid line intervals
    public double[]? GridLineIntervals { get; set; }

    // Flow management
    private List<FlowTab> Flows = new List<FlowTab>();
    private string CurrentFlowId = "flow1";
    private int FlowCounter = 1;

    // Selection state
    private Node? SelectedDiagramNode;
    private string SelectedNodeName = "";
    private int SelectedSidebarTab = 0;
    private bool IsPropertyTrayOpen = false;

    // Menu state
    private bool IsMainMenuOpen = false;
    private bool IsDeployMenuOpen = false;
    private string DeployMode = "full";
    private bool HasUnsavedChanges = true;
    private bool HasBeenDeployed = false;

    // Import/Export dialogs
    private bool IsImportDialogOpen = false;
    private bool IsExportDialogOpen = false;
    private string ImportJson = "";
    private string ExportJson = "";
    private string ExportFormat = "pretty";

    // Debug messages
    private List<DebugMessage> DebugMessages = new();
    private string DebugMessageFilter = "";
    private bool DebugFilterByNode = false;

    // Node statuses (node ID -> status)
    private Dictionary<string, NodeStatus> _nodeStatuses = new();

    // Cached node definitions for performance
    private List<NodeDefinition>? _cachedNodeDefinitions;

    // Undo/Redo history stacks
    private Stack<EditorAction> _undoStack = new();
    private Stack<EditorAction> _redoStack = new();
    private const int MaxUndoStackSize = 50;

    // Clipboard for copy/paste
    private string _nodeClipboard = "";
    private bool _clipboardIsCut = false;

    // Context menu state
    private bool IsNodeContextMenuOpen = false;
    private bool IsCanvasContextMenuOpen = false;
    private double ContextMenuX = 0;
    private double ContextMenuY = 0;
    private Node? ContextMenuNode = null;

    // Node counter for unique IDs
    private int NodeCount = 0;
    private int ConnectorCount = 0;

    // Custom Palette
    private List<PaletteCategory> PaletteCategories = new();
    private string PaletteFilter = "";
    private PaletteNodeInfo? DraggedNode = null;

    /// <summary>
    /// All node properties are stored dynamically in _nodePropertyValues.
    /// Properties come from SDK node DefineProperties() - no hardcoded bindings.
    /// </summary>

    // Default node constraints
    private static readonly NodeConstraints DefaultNodeConstraints =
        NodeConstraints.Select | NodeConstraints.Drag | NodeConstraints.Delete |
        NodeConstraints.InConnect | NodeConstraints.OutConnect |
        NodeConstraints.PointerEvents | NodeConstraints.AllowDrop;
    
    // Group node constraints - no connections allowed (groups don't have ports)
    private static readonly NodeConstraints GroupNodeConstraints =
        NodeConstraints.Select | NodeConstraints.Drag | NodeConstraints.Delete |
        NodeConstraints.PointerEvents;

    // Application state
    private Workspace CurrentWorkspace = new();

    protected override void OnInitialized()
    {
        // GridLineIntervals: alternating pattern of line thickness and gap
        // Format: [lineWidth, gap, lineWidth, gap, ...]
        GridLineIntervals = new double[] { 
            1, 9, 0.25, 9.75, 0.25, 9.75, 0.25, 9.75, 0.25, 9.75, 
            0.25, 9.75, 0.25, 9.75, 0.25, 9.75, 0.25, 9.75, 0.25, 9.75 
        };

        // Initialize flows
        Flows.Add(new FlowTab { Id = "flow1", Label = "Flow 1" });

        // Initialize custom palette
        InitializePalette();

        // Initialize diagram with sample flow
        InitDiagramModel();

        // Subscribe to runtime events
        FlowRuntime.OnDebugMessage += OnDebugMessage;
        FlowRuntime.OnNodeStatusChanged += OnNodeStatusChanged;
    }

    private void InitializePalette()
    {
        // Build palette entirely from SDK node definitions
        PaletteCategories = new List<PaletteCategory>();
        
        // Get all node definitions from the loader
        var nodeDefinitions = NodeLoader.GetNodeDefinitions();
        
        // Define the preferred category order
        var categoryOrder = new List<string> { "common", "function", "network", "sequence", "parser", "storage", "database" };
        
        // Group nodes by category
        var nodesByCategory = new Dictionary<string, List<PaletteNodeInfo>>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var nodeDef in nodeDefinitions)
        {
            // Determine category name from the node definition
            var categoryName = nodeDef.Category.ToString().ToLowerInvariant();
            
            // For external plugins with hyphenated names (e.g., "example-upper"), use the prefix as category
            if (!string.IsNullOrEmpty(nodeDef.Type) && nodeDef.Type.Contains('-'))
            {
                var prefix = nodeDef.Type.Split('-')[0];
                // Use prefix as category for external plugins
                if (!categoryOrder.Contains(categoryName))
                {
                    categoryName = prefix;
                }
            }
            
            if (!nodesByCategory.ContainsKey(categoryName))
            {
                nodesByCategory[categoryName] = new List<PaletteNodeInfo>();
            }
            
            // Map icon from definition or use default
            var iconClass = !string.IsNullOrEmpty(nodeDef.Icon) ? nodeDef.Icon : "fa fa-cube";
            
            nodesByCategory[categoryName].Add(new PaletteNodeInfo
            {
                Type = nodeDef.Type,
                Label = nodeDef.DisplayName ?? nodeDef.Type,
                Color = nodeDef.Color ?? "#999",
                IconClass = iconClass,
                IconBackground = "rgba(0,0,0,0.05)",
                Inputs = nodeDef.Inputs,
                Outputs = nodeDef.Outputs
            });
        }
        
        // Add categories in preferred order first
        foreach (var categoryName in categoryOrder)
        {
            if (nodesByCategory.TryGetValue(categoryName, out var nodes) && nodes.Count > 0)
            {
                PaletteCategories.Add(new PaletteCategory
                {
                    Name = categoryName,
                    IsExpanded = categoryName == "common" || categoryName == "function",
                    Nodes = nodes
                });
                nodesByCategory.Remove(categoryName);
            }
        }
        
        // Add remaining categories (plugins, etc.)
        foreach (var (categoryName, nodes) in nodesByCategory.OrderBy(kv => kv.Key))
        {
            if (nodes.Count > 0)
            {
                PaletteCategories.Add(new PaletteCategory
                {
                    Name = categoryName,
                    IsExpanded = true,
                    Nodes = nodes
                });
            }
        }
    }

    // Palette filtering
    private bool FilterCategory(PaletteCategory category)
    {
        if (string.IsNullOrWhiteSpace(PaletteFilter)) return true;
        return category.Nodes.Any(n => FilterNode(n));
    }

    private bool FilterNode(PaletteNodeInfo node)
    {
        if (string.IsNullOrWhiteSpace(PaletteFilter)) return true;
        return node.Label.Contains(PaletteFilter, StringComparison.OrdinalIgnoreCase) ||
               node.Type.Contains(PaletteFilter, StringComparison.OrdinalIgnoreCase);
    }

    private void ToggleCategory(string categoryName)
    {
        var category = PaletteCategories.FirstOrDefault(c => c.Name == categoryName);
        if (category != null)
        {
            category.IsExpanded = !category.IsExpanded;
        }
    }

    // Drag and drop from palette
    private void OnPaletteDragStart(PaletteNodeInfo node)
    {
        DraggedNode = node;
    }

    private void OnPaletteDragEnd()
    {
        DraggedNode = null;
    }

    private void OnWorkspaceDragOver(DragEventArgs e)
    {
        e.DataTransfer.DropEffect = "copy";
    }

    private async Task OnWorkspaceDrop(DragEventArgs e)
    {
        if (DraggedNode != null && DiagramInstance != null)
        {
            // Create node at drop position
            var nodeId = $"{DraggedNode.Type}{++NodeCount}";
            
            // Get diagram bounds to calculate position
            var x = 200 + (NodeCount % 5) * 150;
            var y = 150 + (NodeCount / 5) * 80;

            await CreateDiagramNode(nodeId, x, y, DraggedNode.Type, DraggedNode.Label, DraggedNode.Color);
            DraggedNode = null;
            StateHasChanged();
        }
    }

    private async Task CreateDiagramNode(string id, double x, double y, string nodeType, string label, string color)
    {
        var paletteNode = GetPaletteNodeInfo(nodeType);
        var node = CreateNodeRedStyleNode(id, x, y, nodeType, label, color, paletteNode);
        DiagramNodes!.Add(node);
    }

    private void InitDiagramModel()
    {
        // Create a simple sample flow
        // Users can add plugin nodes by dragging from the palette
        CreateNode("inject1", 150, 150, "inject", "timestamp", "#a6bbcf");
        CreateNode("function1", 350, 150, "function", "process", "#fdd0a2");
        CreateNode("debug1", 550, 150, "debug", "msg.payload", "#87a980");

        // Note: Connectors are not created here to avoid initialization issues
        // Users can draw connections by clicking on the output port (right side)
        // and dragging to an input port (left side)
    }

    private void CreateNode(string id, double x, double y, string nodeType, string label, string color)
    {
        var paletteNode = GetPaletteNodeInfo(nodeType);
        var node = CreateNodeRedStyleNode(id, x, y, nodeType, label, color, paletteNode);
        DiagramNodes!.Add(node);
        NodeCount++;
    }

    private PaletteNodeInfo? GetPaletteNodeInfo(string nodeType)
    {
        foreach (var category in PaletteCategories)
        {
            var node = category.Nodes.FirstOrDefault(n => n.Type == nodeType);
            if (node != null) return node;
        }
        return null;
    }

    private Node CreateNodeRedStyleNode(string id, double x, double y, string nodeType, string label, string color, PaletteNodeInfo? paletteNode)
    {
        var iconClass = paletteNode?.IconClass ?? "fa fa-cube";
        
        // Use palette node info if available, otherwise fall back to hardcoded lists
        bool hasInput;
        bool hasOutput;
        if (paletteNode is not null)
        {
            hasInput = paletteNode.Inputs > 0;
            hasOutput = paletteNode.Outputs > 0;
        }
        else
        {
            // Fallback for nodes not found in palette
            hasInput = nodeType != "inject" && nodeType != "complete" && nodeType != "catch" && 
                       nodeType != "status" && nodeType != "link in" && nodeType != "comment" &&
                       nodeType != "mqtt in" && nodeType != "http in" && nodeType != "tcp in" && 
                       nodeType != "udp in" && nodeType != "websocket in" && nodeType != "watch";
            hasOutput = nodeType != "debug" && nodeType != "link out" && nodeType != "http response" && 
                        nodeType != "mqtt out" && nodeType != "websocket out" && nodeType != "tcp out" && 
                        nodeType != "udp out" && nodeType != "comment";
        }

        // Create a node with child nodes for the icon area
        // Match palette node dimensions: 122px wide, 25px tall
        var node = new Node()
        {
            ID = id,
            OffsetX = x,
            OffsetY = y,
            Width = 122,
            Height = 25,
            Ports = CreatePortsFromNodeInfo(hasInput, hasOutput),
            Style = new ShapeStyle { Fill = color, StrokeColor = "#999", StrokeWidth = 1 },
            Shape = new BasicShape() { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 5 },
            Constraints = DefaultNodeConstraints,
            Annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                // Icon annotation (using Unicode/Font Awesome representation)
                new ShapeAnnotation
                {
                    ID = "iconAnnotation",
                    Content = GetIconContent(iconClass),
                    Offset = new DiagramPoint() { X = 0.12, Y = 0.5 },
                    Style = new TextStyle() 
                    { 
                        Color = "#fff", 
                        FontSize = 12,
                        FontFamily = "FontAwesome"
                    },
                    Constraints = AnnotationConstraints.ReadOnly
                },
                // Label annotation
                new ShapeAnnotation
                {
                    ID = "labelAnnotation",
                    Content = label,
                    Offset = new DiagramPoint() { X = 0.58, Y = 0.5 },
                    Style = new TextStyle() { Color = "#333", FontSize = 12 },
                    Constraints = AnnotationConstraints.ReadOnly
                },
                // Status annotation (below the node, hidden by default)
                new ShapeAnnotation
                {
                    ID = "statusAnnotation",
                    Content = "",
                    Offset = new DiagramPoint() { X = 0.5, Y = 1.5 },
                    Style = new TextStyle() { Color = "#888", FontSize = 10 },
                    Constraints = AnnotationConstraints.ReadOnly
                }
            },
            AdditionalInfo = new Dictionary<string, object> 
            { 
                { "nodeType", nodeType }, 
                { "color", color },
                { "iconClass", iconClass },
                { "hasInput", hasInput },
                { "hasOutput", hasOutput }
            },
            FixedUserHandles = CreateFixedUserHandles(nodeType)
        };

        return node;
    }

    private string GetIconContent(string iconClass)
    {
        // Map Font Awesome classes to Unicode characters
        return iconClass switch
        {
            "fa fa-arrow-right" => "\uf061",
            "fa fa-arrow-left" => "\uf060",
            "fa fa-arrow-down" => "\uf063",
            "fa fa-arrow-up" => "\uf062",
            "fa fa-bug" => "\uf188",
            "fa fa-code" => "\uf121",
            "fa fa-edit" => "\uf044",
            "fa fa-random" => "\uf074",
            "fa fa-clock-o" => "\uf017",
            "fa fa-file-text-o" => "\uf0f6",
            "fa fa-comment-o" => "\uf0e5",
            "fa fa-warning" => "\uf071",
            "fa fa-check-circle-o" => "\uf05d",
            "fa fa-circle-o" => "\uf10c",
            "fa fa-link" => "\uf0c1",
            "fa fa-arrows-h" => "\uf07e",
            "fa fa-toggle-off" => "\uf204",
            "fa fa-terminal" => "\uf120",
            "fa fa-tasks" => "\uf0ae",
            "fa fa-sign-in" => "\uf090",
            "fa fa-sign-out" => "\uf08b",
            "fa fa-globe" => "\uf0ac",
            "fa fa-plug" => "\uf1e6",
            "fa fa-exchange" => "\uf0ec",
            "fa fa-columns" => "\uf0db",
            "fa fa-compress" => "\uf066",
            "fa fa-sort" => "\uf0dc",
            "fa fa-list" => "\uf03a",
            "fa fa-table" => "\uf0ce",
            "fa fa-file-code-o" => "\uf1c9",
            "fa fa-file" => "\uf15b",
            "fa fa-eye" => "\uf06e",
            "fa fa-tag" => "\uf02b",
            "fa fa-server" => "\uf233",
            "fa fa-filter" => "\uf0b0",
            "fa fa-database" => "\uf1c0",
            "fa fa-lock" => "\uf023",
            "fa fa-shield" => "\uf132",
            "fa fa-cog" => "\uf013",
            "fa fa-cogs" => "\uf085",
            "fa fa-sitemap" => "\uf0e8",
            "fa fa-dot-circle-o" => "\uf192",
            _ => "\uf1b2" // cube as default
        };
    }

    private DiagramObjectCollection<NodeFixedUserHandle> CreateFixedUserHandles(string nodeType)
    {
        var handles = new DiagramObjectCollection<NodeFixedUserHandle>();

        // Only add inject button for inject nodes - positioned to the left like Node-RED JS
        // Node-RED JS shows a small square button with play icon
        if (nodeType == "inject")
        {
            handles.Add(new NodeFixedUserHandle()
            {
                ID = "injectButton",
                Width = 14,
                Height = 14,
                Offset = new DiagramPoint() { X = 0, Y = 0.5 },
                Margin = new DiagramThickness() { Left = -20 }, // Position outside node like Node-RED JS
                PathData = "M0 0 L8 4 L0 8 Z", // Triangle play icon
                Visibility = true,
                CornerRadius = 0,
                Fill = "#d9d9d9",
                Stroke = "#999",
                StrokeThickness = 1,
                IconStroke = "#777",
                IconStrokeThickness = 0
            });
        }

        return handles;
    }

    private DiagramObjectCollection<PointPort> CreatePorts(string nodeType = "")
    {
        // Use the hardcoded list as fallback for when palette info is not available
        bool hasInput = nodeType != "inject" && nodeType != "complete" && nodeType != "catch" && 
                        nodeType != "status" && nodeType != "link in" && nodeType != "comment" &&
                        nodeType != "mqtt in" && nodeType != "http in" && nodeType != "tcp in" && 
                        nodeType != "udp in" && nodeType != "websocket in" && nodeType != "watch";
        bool hasOutput = nodeType != "debug" && nodeType != "link out" && nodeType != "http response" && 
                         nodeType != "mqtt out" && nodeType != "websocket out" && nodeType != "tcp out" && 
                         nodeType != "udp out" && nodeType != "comment";
        
        return CreatePortsFromNodeInfo(hasInput, hasOutput);
    }

    private DiagramObjectCollection<PointPort> CreatePortsFromNodeInfo(bool hasInput, bool hasOutput)
    {
        var ports = new DiagramObjectCollection<PointPort>();

        if (hasInput)
        {
            // Input port - only accepts incoming connections
            // Uses Default constraints which allow connections to dock
            ports.Add(new PointPort()
            {
                ID = "port1",
                Shape = PortShapes.Square,
                Offset = new DiagramPoint() { X = 0, Y = 0.5 },
                Visibility = PortVisibility.Visible,
                Style = new ShapeStyle { Fill = "#d9d9d9", StrokeColor = "#999" },
                Width = 10,
                Height = 10,
                // Default allows the port to be a valid connection target
                Constraints = PortConstraints.Default
            });
        }

        if (hasOutput)
        {
            // Output port - can draw connectors from this port
            // Uses Default + Draw to enable drawing connections from this port
            ports.Add(new PointPort()
            {
                ID = "port2",
                Shape = PortShapes.Square,
                Offset = new DiagramPoint() { X = 1, Y = 0.5 },
                Visibility = PortVisibility.Visible,
                Style = new ShapeStyle { Fill = "#d9d9d9", StrokeColor = "#999" },
                Width = 10,
                Height = 10,
                // Default + Draw enables drawing connectors from this port
                Constraints = PortConstraints.Default | PortConstraints.Draw
            });
        }

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
            Type = ConnectorSegmentType.Orthogonal,
            Style = new ShapeStyle { StrokeColor = "#999", StrokeWidth = 2 },
            TargetDecorator = new DecoratorSettings { Shape = DecoratorShape.None },
            // Fallback points to prevent null reference during initialization
            SourcePoint = new DiagramPoint() { X = FallbackSourcePointX, Y = FallbackSourcePointY },
            TargetPoint = new DiagramPoint() { X = FallbackTargetPointX, Y = FallbackTargetPointY }
        };
        DiagramConnectors!.Add(connector);
    }

    private void OnSelectionChanged(Syncfusion.Blazor.Diagram.SelectionChangedEventArgs args)
    {
        if (args.NewValue?.Count > 0 && args.NewValue[0] is Node node)
        {
            SelectedDiagramNode = node;
            // Get label from labelAnnotation (index 1), not iconAnnotation (index 0)
            SelectedNodeName = node.Annotations?.FirstOrDefault(a => a.ID == "labelAnnotation")?.Content ?? "";
            LoadNodeProperties(node);
            SelectedSidebarTab = 0;
        }
        else
        {
            SelectedDiagramNode = null;
            SelectedNodeName = "";
        }
    }

    private void OnDiagramClick(ClickEventArgs args)
    {
        // Double-click opens property tray (like Node-RED)
        if (args.Count == 2 && SelectedDiagramNode != null)
        {
            IsPropertyTrayOpen = true;
            SelectedSidebarTab = 0; // Switch to Info tab
        }
    }

    /// <summary>
    /// Handles position changes - when a group is moved, move its contained nodes too
    /// </summary>
    private void OnPositionChanged(PositionChangedEventArgs args)
    {
        if (args.NewValue?.Nodes?.Count > 0)
        {
            foreach (var movedNode in args.NewValue.Nodes)
            {
                // Check if this is a group node
                var group = Groups.FirstOrDefault(g => g.DiagramNodeId == movedNode.ID);
                if (group != null && DiagramNodes != null)
                {
                    // Calculate the delta movement
                    var oldNode = args.OldValue?.Nodes?.FirstOrDefault(n => n.ID == movedNode.ID);
                    if (oldNode != null)
                    {
                        double deltaX = movedNode.OffsetX - oldNode.OffsetX;
                        double deltaY = movedNode.OffsetY - oldNode.OffsetY;
                        
                        // Move all nodes that belong to this group
                        foreach (var nodeId in group.NodeIds)
                        {
                            var node = DiagramNodes.FirstOrDefault(n => n.ID == nodeId);
                            if (node != null)
                            {
                                node.OffsetX += deltaX;
                                node.OffsetY += deltaY;
                            }
                        }
                        
                        // Update group position info
                        group.X += deltaX;
                        group.Y += deltaY;
                    }
                }
            }
        }
    }

    private void ClosePropertyTray()
    {
        IsPropertyTrayOpen = false;
        // Reload the original properties from the node (cancel changes)
        if (SelectedDiagramNode != null)
        {
            LoadNodeProperties(SelectedDiagramNode);
        }
    }

    private void SaveAndClosePropertyTray()
    {
        SaveNodeProperties();
        IsPropertyTrayOpen = false;
    }

    private async Task OnFixedUserHandleClick(FixedUserHandleClickEventArgs args)
    {
        // Handle inject button click
        if (args.FixedUserHandle?.ID == "injectButton" && args.Element is Node node)
        {
            var nodeType = node.AdditionalInfo?.TryGetValue("nodeType", out var typeObj) == true
                ? typeObj as string : null;

            if (nodeType == "inject" && node.ID != null)
            {
                // Check if flows are running
                if (FlowRuntime.State != FlowState.Running)
                {
                    var nodeName = node.Annotations?.FirstOrDefault(a => a.ID == "labelAnnotation")?.Content ?? node.ID;
                    DebugMessages.Add(new DebugMessage
                    {
                        NodeId = node.ID,
                        NodeName = nodeName ?? node.ID,
                        Data = "Please deploy and start the flows first",
                        Timestamp = DateTimeOffset.Now
                    });
                    StateHasChanged();
                    return;
                }

                try
                {
                    await FlowRuntime.InjectAsync(node.ID);
                    // Don't add message here - the debug node will show the output
                    StateHasChanged();
                }
                catch (Exception ex)
                {
                    DebugMessages.Add(new DebugMessage
                    {
                        NodeId = node.ID,
                        NodeName = node.ID,
                        Data = $"Error: {ex.Message}",
                        Timestamp = DateTimeOffset.Now
                    });
                    StateHasChanged();
                }
            }
        }
    }

    private void LoadNodeProperties(Node node)
    {
        // All nodes use dynamic property loading from SDK definitions
        LoadDynamicNodeProperties(node);
    }

    /// <summary>
    /// Loads properties for SDK nodes dynamically from AdditionalInfo.
    /// </summary>
    private void LoadDynamicNodeProperties(Node node)
    {
        _nodePropertyValues.Clear();
        
        // Get the schema for this node type
        var nodeType = GetSelectedNodeType();
        var schema = GetNodeSchema(nodeType);
        
        // Load each property from the node's AdditionalInfo
        foreach (var field in schema)
        {
            if (node.AdditionalInfo?.TryGetValue(field.Name, out var value) == true)
            {
                _nodePropertyValues[field.Name] = value;
            }
            else if (!string.IsNullOrEmpty(field.DefaultValue))
            {
                _nodePropertyValues[field.Name] = field.DefaultValue;
            }
        }
    }

    private T GetNodeProperty<T>(Node node, string key, T defaultValue)
    {
        if (node.AdditionalInfo?.TryGetValue(key, out var value) == true && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }

    private void OnCreated()
    {
        if (DiagramInstance?.Nodes?.Count > 0)
        {
            DiagramInstance.Select(new ObservableCollection<IDiagramObject>() { DiagramInstance.Nodes[0] });
        }
    }

    private void OnNodeCreating(IDiagramObject obj)
    {
        if (obj is Node node)
        {
            node.Style ??= new ShapeStyle();
            node.Style.StrokeWidth = 1;
            node.Style.StrokeColor = "#999";
            node.Constraints = DefaultNodeConstraints;
        }
    }

    private void OnConnectorCreating(IDiagramObject obj)
    {
        if (obj is Connector connector)
        {
            connector.Style ??= new ShapeStyle();
            connector.Style.StrokeColor = "#999";
            connector.Style.StrokeWidth = 2;
            connector.TargetDecorator = new DecoratorSettings { Shape = DecoratorShape.None };
            // TODO: Implement Bezier connectors - requires additional segment configuration
            // For now, using Orthogonal connectors which work correctly with Syncfusion
            connector.Type = ConnectorSegmentType.Orthogonal;
            
            // Initialize source and target points to prevent null reference during connection
            connector.SourcePoint ??= new DiagramPoint() { X = FallbackSourcePointX, Y = FallbackSourcePointY };
            connector.TargetPoint ??= new DiagramPoint() { X = FallbackTargetPointX, Y = FallbackTargetPointY };
        }
    }

    private void OnConnectionChanging(ConnectionChangingEventArgs args)
    {
        var connector = args.Connector;
        if (connector == null) return;

        // Prevent self-connections
        if (!string.IsNullOrEmpty(connector.SourceID) &&
            !string.IsNullOrEmpty(connector.TargetID) &&
            connector.SourceID == connector.TargetID)
        {
            args.Cancel = true;
            return;
        }

        // Check for self-connection in new value
        if (args.NewValue != null)
        {
            var newSourceId = args.NewValue.SourceID ?? connector.SourceID;
            var newTargetId = args.NewValue.TargetID ?? connector.TargetID;
            if (!string.IsNullOrEmpty(newSourceId) && newSourceId == newTargetId)
            {
                args.Cancel = true;
                return;
            }
        }
    }

    // Flow management
    private void AddNewFlow()
    {
        FlowCounter++;
        var newFlow = new FlowTab { Id = $"flow{FlowCounter}", Label = $"Flow {FlowCounter}" };
        Flows.Add(newFlow);
        CurrentFlowId = newFlow.Id;
        DiagramNodes?.Clear();
        DiagramConnectors?.Clear();
        NodeCount = 0;
        ConnectorCount = 0;
    }

    private void SwitchFlow(string flowId)
    {
        if (flowId == CurrentFlowId) return;
        
        // Save current flow's nodes and connectors
        SaveCurrentFlowState();
        
        // Switch to the new flow
        CurrentFlowId = flowId;
        
        // Restore the new flow's nodes and connectors
        RestoreFlowState(flowId);
        
        StateHasChanged();
    }
    
    /// <summary>
    /// Saves the current flow's diagram state (nodes and connectors) to the FlowTab
    /// </summary>
    private void SaveCurrentFlowState()
    {
        var currentFlow = Flows.FirstOrDefault(f => f.Id == CurrentFlowId);
        if (currentFlow == null || DiagramNodes == null || DiagramConnectors == null) return;
        
        currentFlow.StoredNodes.Clear();
        currentFlow.StoredConnectors.Clear();
        currentFlow.NodeCounter = NodeCount;
        currentFlow.ConnectorCounter = ConnectorCount;
        currentFlow.Groups = new List<GroupInfo>(Groups);
        
        // Save all nodes
        foreach (var node in DiagramNodes)
        {
            var nodeData = new FlowNodeData
            {
                Id = node.ID,
                Type = node.AdditionalInfo?.ContainsKey("nodeType") == true ? node.AdditionalInfo["nodeType"]?.ToString() ?? "" : "",
                OffsetX = node.OffsetX,
                OffsetY = node.OffsetY,
                Width = node.Width ?? 122,
                Height = node.Height ?? 25,
                AdditionalInfo = node.AdditionalInfo != null ? new Dictionary<string, object?>(node.AdditionalInfo) : new()
            };
            
            // Check if this is a group node
            var isGroup = Groups.Any(g => g.DiagramNodeId == node.ID);
            nodeData.IsGroup = isGroup;
            
            // Get the node color
            if (node.Style is ShapeStyle style)
            {
                nodeData.Color = style.Fill ?? "";
                if (isGroup)
                {
                    nodeData.GroupStyle = $"{style.Fill}|{style.StrokeColor}";
                }
            }
            
            // Get icon and label from annotations
            if (node.Annotations != null)
            {
                var iconAnnotation = node.Annotations.FirstOrDefault(a => a.ID == "iconAnnotation");
                if (iconAnnotation != null)
                {
                    nodeData.IconContent = iconAnnotation.Content ?? "";
                }
                
                var labelAnnotation = node.Annotations.FirstOrDefault(a => a.ID == "labelAnnotation");
                if (labelAnnotation != null)
                {
                    nodeData.LabelContent = labelAnnotation.Content ?? "";
                }
                
                // Also check for group label
                var groupLabel = node.Annotations.FirstOrDefault(a => a.ID == "groupLabel");
                if (groupLabel != null)
                {
                    nodeData.LabelContent = groupLabel.Content ?? "";
                }
            }
            
            currentFlow.StoredNodes.Add(nodeData);
        }
        
        // Save all connectors
        foreach (var connector in DiagramConnectors)
        {
            var connectorData = new FlowConnectorData
            {
                Id = connector.ID,
                SourceId = connector.SourceID,
                SourcePortId = connector.SourcePortID ?? "",
                TargetId = connector.TargetID,
                TargetPortId = connector.TargetPortID ?? ""
            };
            currentFlow.StoredConnectors.Add(connectorData);
        }
    }
    
    /// <summary>
    /// Restores a flow's diagram state from the FlowTab
    /// </summary>
    private void RestoreFlowState(string flowId)
    {
        var flow = Flows.FirstOrDefault(f => f.Id == flowId);
        if (flow == null || DiagramNodes == null || DiagramConnectors == null) return;
        
        // Clear current diagram
        DiagramNodes.Clear();
        DiagramConnectors.Clear();
        Groups.Clear();
        
        NodeCount = flow.NodeCounter;
        ConnectorCount = flow.ConnectorCounter;
        Groups = new List<GroupInfo>(flow.Groups);
        
        // Restore nodes
        foreach (var nodeData in flow.StoredNodes)
        {
            if (nodeData.IsGroup)
            {
                // Skip group nodes on first pass - we'll add them after all regular nodes
                continue;
            }
            
            // Handle special subflow I/O nodes
            if (nodeData.Type == "subflow-in" || nodeData.Type == "subflow-out")
            {
                var isInput = nodeData.Type == "subflow-in";
                var ioNode = new Node
                {
                    ID = nodeData.Id,
                    OffsetX = nodeData.OffsetX,
                    OffsetY = nodeData.OffsetY,
                    Width = nodeData.Width,
                    Height = nodeData.Height,
                    Shape = new BasicShape { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 3 },
                    Style = new ShapeStyle { Fill = "#A6BBCF", StrokeColor = "#7B9BAC", StrokeWidth = 1 },
                    AdditionalInfo = new Dictionary<string, object?>(nodeData.AdditionalInfo)
                };
                ioNode.Annotations = new DiagramObjectCollection<ShapeAnnotation>
                {
                    new ShapeAnnotation { ID = "iconAnnotation", Content = isInput ? "→" : "←", Style = new TextStyle { Color = "#333", FontSize = 12 } },
                    new ShapeAnnotation { ID = "labelAnnotation", Content = nodeData.LabelContent, Style = new TextStyle { Color = "#333", FontSize = 12 }, Offset = new DiagramPoint { X = 0.5, Y = 0.5 } }
                };
                ioNode.Ports = new DiagramObjectCollection<PointPort>
                {
                    new PointPort
                    {
                        ID = isInput ? "output" : "input",
                        Offset = new DiagramPoint { X = isInput ? 1 : 0, Y = 0.5 },
                        Visibility = PortVisibility.Visible,
                        Height = 8,
                        Width = 8,
                        Style = new ShapeStyle { Fill = "#333", StrokeColor = "#333" }
                    }
                };
                DiagramNodes.Add(ioNode);
                continue;
            }
            
            // Find palette node info
            PaletteNodeInfo? paletteNode = null;
            foreach (var category in PaletteCategories)
            {
                paletteNode = category.Nodes.FirstOrDefault(n => n.Type == nodeData.Type);
                if (paletteNode != null) break;
            }
            
            // Create regular node
            var node = CreateNodeRedStyleNode(
                nodeData.Id,
                nodeData.OffsetX,
                nodeData.OffsetY,
                nodeData.Type,
                nodeData.LabelContent,
                !string.IsNullOrEmpty(nodeData.Color) && nodeData.Color.StartsWith("#") ? nodeData.Color : paletteNode?.Color ?? "#ddd",
                paletteNode
            );
            
            // Restore additional info
            node.AdditionalInfo = new Dictionary<string, object?>(nodeData.AdditionalInfo);
            
            DiagramNodes.Add(node);
        }
        
        // Restore connectors - add fallback points to prevent NullReferenceException
        foreach (var connectorData in flow.StoredConnectors)
        {
            var connector = new Connector
            {
                ID = connectorData.Id,
                SourceID = connectorData.SourceId,
                SourcePortID = connectorData.SourcePortId,
                TargetID = connectorData.TargetId,
                TargetPortID = connectorData.TargetPortId,
                Type = ConnectorSegmentType.Orthogonal,
                Style = new ShapeStyle { StrokeColor = "#999", StrokeWidth = 2 },
                TargetDecorator = new DecoratorSettings { Shape = DecoratorShape.None },
                // Fallback points to prevent null reference during initialization
                SourcePoint = new DiagramPoint() { X = FallbackSourcePointX, Y = FallbackSourcePointY },
                TargetPoint = new DiagramPoint() { X = FallbackTargetPointX, Y = FallbackTargetPointY }
            };
            DiagramConnectors.Add(connector);
        }
        
        // Now add group visual nodes (using regular Node, not NodeGroup to avoid connector issues)
        foreach (var group in Groups)
        {
            var parts = group.Color?.Split('|') ?? new[] { DefaultGroupFillColor, DefaultGroupStrokeColor };
            var groupNode = new Node
            {
                ID = group.DiagramNodeId,
                OffsetX = group.X + group.Width / 2,
                OffsetY = group.Y + group.Height / 2,
                Width = group.Width,
                Height = group.Height,
                Shape = new BasicShape { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 5 },
                Style = new ShapeStyle
                {
                    Fill = parts.Length > 0 ? parts[0] : DefaultGroupFillColor,
                    StrokeColor = parts.Length > 1 ? parts[1] : DefaultGroupStrokeColor,
                    StrokeWidth = 2,
                    StrokeDashArray = "5,3"
                },
                ZIndex = -1,
                Ports = new DiagramObjectCollection<PointPort>(), // Empty ports - groups don't have connections
                Constraints = GroupNodeConstraints, // No connections - groups don't have ports
                Annotations = new DiagramObjectCollection<ShapeAnnotation>
                {
                    new ShapeAnnotation
                    {
                        ID = "groupLabel",
                        Content = group.Name,
                        Style = new TextStyle { Color = "#666", FontSize = 11, Bold = true },
                        Offset = new DiagramPoint { X = 0, Y = 0 },
                        Margin = new DiagramThickness { Left = 5, Top = 5, Right = 0, Bottom = 0 },
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top
                    }
                }
            };
            DiagramNodes.Add(groupNode);
        }
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

    // Flow properties dialog state
    private bool IsFlowPropertiesDialogOpen = false;
    private string FlowPropertiesFlowId = "";
    private string FlowPropertiesLabel = "";
    private string FlowPropertiesInfo = "";
    private bool FlowPropertiesEnabled = true;

    private void EditFlowProperties(string flowId)
    {
        var flow = Flows.FirstOrDefault(f => f.Id == flowId);
        if (flow != null)
        {
            FlowPropertiesFlowId = flowId;
            FlowPropertiesLabel = flow.Label;
            FlowPropertiesInfo = flow.Info;
            FlowPropertiesEnabled = !flow.Disabled;
            IsFlowPropertiesDialogOpen = true;
        }
    }

    private void CloseFlowPropertiesDialog()
    {
        IsFlowPropertiesDialogOpen = false;
    }

    private void SaveFlowProperties()
    {
        var flow = Flows.FirstOrDefault(f => f.Id == FlowPropertiesFlowId);
        if (flow != null)
        {
            flow.Label = FlowPropertiesLabel;
            flow.Info = FlowPropertiesInfo;
            flow.Disabled = !FlowPropertiesEnabled;
        }
        IsFlowPropertiesDialogOpen = false;
        HasUnsavedChanges = true;
        StateHasChanged();
    }

    private void EnableCurrentFlow()
    {
        var flow = Flows.FirstOrDefault(f => f.Id == CurrentFlowId);
        if (flow != null)
        {
            flow.Disabled = false;
            HasUnsavedChanges = true;
            IsMainMenuOpen = false;
            StateHasChanged();
        }
    }

    private void DisableCurrentFlow()
    {
        var flow = Flows.FirstOrDefault(f => f.Id == CurrentFlowId);
        if (flow != null)
        {
            flow.Disabled = true;
            HasUnsavedChanges = true;
            IsMainMenuOpen = false;
            StateHasChanged();
        }
    }

    private string GetSelectedNodeType()
    {
        if (SelectedDiagramNode?.AdditionalInfo?.TryGetValue("nodeType", out var typeObj) == true)
        {
            return typeObj as string ?? "unknown";
        }
        return "unknown";
    }

    private string GetSelectedNodeColor()
    {
        if (SelectedDiagramNode?.AdditionalInfo?.TryGetValue("color", out var colorObj) == true)
        {
            return colorObj as string ?? "#ddd";
        }
        return "#ddd";
    }

    // Property panel handlers
    private void CancelNodeEdit()
    {
        // Reload the original properties from the node
        if (SelectedDiagramNode != null)
        {
            LoadNodeProperties(SelectedDiagramNode);
        }
    }

    private void SaveNodeProperties()
    {
        if (SelectedDiagramNode != null)
        {
            // Update node name in labelAnnotation (not iconAnnotation)
            var labelAnnotation = SelectedDiagramNode.Annotations?.FirstOrDefault(a => a.ID == "labelAnnotation");
            if (labelAnnotation != null)
            {
                labelAnnotation.Content = SelectedNodeName;
            }

            // All nodes use dynamic property saving from SDK definitions
            SaveDynamicNodeProperties();
        }
    }

    /// <summary>
    /// Saves properties for SDK nodes dynamically to AdditionalInfo.
    /// </summary>
    private void SaveDynamicNodeProperties()
    {
        if (SelectedDiagramNode == null)
            return;

        // Initialize AdditionalInfo if null
        SelectedDiagramNode.AdditionalInfo ??= new Dictionary<string, object>();

        foreach (var (key, value) in _nodePropertyValues)
        {
            SelectedDiagramNode.AdditionalInfo[key] = value;
        }
    }

    private void SaveNodeProperty(string key, object value)
    {
        if (SelectedDiagramNode?.AdditionalInfo != null)
        {
            SelectedDiagramNode.AdditionalInfo[key] = value;
        }
    }

    // Inject trigger
    private async Task TriggerInjectNode()
    {
        if (SelectedDiagramNode?.ID != null && GetSelectedNodeType() == "inject")
        {
            // Check if flows are running
            if (FlowRuntime.State != FlowState.Running)
            {
                DebugMessages.Add(new DebugMessage
                {
                    NodeId = SelectedDiagramNode.ID,
                    NodeName = SelectedNodeName,
                    Data = "Please deploy and start the flows first",
                    Timestamp = DateTimeOffset.Now
                });
                StateHasChanged();
                return;
            }

            try
            {
                await FlowRuntime.InjectAsync(SelectedDiagramNode.ID);
                // Don't add message here - the debug node will show the output
                StateHasChanged();
            }
            catch (Exception ex)
            {
                DebugMessages.Add(new DebugMessage
                {
                    NodeId = SelectedDiagramNode.ID,
                    NodeName = SelectedNodeName,
                    Data = $"Error: {ex.Message}",
                    Timestamp = DateTimeOffset.Now
                });
                StateHasChanged();
            }
        }
    }

    // Zoom controls
    private void ZoomIn()
    {
        DiagramInstance?.Zoom(1.2, null);
    }

    private void ZoomOut()
    {
        DiagramInstance?.Zoom(0.8, null);
    }

    private void ZoomReset()
    {
        var currentZoom = DiagramInstance?.ScrollSettings?.CurrentZoom ?? 1;
        DiagramInstance?.Zoom(1 / currentZoom, null);
    }

    // =============== UNDO/REDO ===============
    
    /// <summary>
    /// Records an action for undo/redo.
    /// </summary>
    private void RecordAction(EditorAction action)
    {
        _undoStack.Push(action);
        // Remove oldest item if stack exceeds max size
        if (_undoStack.Count > MaxUndoStackSize)
        {
            // Convert to array, remove oldest (last), convert back to stack
            var items = _undoStack.ToArray();
            _undoStack = new Stack<EditorAction>(items.Take(MaxUndoStackSize).Reverse());
        }
        // Clear redo stack when a new action is recorded
        _redoStack.Clear();
    }

    /// <summary>
    /// Gets the currently selected nodes in the diagram.
    /// </summary>
    private List<Node> GetSelectedNodes()
    {
        var result = new List<Node>();
        var selectedNodes = DiagramInstance?.SelectionSettings?.Nodes;
        if (selectedNodes != null && selectedNodes.Count > 0)
        {
            result.AddRange(selectedNodes);
        }
        else if (SelectedDiagramNode != null)
        {
            result.Add(SelectedDiagramNode);
        }
        return result;
    }

    /// <summary>
    /// Undo the last action.
    /// </summary>
    private void Undo()
    {
        if (_undoStack.Count == 0) return;

        var action = _undoStack.Pop();
        _redoStack.Push(action);

        switch (action.Type)
        {
            case EditorActionType.AddNode:
                // Undo add = remove node
                if (DiagramNodes != null && action.NodeData != null)
                {
                    var node = DiagramNodes.FirstOrDefault(n => n.ID == action.NodeId);
                    if (node != null)
                    {
                        DiagramNodes.Remove(node);
                    }
                }
                break;

            case EditorActionType.DeleteNode:
                // Undo delete = restore node
                if (DiagramNodes != null && action.NodeData != null)
                {
                    DiagramNodes.Add(action.NodeData);
                }
                break;

            case EditorActionType.MoveNode:
                // Undo move = restore original position
                if (DiagramNodes != null && action.NodeId != null)
                {
                    var node = DiagramNodes.FirstOrDefault(n => n.ID == action.NodeId);
                    if (node != null)
                    {
                        node.OffsetX = action.OldX;
                        node.OffsetY = action.OldY;
                    }
                }
                break;

            case EditorActionType.EditNode:
                // Undo edit = restore old properties
                if (DiagramNodes != null && action.NodeId != null && action.OldProperties != null)
                {
                    var node = DiagramNodes.FirstOrDefault(n => n.ID == action.NodeId);
                    if (node != null)
                    {
                        foreach (var kv in action.OldProperties)
                        {
                            node.AdditionalInfo[kv.Key] = kv.Value;
                        }
                    }
                }
                break;
        }

        HasUnsavedChanges = true;
        StateHasChanged();
    }

    /// <summary>
    /// Redo the last undone action.
    /// </summary>
    private void Redo()
    {
        if (_redoStack.Count == 0) return;

        var action = _redoStack.Pop();
        _undoStack.Push(action);

        switch (action.Type)
        {
            case EditorActionType.AddNode:
                // Redo add = re-add node
                if (DiagramNodes != null && action.NodeData != null)
                {
                    DiagramNodes.Add(action.NodeData);
                }
                break;

            case EditorActionType.DeleteNode:
                // Redo delete = remove node again
                if (DiagramNodes != null && action.NodeData != null)
                {
                    var node = DiagramNodes.FirstOrDefault(n => n.ID == action.NodeId);
                    if (node != null)
                    {
                        DiagramNodes.Remove(node);
                    }
                }
                break;

            case EditorActionType.MoveNode:
                // Redo move = apply new position
                if (DiagramNodes != null && action.NodeId != null)
                {
                    var node = DiagramNodes.FirstOrDefault(n => n.ID == action.NodeId);
                    if (node != null)
                    {
                        node.OffsetX = action.NewX;
                        node.OffsetY = action.NewY;
                    }
                }
                break;

            case EditorActionType.EditNode:
                // Redo edit = apply new properties
                if (DiagramNodes != null && action.NodeId != null && action.NewProperties != null)
                {
                    var node = DiagramNodes.FirstOrDefault(n => n.ID == action.NodeId);
                    if (node != null)
                    {
                        foreach (var kv in action.NewProperties)
                        {
                            node.AdditionalInfo[kv.Key] = kv.Value;
                        }
                    }
                }
                break;
        }

        HasUnsavedChanges = true;
        StateHasChanged();
    }

    // =============== COPY/CUT/PASTE ===============

    /// <summary>
    /// Copy selected nodes to clipboard.
    /// </summary>
    private void CopySelection()
    {
        var selectedNodes = GetSelectedNodes();
        if (selectedNodes.Count == 0) return;

        var nodesToCopy = new List<object>();

        foreach (var node in selectedNodes)
        {
            nodesToCopy.Add(new
            {
                id = node.ID,
                type = node.AdditionalInfo.TryGetValue("nodeType", out var t) ? t?.ToString() : "unknown",
                x = node.OffsetX,
                y = node.OffsetY,
                name = node.Annotations?.FirstOrDefault(a => a.ID == "labelAnnotation")?.Content,
                properties = node.AdditionalInfo
            });
        }

        _nodeClipboard = System.Text.Json.JsonSerializer.Serialize(nodesToCopy);
        _clipboardIsCut = false;

        // Show notification
        DebugMessages.Insert(0, new DebugMessage
        {
            Timestamp = DateTime.Now,
            NodeId = "system",
            NodeName = "clipboard",
            Data = $"Copied {nodesToCopy.Count} node(s)"
        });

        StateHasChanged();
    }

    /// <summary>
    /// Cut selected nodes to clipboard.
    /// </summary>
    private void CutSelection()
    {
        CopySelection();
        _clipboardIsCut = true;
        DeleteSelection();
    }

    /// <summary>
    /// Paste nodes from clipboard.
    /// </summary>
    private void PasteFromClipboard()
    {
        if (string.IsNullOrEmpty(_nodeClipboard)) return;

        try
        {
            var nodeData = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(_nodeClipboard);
            if (nodeData == null || nodeData.Count == 0) return;

            int pastedCount = 0;
            double offsetX = 20, offsetY = 20; // Offset pasted nodes slightly

            foreach (var nodeJson in nodeData)
            {
                var type = nodeJson.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "unknown" : "unknown";
                var x = nodeJson.TryGetProperty("x", out var xProp) ? xProp.GetDouble() : 200;
                var y = nodeJson.TryGetProperty("y", out var yProp) ? yProp.GetDouble() : 200;
                var name = nodeJson.TryGetProperty("name", out var nProp) ? nProp.GetString() : "";

                // Find the palette node info
                var paletteNode = PaletteCategories
                    .SelectMany(c => c.Nodes)
                    .FirstOrDefault(n => n.Type == type);

                if (paletteNode != null)
                {
                    var nodeId = $"node{++NodeCount}";
                    var label = string.IsNullOrEmpty(name) ? paletteNode.Label : name;
                    var newNode = CreateNodeRedStyleNode(
                        nodeId,
                        x + offsetX,
                        y + offsetY,
                        type,
                        label,
                        paletteNode.Color,
                        paletteNode
                    );

                    // Copy properties from original
                    if (nodeJson.TryGetProperty("properties", out var propsJson))
                    {
                        foreach (var prop in propsJson.EnumerateObject())
                        {
                            newNode.AdditionalInfo[prop.Name] = prop.Value.ToString();
                        }
                    }

                    DiagramNodes!.Add(newNode);
                    pastedCount++;
                }
            }

            if (_clipboardIsCut)
            {
                _nodeClipboard = "";
                _clipboardIsCut = false;
            }

            HasUnsavedChanges = true;
            
            DebugMessages.Insert(0, new DebugMessage
            {
                Timestamp = DateTime.Now,
                NodeId = "system",
                NodeName = "clipboard",
                Data = $"Pasted {pastedCount} node(s)"
            });

            StateHasChanged();
        }
        catch (Exception ex)
        {
            DebugMessages.Insert(0, new DebugMessage
            {
                Timestamp = DateTime.Now,
                NodeId = "system",
                NodeName = "error",
                Data = $"Paste failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Delete selected nodes.
    /// </summary>
    private void DeleteSelection()
    {
        var selectedNodes = GetSelectedNodes();
        if (selectedNodes.Count == 0) return;

        // Create a copy since we're modifying the collection
        foreach (var node in selectedNodes.ToList())
        {
            // Record for undo
            RecordAction(new EditorAction
            {
                Type = EditorActionType.DeleteNode,
                NodeId = node.ID,
                NodeData = node
            });

            DiagramNodes!.Remove(node);
        }

        SelectedDiagramNode = null;
        HasUnsavedChanges = true;
        StateHasChanged();
    }

    private async Task OnDeployClick()
    {
        IsDeployMenuOpen = false;
        IsMainMenuOpen = false;
        
        // Build workspace from diagram
        BuildWorkspaceFromDiagram();
        
        await FlowStorage.SaveAsync(CurrentWorkspace);
        
        var deployType = DeployMode switch
        {
            "flows" => DeployType.Flows,
            "nodes" => DeployType.Nodes,
            _ => DeployType.Full
        };
        
        await FlowRuntime.DeployAsync(CurrentWorkspace, deployType);

        if (FlowRuntime.State != FlowState.Running)
        {
            await FlowRuntime.StartAsync();
        }

        HasUnsavedChanges = false;
        HasBeenDeployed = true;
        StateHasChanged();
    }

    // Menu toggle handlers
    private void ToggleMainMenu()
    {
        IsMainMenuOpen = !IsMainMenuOpen;
        IsDeployMenuOpen = false;
    }

    private void ToggleDeployMenu()
    {
        IsDeployMenuOpen = !IsDeployMenuOpen;
        IsMainMenuOpen = false;
    }

    private void SetDeployMode(string mode)
    {
        DeployMode = mode;
        IsDeployMenuOpen = false;
    }

    // Menu action handlers
    private void OnImportClick()
    {
        IsMainMenuOpen = false;
        ImportJson = "";
        IsImportDialogOpen = true;
    }

    private async Task OnExportClick()
    {
        IsMainMenuOpen = false;
        BuildWorkspaceFromDiagram();
        
        try
        {
            ExportJson = await FlowStorage.ExportAsync(CurrentWorkspace);
            if (ExportFormat == "minified")
            {
                // Minify JSON
                var obj = System.Text.Json.JsonSerializer.Deserialize<object>(ExportJson);
                ExportJson = System.Text.Json.JsonSerializer.Serialize(obj);
            }
        }
        catch
        {
            ExportJson = "{}";
        }
        
        IsExportDialogOpen = true;
    }

    private void CloseImportDialog()
    {
        IsImportDialogOpen = false;
        ImportJson = "";
    }

    private async Task ConfirmImport()
    {
        if (!string.IsNullOrWhiteSpace(ImportJson))
        {
            try
            {
                var importedWorkspace = await FlowStorage.ImportAsync(ImportJson);
                
                // Add imported flows to current workspace
                foreach (var flow in importedWorkspace.Flows)
                {
                    CurrentWorkspace.Flows.Add(flow);
                }
                
                // Rebuild diagram from imported data
                // For now, just add a debug message
                DebugMessages.Add(new DebugMessage
                {
                    NodeId = "system",
                    NodeName = "System",
                    Data = $"Imported {importedWorkspace.Flows.Count} flow(s)",
                    Timestamp = DateTimeOffset.Now
                });
                
                HasUnsavedChanges = true;
            }
            catch (Exception ex)
            {
                DebugMessages.Add(new DebugMessage
                {
                    NodeId = "system",
                    NodeName = "System",
                    Data = $"Import error: {ex.Message}",
                    Timestamp = DateTimeOffset.Now
                });
            }
        }
        
        IsImportDialogOpen = false;
        ImportJson = "";
    }

    private void CloseExportDialog()
    {
        IsExportDialogOpen = false;
        ExportJson = "";
    }

    private async Task CopyExportToClipboard()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", ExportJson);
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = "Copied to clipboard",
                Timestamp = DateTimeOffset.Now
            });
        }
        catch
        {
            // Clipboard API not available
        }
    }

    private async Task DownloadExport()
    {
        try
        {
            var fileName = $"flows_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var bytes = System.Text.Encoding.UTF8.GetBytes(ExportJson);
            var base64 = Convert.ToBase64String(bytes);
            
            await JSRuntime.InvokeVoidAsync("eval", 
                $"(function() {{ var a = document.createElement('a'); a.href = 'data:application/json;base64,{base64}'; a.download = '{fileName}'; a.click(); }})()");
        }
        catch
        {
            // Download failed
        }
    }

    // Search dialog state
    private bool IsSearchDialogOpen = false;
    private string SearchQuery = "";
    private List<SearchResult> SearchResults = new();

    private void OnSearchClick()
    {
        IsMainMenuOpen = false;
        SearchQuery = "";
        SearchResults.Clear();
        IsSearchDialogOpen = true;
    }

    private void CloseSearchDialog()
    {
        IsSearchDialogOpen = false;
    }

    private void PerformSearch()
    {
        SearchResults.Clear();
        
        if (string.IsNullOrWhiteSpace(SearchQuery))
            return;
        
        var query = SearchQuery.ToLower();
        
        // Search in nodes
        if (DiagramNodes != null)
        {
            foreach (var node in DiagramNodes)
            {
                var nodeName = node.Annotations?.FirstOrDefault(a => a.ID == "labelAnnotation")?.Content ?? "";
                var nodeType = node.AdditionalInfo?.TryGetValue("nodeType", out var typeObj) == true
                    ? typeObj as string ?? ""
                    : "";
                
                if (nodeName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    nodeType.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    node.ID?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                {
                    SearchResults.Add(new SearchResult
                    {
                        Type = "Node",
                        Id = node.ID ?? "",
                        Label = nodeName,
                        Description = $"Type: {nodeType}"
                    });
                }
            }
        }
        
        // Search in flows
        foreach (var flow in Flows)
        {
            if (flow.Label.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                SearchResults.Add(new SearchResult
                {
                    Type = "Flow",
                    Id = flow.Id,
                    Label = flow.Label,
                    Description = "Flow"
                });
            }
        }
    }

    private async Task SelectSearchResult(SearchResult result)
    {
        if (result.Type == "Node" && DiagramInstance != null)
        {
            var node = DiagramNodes?.FirstOrDefault(n => n.ID == result.Id);
            if (node != null)
            {
                DiagramInstance.ClearSelection();
                DiagramInstance.Select(new ObservableCollection<IDiagramObject> { node });
                await HighlightDebugNode(result.Id);
            }
        }
        else if (result.Type == "Flow")
        {
            SwitchFlow(result.Id);
        }
        
        IsSearchDialogOpen = false;
    }

    // Configuration nodes panel state
    private bool IsConfigNodesDialogOpen = false;
    private List<ConfigNodeInfo> ConfigNodes = new();

    private void OnConfigNodesClick()
    {
        IsMainMenuOpen = false;
        RefreshConfigNodes();
        IsConfigNodesDialogOpen = true;
    }

    private void CloseConfigNodesDialog()
    {
        IsConfigNodesDialogOpen = false;
    }

    private void RefreshConfigNodes()
    {
        ConfigNodes.Clear();
        
        // Get configuration nodes from the current workspace
        if (CurrentWorkspace.ConfigNodes != null)
        {
            foreach (var configNode in CurrentWorkspace.ConfigNodes)
            {
                ConfigNodes.Add(new ConfigNodeInfo
                {
                    Id = configNode.Id,
                    Type = configNode.Type,
                    Label = !string.IsNullOrEmpty(configNode.Name) ? configNode.Name : configNode.Type
                });
            }
        }
        
        // Also check for config nodes in the current flow
        if (DiagramNodes != null)
        {
            foreach (var node in DiagramNodes)
            {
                var nodeType = node.AdditionalInfo?.TryGetValue("nodeType", out var typeObj) == true
                    ? typeObj as string ?? ""
                    : "";
                
                // Configuration nodes typically have types ending in "-config" or specific config types
                if (nodeType.EndsWith("-config") || nodeType.Contains("config"))
                {
                    var label = node.Annotations?.FirstOrDefault(a => a.ID == "labelAnnotation")?.Content ?? nodeType;
                    if (!ConfigNodes.Any(cn => cn.Id == node.ID))
                    {
                        ConfigNodes.Add(new ConfigNodeInfo
                        {
                            Id = node.ID ?? "",
                            Type = nodeType,
                            Label = label
                        });
                    }
                }
            }
        }
    }

    // Flows management panel state
    private bool IsFlowsDialogOpen = false;

    private void OnFlowsClick()
    {
        IsMainMenuOpen = false;
        IsFlowsDialogOpen = true;
    }

    private void CloseFlowsDialog()
    {
        IsFlowsDialogOpen = false;
    }

    private void AddFlowFromDialog()
    {
        AddNewFlow();
        StateHasChanged();
    }

    private void DeleteFlowFromDialog(string flowId)
    {
        DeleteFlow(flowId);
        StateHasChanged();
    }

    private void EditFlowFromDialog(string flowId)
    {
        IsFlowsDialogOpen = false;
        EditFlowProperties(flowId);
    }

    // Subflows panel state
    private bool IsSubflowsDialogOpen = false;
    private List<SubflowInfo> Subflows = new();

    private void OnSubflowsClick()
    {
        IsMainMenuOpen = false;
        RefreshSubflows();
        IsSubflowsDialogOpen = true;
    }

    private void CloseSubflowsDialog()
    {
        IsSubflowsDialogOpen = false;
    }

    private void RefreshSubflows()
    {
        Subflows.Clear();
        
        // In the full implementation, subflows would be stored in the workspace
        // For now, we can create a basic structure
        // Subflows in Node-RED are special flow tabs that can be instantiated as nodes
    }

    private void CreateSubflow()
    {
        // Create a new subflow
        var subflowId = $"subflow_{Guid.NewGuid():N}";
        var subflowName = $"Subflow {Subflows.Count + 1}";
        
        var newSubflow = new SubflowInfo
        {
            Id = subflowId,
            Name = subflowName,
            Inputs = 1,
            Outputs = 1,
            Description = "A reusable subflow",
            Category = "subflows",
            Color = "#DDAA99"
        };
        
        Subflows.Add(newSubflow);
        
        // Create a new flow tab for the subflow
        var newFlow = new FlowTab
        {
            Id = subflowId,
            Label = subflowName,
            Disabled = false,
            Info = "Subflow editor - add nodes here to define the subflow logic"
        };
        Flows.Add(newFlow);
        
        // Add the subflow as a node type to the palette
        AddSubflowToPalette(newSubflow);
        
        // Save current flow state before switching
        SaveCurrentFlowState();
        
        // Switch to the subflow tab
        CurrentFlowId = subflowId;
        DiagramNodes?.Clear();
        DiagramConnectors?.Clear();
        Groups.Clear();
        
        // Add input and output nodes for the subflow
        AddSubflowIONodes(newSubflow);
        
        DebugMessages.Add(new DebugMessage
        {
            NodeId = "system",
            NodeName = "System",
            Data = $"Created subflow '{subflowName}'. Add nodes between the input and output to define the subflow logic. The subflow is now available in the palette under 'subflows'.",
            Timestamp = DateTimeOffset.Now
        });
        
        HasUnsavedChanges = true;
        StateHasChanged();
    }
    
    private void AddSubflowIONodes(SubflowInfo subflow)
    {
        // Add subflow input node
        var inputNode = new Node
        {
            ID = $"{subflow.Id}_in",
            OffsetX = 150,
            OffsetY = 200,
            Width = 80,
            Height = 25,
            Shape = new BasicShape { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 3 },
            Style = new ShapeStyle { Fill = "#A6BBCF", StrokeColor = "#7B9BAC", StrokeWidth = 1 },
            AdditionalInfo = new Dictionary<string, object?>
            {
                { "nodeType", "subflow-in" },
                { "subflowId", subflow.Id }
            }
        };
        inputNode.Annotations = new DiagramObjectCollection<ShapeAnnotation>
        {
            new ShapeAnnotation { ID = "iconAnnotation", Content = "→", Style = new TextStyle { Color = "#333", FontSize = 12 } },
            new ShapeAnnotation { ID = "labelAnnotation", Content = "Input", Style = new TextStyle { Color = "#333", FontSize = 12 }, Offset = new DiagramPoint { X = 0.5, Y = 0.5 } }
        };
        inputNode.Ports = new DiagramObjectCollection<PointPort>
        {
            new PointPort
            {
                ID = "output",
                Offset = new DiagramPoint { X = 1, Y = 0.5 },
                Visibility = PortVisibility.Visible,
                Height = 8,
                Width = 8,
                Style = new ShapeStyle { Fill = "#333", StrokeColor = "#333" }
            }
        };
        DiagramNodes?.Add(inputNode);
        
        // Add subflow output node
        var outputNode = new Node
        {
            ID = $"{subflow.Id}_out",
            OffsetX = 500,
            OffsetY = 200,
            Width = 80,
            Height = 25,
            Shape = new BasicShape { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 3 },
            Style = new ShapeStyle { Fill = "#A6BBCF", StrokeColor = "#7B9BAC", StrokeWidth = 1 },
            AdditionalInfo = new Dictionary<string, object?>
            {
                { "nodeType", "subflow-out" },
                { "subflowId", subflow.Id }
            }
        };
        outputNode.Annotations = new DiagramObjectCollection<ShapeAnnotation>
        {
            new ShapeAnnotation { ID = "iconAnnotation", Content = "←", Style = new TextStyle { Color = "#333", FontSize = 12 } },
            new ShapeAnnotation { ID = "labelAnnotation", Content = "Output", Style = new TextStyle { Color = "#333", FontSize = 12 }, Offset = new DiagramPoint { X = 0.5, Y = 0.5 } }
        };
        outputNode.Ports = new DiagramObjectCollection<PointPort>
        {
            new PointPort
            {
                ID = "input",
                Offset = new DiagramPoint { X = 0, Y = 0.5 },
                Visibility = PortVisibility.Visible,
                Height = 8,
                Width = 8,
                Style = new ShapeStyle { Fill = "#333", StrokeColor = "#333" }
            }
        };
        DiagramNodes?.Add(outputNode);
    }
    
    private void AddSubflowToPalette(SubflowInfo subflow)
    {
        // Add subflow to the "subflows" category in the palette
        var subflowCategory = PaletteCategories.FirstOrDefault(c => c.Name == "subflows");
        if (subflowCategory == null)
        {
            // Create the subflows category if it doesn't exist
            subflowCategory = new PaletteCategory
            {
                Name = "subflows",
                IsExpanded = true,
                Nodes = new List<PaletteNodeInfo>()
            };
            PaletteCategories.Add(subflowCategory);
        }
        
        // Add the subflow as a palette node
        var paletteNode = new PaletteNodeInfo
        {
            Type = $"subflow:{subflow.Id}",
            Label = subflow.Name,
            Color = subflow.Color,
            IconClass = "fa fa-th-large",
            Inputs = subflow.Inputs,
            Outputs = subflow.Outputs
        };
        subflowCategory.Nodes.Add(paletteNode);
        
        DebugMessages.Add(new DebugMessage
        {
            NodeId = "system",
            NodeName = "System",
            Data = $"Subflow '{subflow.Name}' added to palette. Drag it from the 'subflows' category onto any flow to use it.",
            Timestamp = DateTimeOffset.Now
        });
    }
    
    private void CreateSubflowFromSelection()
    {
        var selectedNodes = GetSelectedNodes();
        
        if (selectedNodes.Count == 0)
        {
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = "Please select nodes in the workspace first to convert them to a subflow.",
                Timestamp = DateTimeOffset.Now
            });
            return;
        }
        
        var subflowId = $"subflow_{Guid.NewGuid():N}";
        var subflowName = $"Subflow {Subflows.Count + 1}";
        
        // Determine inputs and outputs based on unconnected ports
        int inputs = 0;
        int outputs = 0;
        var nodeIds = selectedNodes.Select(n => n.ID).ToList();
        
        foreach (var node in selectedNodes)
        {
            // Check for incoming connections from outside the selection
            var incomingFromOutside = DiagramConnectors?.Any(c => 
                c.TargetID == node.ID && 
                !nodeIds.Contains(c.SourceID)) ?? false;
            if (incomingFromOutside) inputs = 1;
            
            // Check for outgoing connections to outside the selection
            var outgoingToOutside = DiagramConnectors?.Any(c => 
                c.SourceID == node.ID && 
                !nodeIds.Contains(c.TargetID)) ?? false;
            if (outgoingToOutside) outputs = 1;
        }
        
        // Default to 1 input and 1 output if no connections found
        if (inputs == 0) inputs = 1;
        if (outputs == 0) outputs = 1;
        
        var newSubflow = new SubflowInfo
        {
            Id = subflowId,
            Name = subflowName,
            Inputs = inputs,
            Outputs = outputs,
            Description = $"Created from {selectedNodes.Count} selected node(s)",
            Category = "subflows",
            Color = "#DDAA99",
            NodeIds = nodeIds
        };
        
        Subflows.Add(newSubflow);
        
        // Create a new flow tab for the subflow
        var newFlow = new FlowTab
        {
            Id = subflowId,
            Label = subflowName,
            Disabled = false,
            Info = $"Subflow created from selection - {selectedNodes.Count} nodes"
        };
        Flows.Add(newFlow);
        
        // Remove selected nodes from current flow (they're now in the subflow)
        foreach (var node in selectedNodes)
        {
            DiagramNodes?.Remove(node);
        }
        
        // Remove connectors between the removed nodes
        var connectorsToRemove = DiagramConnectors?
            .Where(c => nodeIds.Contains(c.SourceID) || nodeIds.Contains(c.TargetID))
            .ToList() ?? new List<Connector>();
        foreach (var connector in connectorsToRemove)
        {
            DiagramConnectors?.Remove(connector);
        }
        
        // Create a subflow instance node in place of the removed nodes
        // Note: selectedNodes.Count > 0 is guaranteed by earlier check
        var avgX = selectedNodes.Count > 0 ? selectedNodes.Average(n => n.OffsetX) : 300.0;
        var avgY = selectedNodes.Count > 0 ? selectedNodes.Average(n => n.OffsetY) : 200.0;
        
        var subflowInstanceId = $"subflow_instance_{Guid.NewGuid():N}";
        var subflowInstanceNode = CreateNodeRedStyleNode(subflowInstanceId, avgX, avgY, "subflow:" + subflowId, subflowName, "#DDAA99", null);
        DiagramNodes?.Add(subflowInstanceNode);
        
        AddSubflowToPalette(newSubflow);
        
        DebugMessages.Add(new DebugMessage
        {
            NodeId = "system",
            NodeName = "System",
            Data = $"Created subflow '{subflowName}' from {selectedNodes.Count} selected node(s). A subflow instance has been placed in the current flow.",
            Timestamp = DateTimeOffset.Now
        });
        
        HasUnsavedChanges = true;
        IsSubflowsDialogOpen = false;
        StateHasChanged();
    }

    private void EditSubflow(string subflowId)
    {
        var subflow = Subflows.FirstOrDefault(sf => sf.Id == subflowId);
        if (subflow != null)
        {
            // Use proper tab switching which saves/restores state
            SwitchFlow(subflowId);
            
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Editing subflow '{subflow.Name}'. Add nodes between Input and Output to define the logic.",
                Timestamp = DateTimeOffset.Now
            });
            
            IsSubflowsDialogOpen = false;
            StateHasChanged();
        }
    }
    
    private void LoadFlowNodes(string flowId)
    {
        // This method is now deprecated - use RestoreFlowState instead
        // Keeping for backward compatibility but it will use restore logic
        RestoreFlowState(flowId);
    }

    private void DeleteSubflow(string subflowId)
    {
        var subflow = Subflows.FirstOrDefault(sf => sf.Id == subflowId);
        if (subflow != null)
        {
            // Remove the flow tab for this subflow
            var flowTab = Flows.FirstOrDefault(f => f.Id == subflowId);
            if (flowTab != null)
            {
                Flows.Remove(flowTab);
            }
            
            // If we're currently viewing this subflow, switch to a regular flow
            if (CurrentFlowId == subflowId)
            {
                var firstRegularFlow = Flows.FirstOrDefault(f => !Subflows.Any(sf => sf.Id == f.Id));
                if (firstRegularFlow != null)
                {
                    CurrentFlowId = firstRegularFlow.Id;
                }
                else if (Flows.Count > 0)
                {
                    CurrentFlowId = Flows[0].Id;
                }
                else
                {
                    // Create a new flow if none exist
                    AddNewFlow();
                }
            }
            
            Subflows.Remove(subflow);
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Deleted subflow '{subflow.Name}'. Any instances in flows will need to be removed manually.",
                Timestamp = DateTimeOffset.Now
            });
            HasUnsavedChanges = true;
            StateHasChanged();
        }
    }

    // Groups panel state
    private bool IsGroupsDialogOpen = false;
    private List<GroupInfo> Groups = new();

    private void OnGroupsClick()
    {
        IsMainMenuOpen = false;
        RefreshGroups();
        IsGroupsDialogOpen = true;
    }

    private void CloseGroupsDialog()
    {
        IsGroupsDialogOpen = false;
    }

    private void RefreshGroups()
    {
        Groups.Clear();
        
        // In a full implementation, groups would be tracked in the diagram
        // Groups are visual containers for organizing nodes
    }

    private void GroupSelectedNodes()
    {
        // Get all selected nodes using the helper
        var selectedNodes = GetSelectedNodes();
        
        if (selectedNodes.Count > 0)
        {
            var groupId = $"group_{Guid.NewGuid():N}";
            var groupName = $"Group {Groups.Count + 1}";
            
            // Calculate bounding box for the group - initialize with first node
            var firstNode = selectedNodes[0];
            double minX = (firstNode.OffsetX) - (firstNode.Width ?? 100.0) / 2;
            double minY = (firstNode.OffsetY) - (firstNode.Height ?? 30.0) / 2;
            double maxX = (firstNode.OffsetX) + (firstNode.Width ?? 100.0) / 2;
            double maxY = (firstNode.OffsetY) + (firstNode.Height ?? 30.0) / 2;
            var nodeIds = new List<string> { firstNode.ID };
            
            foreach (var node in selectedNodes.Skip(1))
            {
                nodeIds.Add(node.ID);
                double nodeMinX = (node.OffsetX) - (node.Width ?? 100.0) / 2;
                double nodeMinY = (node.OffsetY) - (node.Height ?? 30.0) / 2;
                double nodeMaxX = (node.OffsetX) + (node.Width ?? 100.0) / 2;
                double nodeMaxY = (node.OffsetY) + (node.Height ?? 30.0) / 2;
                if (nodeMinX < minX) minX = nodeMinX;
                if (nodeMinY < minY) minY = nodeMinY;
                if (nodeMaxX > maxX) maxX = nodeMaxX;
                if (nodeMaxY > maxY) maxY = nodeMaxY;
            }
            
            // Add padding around the group
            var padding = 20.0;
            minX -= padding;
            minY -= padding;
            maxX += padding;
            maxY += padding;
            
            // Create visual group node (rectangle behind the nodes)
            // Using regular Node instead of NodeGroup to avoid connector issues
            var groupNode = new Node
            {
                ID = groupId,
                OffsetX = minX + (maxX - minX) / 2,
                OffsetY = minY + (maxY - minY) / 2,
                Width = maxX - minX,
                Height = maxY - minY,
                Shape = new BasicShape { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 5 },
                Style = new ShapeStyle
                {
                    Fill = DefaultGroupFillColor,
                    StrokeColor = DefaultGroupStrokeColor,
                    StrokeWidth = 2,
                    StrokeDashArray = "5,3"
                },
                ZIndex = -1, // Behind other nodes
                Ports = new DiagramObjectCollection<PointPort>(), // Empty ports - groups don't have connections
                Constraints = GroupNodeConstraints // No connections - groups don't have ports
            };
            
            // Add label annotation for the group name
            groupNode.Annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation
                {
                    ID = "groupLabel",
                    Content = groupName,
                    Style = new TextStyle { Color = "#666", FontSize = 11, Bold = true },
                    Offset = new DiagramPoint { X = 0, Y = 0 },
                    Margin = new DiagramThickness { Left = 5, Top = 5, Right = 0, Bottom = 0 },
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                }
            };
            
            DiagramNodes?.Add(groupNode);
            
            var newGroup = new GroupInfo
            {
                Id = groupId,
                Name = groupName,
                NodeCount = selectedNodes.Count,
                NodeIds = nodeIds,
                Color = "#FFCCCC",
                X = minX,
                Y = minY,
                Width = maxX - minX,
                Height = maxY - minY,
                DiagramNodeId = groupId
            };
            
            Groups.Add(newGroup);
            
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Created group '{groupName}' containing {selectedNodes.Count} node(s).",
                Timestamp = DateTimeOffset.Now
            });
            
            HasUnsavedChanges = true;
            StateHasChanged();
        }
        else
        {
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = "Please select nodes to group. Select multiple nodes by holding Ctrl while clicking.",
                Timestamp = DateTimeOffset.Now
            });
        }
    }

    private void UngroupSelectedNodes()
    {
        // First check if we have a group selected (via its visual node)
        var selectedNodes = GetSelectedNodes();
        GroupInfo? groupToRemove = null;
        
        // Check if any selected node is a group node
        foreach (var node in selectedNodes)
        {
            var group = Groups.FirstOrDefault(g => g.DiagramNodeId == node.ID);
            if (group != null)
            {
                groupToRemove = group;
                break;
            }
        }
        
        // If no group node selected, try to find a group containing the selected node
        if (groupToRemove == null && SelectedDiagramNode != null)
        {
            groupToRemove = Groups.FirstOrDefault(g => g.NodeIds.Contains(SelectedDiagramNode.ID));
        }
        
        // If still no group found, just remove the last group
        if (groupToRemove == null && Groups.Count > 0)
        {
            groupToRemove = Groups.Last();
        }
        
        if (groupToRemove != null)
        {
            // Remove the visual group node from the diagram
            var groupNode = DiagramNodes?.FirstOrDefault(n => n.ID == groupToRemove.DiagramNodeId);
            if (groupNode != null)
            {
                DiagramNodes?.Remove(groupNode);
            }
            
            Groups.Remove(groupToRemove);
            
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Ungrouped '{groupToRemove.Name}'. {groupToRemove.NodeCount} node(s) are now independent.",
                Timestamp = DateTimeOffset.Now
            });
            
            HasUnsavedChanges = true;
            StateHasChanged();
        }
        else
        {
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = "No groups to ungroup.",
                Timestamp = DateTimeOffset.Now
            });
        }
    }

    private void EditGroup(string groupId)
    {
        var group = Groups.FirstOrDefault(g => g.Id == groupId);
        if (group != null)
        {
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Editing group '{group.Name}'. In a full implementation, this would open group properties.",
                Timestamp = DateTimeOffset.Now
            });
        }
    }

    private void DeleteGroup(string groupId)
    {
        var group = Groups.FirstOrDefault(g => g.Id == groupId);
        if (group != null)
        {
            // Remove the visual group node from the diagram
            var groupNode = DiagramNodes?.FirstOrDefault(n => n.ID == group.DiagramNodeId);
            if (groupNode != null)
            {
                DiagramNodes?.Remove(groupNode);
            }
            
            Groups.Remove(group);
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Deleted group '{group.Name}'. Contained nodes are now independent.",
                Timestamp = DateTimeOffset.Now
            });
            HasUnsavedChanges = true;
            StateHasChanged();
        }
    }

    // Palette management panel state
    private bool IsPaletteDialogOpen = false;
    private string PaletteSearchQuery = "";
    private List<PaletteModuleInfo> AvailableModules = new();

    private void OnManagePaletteClick()
    {
        IsMainMenuOpen = false;
        RefreshPaletteModules();
        IsPaletteDialogOpen = true;
    }

    private void ClosePaletteDialog()
    {
        IsPaletteDialogOpen = false;
    }

    private void RefreshPaletteModules()
    {
        AvailableModules.Clear();
        
        // Get currently loaded modules
        var nodeDefinitions = NodeLoader.GetNodeDefinitions();
        var modules = nodeDefinitions
            .Select(n => new
            {
                // Extract module name - handle both single and compound names
                Module = n.Type.Contains('-') ? 
                    string.Join("-", n.Type.Split('-').Take(n.Type.Split('-').Length - 1)) : 
                    "core",
                NodeType = n.Type
            })
            .GroupBy(x => x.Module)
            .Select(g => new PaletteModuleInfo
            {
                Name = g.Key,
                Version = "Unknown", // Version info would come from package metadata
                NodeCount = g.Count(),
                IsInstalled = true
            })
            .ToList();
        
        AvailableModules.AddRange(modules);
    }

    private void InstallPaletteModule(string moduleName)
    {
        // In a production implementation, this would:
        // 1. Call npm install <moduleName> or a package manager API
        // 2. Reload the node definitions
        // 3. Update the palette
        
        // For demonstration, we'll simulate a successful installation
        var newModule = new PaletteModuleInfo
        {
            Name = moduleName,
            Version = "1.0.0",
            NodeCount = 1,
            IsInstalled = true
        };
        
        if (!AvailableModules.Any(m => m.Name == moduleName))
        {
            AvailableModules.Add(newModule);
            
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Module '{moduleName}' installed successfully. In production, this would integrate with npm/package manager.",
                Timestamp = DateTimeOffset.Now
            });
        }
        else
        {
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Module '{moduleName}' is already installed.",
                Timestamp = DateTimeOffset.Now
            });
        }
        
        StateHasChanged();
    }

    private void UninstallPaletteModule(string moduleName)
    {
        // In a production implementation, this would:
        // 1. Call npm uninstall <moduleName> or a package manager API
        // 2. Remove the nodes from the palette
        // 3. Update the node registry
        
        var module = AvailableModules.FirstOrDefault(m => m.Name == moduleName);
        if (module != null)
        {
            AvailableModules.Remove(module);
            
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Module '{moduleName}' uninstalled successfully. In production, this would integrate with npm/package manager.",
                Timestamp = DateTimeOffset.Now
            });
            
            StateHasChanged();
        }
        else
        {
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Module '{moduleName}' not found.",
                Timestamp = DateTimeOffset.Now
            });
        }
    }

    // Settings dialog state
    private bool IsSettingsDialogOpen = false;
    private bool SettingsShowGrid = true;
    private bool SettingsSnapToGrid = true;
    private int SettingsGridSize = 20;

    private void OnSettingsClick()
    {
        IsMainMenuOpen = false;
        IsSettingsDialogOpen = true;
    }

    private void CloseSettingsDialog()
    {
        IsSettingsDialogOpen = false;
    }

    private void SaveSettings()
    {
        // Apply settings to diagram
        if (DiagramInstance != null)
        {
            // Settings would be applied here
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = "Settings updated successfully.",
                Timestamp = DateTimeOffset.Now
            });
        }
        IsSettingsDialogOpen = false;
    }

    // Keyboard shortcuts dialog state
    private bool IsKeyboardShortcutsDialogOpen = false;
    private List<KeyboardShortcut> KeyboardShortcuts = new();

    private void OnKeyboardShortcutsClick()
    {
        IsMainMenuOpen = false;
        LoadKeyboardShortcuts();
        IsKeyboardShortcutsDialogOpen = true;
    }

    private void CloseKeyboardShortcutsDialog()
    {
        IsKeyboardShortcutsDialogOpen = false;
    }

    private void LoadKeyboardShortcuts()
    {
        KeyboardShortcuts = new List<KeyboardShortcut>
        {
            new KeyboardShortcut { Key = "Ctrl+.", Description = "Search flows" },
            new KeyboardShortcut { Key = "Ctrl+E", Description = "Export" },
            new KeyboardShortcut { Key = "Ctrl+I", Description = "Import" },
            new KeyboardShortcut { Key = "Ctrl+S", Description = "Deploy" },
            new KeyboardShortcut { Key = "Ctrl+Z", Description = "Undo" },
            new KeyboardShortcut { Key = "Ctrl+Y", Description = "Redo" },
            new KeyboardShortcut { Key = "Ctrl+A", Description = "Select all nodes" },
            new KeyboardShortcut { Key = "Delete", Description = "Delete selected nodes" },
            new KeyboardShortcut { Key = "Ctrl+C", Description = "Copy selected nodes" },
            new KeyboardShortcut { Key = "Ctrl+X", Description = "Cut selected nodes" },
            new KeyboardShortcut { Key = "Ctrl+V", Description = "Paste nodes" },
        };
    }

    private async Task OnStartClick()
    {
        IsMainMenuOpen = false;
        BuildWorkspaceFromDiagram();
        await FlowRuntime.LoadAsync(CurrentWorkspace);
        await FlowRuntime.StartAsync();
        
        DebugMessages.Add(new DebugMessage
        {
            NodeId = "system",
            NodeName = "System",
            Data = "Flows started",
            Timestamp = DateTimeOffset.Now
        });
        
        StateHasChanged();
    }

    private async Task OnStopClick()
    {
        IsMainMenuOpen = false;
        await FlowRuntime.StopAsync();
        
        DebugMessages.Add(new DebugMessage
        {
            NodeId = "system",
            NodeName = "System",
            Data = "Flows stopped",
            Timestamp = DateTimeOffset.Now
        });
        
        StateHasChanged();
    }

    private async Task OnRestartClick()
    {
        IsMainMenuOpen = false;
        await FlowRuntime.RestartAsync();
        
        DebugMessages.Add(new DebugMessage
        {
            NodeId = "system",
            NodeName = "System",
            Data = "Flows restarted",
            Timestamp = DateTimeOffset.Now
        });
        
        StateHasChanged();
    }

    private void SetExportFormat(string format)
    {
        ExportFormat = format;
    }

    private void BuildWorkspaceFromDiagram()
    {
        // Build workspace from current diagram state
        CurrentWorkspace = new Workspace
        {
            Id = CurrentFlowId,
            Name = "My Workspace",
            Flows = new List<Flow>()
        };

        // Create a flow from the current diagram nodes
        var flow = new Flow
        {
            Id = CurrentFlowId,
            Label = Flows.FirstOrDefault(f => f.Id == CurrentFlowId)?.Label ?? "Flow 1",
            Nodes = new List<FlowNode>()
        };

        // Build a map of node IDs for wire lookups
        var nodeIdMap = new Dictionary<string, FlowNode>();

        // Add nodes from diagram
        if (DiagramNodes != null)
        {
            foreach (var node in DiagramNodes)
            {
                var nodeType = node.AdditionalInfo?.TryGetValue("nodeType", out var typeObj) == true
                    ? typeObj as string ?? "unknown"
                    : "unknown";

                var flowNode = new FlowNode
                {
                    Id = node.ID ?? "",
                    Type = nodeType,
                    Name = node.Annotations?.FirstOrDefault(a => a.ID == "labelAnnotation")?.Content ?? "",
                    X = node.OffsetX,
                    Y = node.OffsetY,
                    FlowId = CurrentFlowId,
                    Wires = new List<List<string>>()
                };

                // Copy config from AdditionalInfo (excluding meta fields)
                if (node.AdditionalInfo != null)
                {
                    foreach (var kvp in node.AdditionalInfo)
                    {
                        if (kvp.Key != "nodeType" && kvp.Key != "color" && kvp.Key != "iconClass" && 
                            kvp.Key != "hasInput" && kvp.Key != "hasOutput")
                        {
                            flowNode.Config[kvp.Key] = kvp.Value;
                        }
                    }
                }

                flow.Nodes.Add(flowNode);
                nodeIdMap[flowNode.Id] = flowNode;
            }
        }

        // Add wires from connectors - wires are stored in the source node
        if (DiagramConnectors != null)
        {
            foreach (var connector in DiagramConnectors)
            {
                if (!string.IsNullOrEmpty(connector.SourceID) && !string.IsNullOrEmpty(connector.TargetID))
                {
                    if (nodeIdMap.TryGetValue(connector.SourceID, out var sourceNode))
                    {
                        // Ensure we have at least one output wire array
                        if (sourceNode.Wires.Count == 0)
                        {
                            sourceNode.Wires.Add(new List<string>());
                        }
                        
                        // Add target node to the first output (port 0)
                        sourceNode.Wires[0].Add(connector.TargetID);
                    }
                }
            }
        }

        CurrentWorkspace.Flows.Add(flow);
    }

    private void OnDebugMessage(DebugMessage message)
    {
        DebugMessages.Add(message);
        InvokeAsync(StateHasChanged);
    }

    private void OnNodeStatusChanged(string nodeId, NodeStatus status)
    {
        _nodeStatuses[nodeId] = status;
        
        // Update the status annotation on the diagram node
        var node = DiagramNodes?.FirstOrDefault(n => n.ID == nodeId);
        if (node?.Annotations != null && node.Annotations.Count >= 3)
        {
            var statusAnnotation = node.Annotations.FirstOrDefault(a => a.ID == "statusAnnotation");
            if (statusAnnotation != null)
            {
                // Build status text with indicator
                var statusText = "";
                if (!string.IsNullOrEmpty(status.Text))
                {
                    var indicator = status.Shape == StatusShape.Ring ? "○" : "●";
                    statusText = $"{indicator} {status.Text}";
                }
                statusAnnotation.Content = statusText;
                
                // Set color based on status
                if (statusAnnotation.Style != null)
                {
                    statusAnnotation.Style.Color = GetStatusColor(status.Color);
                }
            }
        }
        
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Gets the status for a node.
    /// </summary>
    private NodeStatus? GetNodeStatus(string nodeId)
    {
        return _nodeStatuses.TryGetValue(nodeId, out var status) ? status : null;
    }

    /// <summary>
    /// Gets the help text for a node type from SDK definitions.
    /// </summary>
    private NodeHelpText? GetNodeHelp(string nodeType)
    {
        _cachedNodeDefinitions ??= NodeLoader.GetNodeDefinitions().ToList();
        var def = _cachedNodeDefinitions.FirstOrDefault(d => d.Type == nodeType);
        return def?.Help;
    }

    /// <summary>
    /// Gets the CSS color for a node status.
    /// </summary>
    private string GetStatusColor(StatusColor color)
    {
        return color switch
        {
            StatusColor.Red => "#c00",
            StatusColor.Green => "#5a8",
            StatusColor.Yellow => "#f90",
            StatusColor.Blue => "#53a3f3",
            StatusColor.Grey => "#999",
            _ => "#999"
        };
    }

    private void ClearDebugMessages()
    {
        DebugMessages.Clear();
    }

    private void FilterDebugMessages()
    {
        // Toggle filter dialog or apply filter
        // For now, we'll implement a simple filter by node name
        DebugFilterByNode = !DebugFilterByNode;
    }

    private IEnumerable<DebugMessage> GetFilteredDebugMessages()
    {
        var messages = DebugMessages.AsEnumerable();
        
        if (!string.IsNullOrWhiteSpace(DebugMessageFilter))
        {
            messages = messages.Where(m => 
                m.NodeName.Contains(DebugMessageFilter, StringComparison.OrdinalIgnoreCase) ||
                FormatDebugData(m.Data).Contains(DebugMessageFilter, StringComparison.OrdinalIgnoreCase));
        }
        
        return messages.TakeLast(100).Reverse();
    }

    // Node highlighting constants
    private const string HighlightColor = "#ffff00";
    private const int HighlightFlashDuration = 200;

    private async Task HighlightDebugNode(string nodeId)
    {
        // Find and select the node in the diagram
        var node = DiagramNodes?.FirstOrDefault(n => n.ID == nodeId);
        if (node != null && DiagramInstance != null)
        {
            // Select the node
            DiagramInstance.ClearSelection();
            DiagramInstance.Select(new ObservableCollection<IDiagramObject> { node });
            
            // Flash the node to draw attention
            if (node.Style != null)
            {
                var originalColor = node.Style.Fill;
                node.Style.Fill = HighlightColor;
                StateHasChanged();
                await Task.Delay(HighlightFlashDuration);
                node.Style.Fill = originalColor;
                StateHasChanged();
                await Task.Delay(HighlightFlashDuration);
                node.Style.Fill = HighlightColor;
                StateHasChanged();
                await Task.Delay(HighlightFlashDuration);
                node.Style.Fill = originalColor;
                StateHasChanged();
            }
        }
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

    // Helper classes
    private class FlowTab
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string Info { get; set; } = "";
        public bool Disabled { get; set; } = false;
        
        /// <summary>
        /// Serialized nodes for this flow - stores diagram state when switching tabs
        /// </summary>
        public List<FlowNodeData> StoredNodes { get; set; } = new();
        
        /// <summary>
        /// Serialized connectors for this flow - stores diagram state when switching tabs
        /// </summary>
        public List<FlowConnectorData> StoredConnectors { get; set; } = new();
        
        /// <summary>
        /// Node counter for unique IDs within this flow
        /// </summary>
        public int NodeCounter { get; set; } = 0;
        
        /// <summary>
        /// Connector counter for unique IDs within this flow
        /// </summary>
        public int ConnectorCounter { get; set; } = 0;
        
        /// <summary>
        /// Groups for this flow
        /// </summary>
        public List<GroupInfo> Groups { get; set; } = new();
    }
    
    /// <summary>
    /// Serializable node data for storing flow state
    /// </summary>
    private class FlowNodeData
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Color { get; set; } = "";
        public string IconContent { get; set; } = "";
        public string LabelContent { get; set; } = "";
        public Dictionary<string, object?> AdditionalInfo { get; set; } = new();
        public bool IsGroup { get; set; } = false;
        public string? GroupStyle { get; set; }
    }
    
    /// <summary>
    /// Serializable connector data for storing flow state
    /// </summary>
    private class FlowConnectorData
    {
        public string Id { get; set; } = "";
        public string SourceId { get; set; } = "";
        public string SourcePortId { get; set; } = "";
        public string TargetId { get; set; } = "";
        public string TargetPortId { get; set; } = "";
    }

    private class PaletteCategory
    {
        public string Name { get; set; } = "";
        public bool IsExpanded { get; set; } = true;
        public List<PaletteNodeInfo> Nodes { get; set; } = new();
    }

    private class PaletteNodeInfo
    {
        public string Type { get; set; } = "";
        public string Label { get; set; } = "";
        public string Color { get; set; } = "#ddd";
        public string IconClass { get; set; } = "";
        public string IconBackground { get; set; } = "rgba(0,0,0,0.05)";
        public int Inputs { get; set; } = 1;
        public int Outputs { get; set; } = 1;
    }

    private class SearchResult
    {
        public string Type { get; set; } = "";
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string Description { get; set; } = "";
    }

    private class ConfigNodeInfo
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Label { get; set; } = "";
    }

    private class SubflowInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Inputs { get; set; }
        public int Outputs { get; set; }
        public string Description { get; set; } = "";
        public string Category { get; set; } = "subflows";
        public string Color { get; set; } = "#DDAA99";
        public List<string> NodeIds { get; set; } = new();
        public List<(string ConnectorId, string SourceId, string SourcePort, string TargetId, string TargetPort)> Connections { get; set; } = new();
    }

    private class GroupInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int NodeCount { get; set; }
        public List<string> NodeIds { get; set; } = new();
        public string Color { get; set; } = "#FFCCCC";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string DiagramNodeId { get; set; } = "";
    }

    private class PaletteModuleInfo
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public int NodeCount { get; set; }
        public bool IsInstalled { get; set; }
    }

    private class KeyboardShortcut
    {
        public string Key { get; set; } = "";
        public string Description { get; set; } = "";
    }

    // Context menu methods
    private void ShowNodeContextMenu(Node node, double x, double y)
    {
        ContextMenuNode = node;
        ContextMenuX = x;
        ContextMenuY = y;
        IsNodeContextMenuOpen = true;
        IsCanvasContextMenuOpen = false;
        StateHasChanged();
    }

    private void ShowCanvasContextMenu(double x, double y)
    {
        ContextMenuX = x;
        ContextMenuY = y;
        IsCanvasContextMenuOpen = true;
        IsNodeContextMenuOpen = false;
        StateHasChanged();
    }

    private void CloseContextMenus()
    {
        IsNodeContextMenuOpen = false;
        IsCanvasContextMenuOpen = false;
        ContextMenuNode = null;
        StateHasChanged();
    }

    private void ContextMenuEditNode()
    {
        if (ContextMenuNode != null)
        {
            SelectedDiagramNode = ContextMenuNode;
            IsPropertyTrayOpen = true;
            LoadNodeProperties(ContextMenuNode);
        }
        CloseContextMenus();
    }

    private void ContextMenuCopyNode()
    {
        if (ContextMenuNode != null)
        {
            SelectedDiagramNode = ContextMenuNode;
            CopySelection();
        }
        CloseContextMenus();
    }

    private void ContextMenuCutNode()
    {
        if (ContextMenuNode != null)
        {
            SelectedDiagramNode = ContextMenuNode;
            CutSelection();
        }
        CloseContextMenus();
    }

    private void ContextMenuDeleteNode()
    {
        if (ContextMenuNode != null)
        {
            // Record for undo
            RecordAction(new EditorAction
            {
                Type = EditorActionType.DeleteNode,
                NodeId = ContextMenuNode.ID,
                NodeData = ContextMenuNode
            });
            DiagramNodes!.Remove(ContextMenuNode);
            SelectedDiagramNode = null;
            HasUnsavedChanges = true;
        }
        CloseContextMenus();
    }

    private void ContextMenuPaste()
    {
        PasteFromClipboard();
        CloseContextMenus();
    }

    private void ContextMenuAddInject()
    {
        AddNodeAtPosition("inject", ContextMenuX, ContextMenuY);
        CloseContextMenus();
    }

    private void ContextMenuAddDebug()
    {
        AddNodeAtPosition("debug", ContextMenuX, ContextMenuY);
        CloseContextMenus();
    }

    private void ContextMenuAddFunction()
    {
        AddNodeAtPosition("function", ContextMenuX, ContextMenuY);
        CloseContextMenus();
    }

    private void ContextMenuAddChange()
    {
        AddNodeAtPosition("change", ContextMenuX, ContextMenuY);
        CloseContextMenus();
    }

    private void AddNodeAtPosition(string nodeType, double x, double y)
    {
        var paletteNode = GetPaletteNodeInfo(nodeType);
        
        if (paletteNode != null)
        {
            var nodeName = $"{paletteNode.Label}{++NodeCount}";
            var nodeId = $"{nodeType}{NodeCount}";
            var newNode = CreateNodeRedStyleNode(nodeId, x, y, paletteNode.Type, nodeName, paletteNode.Color, paletteNode);
            DiagramNodes?.Add(newNode);
            
            // Record undo action
            RecordAction(new EditorAction
            {
                Type = EditorActionType.AddNode,
                NodeId = newNode.ID,
                NodeData = newNode
            });
            
            HasUnsavedChanges = true;
            StateHasChanged();
        }
    }

    // Undo/Redo action types
    private enum EditorActionType
    {
        AddNode,
        DeleteNode,
        MoveNode,
        EditNode,
        AddConnector,
        DeleteConnector
    }

    // Represents an editor action for undo/redo
    private class EditorAction
    {
        public EditorActionType Type { get; set; }
        public string? NodeId { get; set; }
        public Node? NodeData { get; set; }
        public double OldX { get; set; }
        public double OldY { get; set; }
        public double NewX { get; set; }
        public double NewY { get; set; }
        public Dictionary<string, object?>? OldProperties { get; set; }
        public Dictionary<string, object?>? NewProperties { get; set; }
        public string? ConnectorId { get; set; }
        public Connector? ConnectorData { get; set; }
    }
}
