// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using NodeRed.Blazor.Components.Shared;
using NodeRed.Blazor.Models;
using NodeRed.Blazor.Services;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;
using Syncfusion.Blazor.Diagram;
using Syncfusion.Blazor.Layouts;
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
    
    // Main splitter reference (for tray pane control)
    private SfSplitter? _mainSplitter;

    // Selection settings
    public DiagramSelectionSettings SelectionSettings { get; set; } = new DiagramSelectionSettings()
    {
        Constraints = SelectorConstraints.All & ~SelectorConstraints.ResizeAll & ~SelectorConstraints.Rotate
    };

    // Diagram collections (VIEW - populated from central store based on current flow)
    private DiagramObjectCollection<Node>? DiagramNodes { get; set; } = new DiagramObjectCollection<Node>();
    private DiagramObjectCollection<Connector>? DiagramConnectors { get; set; } = new DiagramObjectCollection<Connector>();

    // CENTRAL NODE REGISTRY (like RED.nodes in JS Node-RED)
    // All nodes across all flows, keyed by node ID
    private Dictionary<string, NodeData> AllNodes = new();
    // All connectors across all flows, keyed by connector ID
    private Dictionary<string, ConnectorData> AllConnectors = new();
    // Groups across all flows, keyed by group ID
    private Dictionary<string, GroupInfo> AllGroups = new();

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
    private string _activeTrayTab = "properties"; // Tray tabs: properties, description, appearance
    private string _selectedNodeDescription = ""; // Node description for editing
    private string _selectedNodeIcon = ""; // Node icon for appearance tab
    private bool _selectedNodeShowLabel = true; // Whether to show label

    // Sidebar state
    private bool _isSidebarClosed = false;
    private string _activeSidebarTabId = "info";
    // Sidebar tabs matching original Node-RED: info, help, config, context, debug
    private List<RedUiSidebar.SidebarTab> _sidebarTabs = new()
    {
        new RedUiSidebar.SidebarTab { Id = "info", Name = "Info", Label = "info", IconClass = "fa fa-info", Pinned = true },
        new RedUiSidebar.SidebarTab { Id = "help", Name = "Help", Label = "help", IconClass = "fa fa-book", Pinned = true },
        new RedUiSidebar.SidebarTab { Id = "config", Name = "Configuration nodes", Label = "config", IconClass = "fa fa-cog", Pinned = true },
        new RedUiSidebar.SidebarTab { Id = "context", Name = "Context Data", Label = "context", IconClass = "fa fa-database", Pinned = true },
        new RedUiSidebar.SidebarTab { Id = "debug", Name = "Debug messages", Label = "debug", IconClass = "fa fa-bug", Pinned = true, EnableOnEdit = true }
    };

    // Tray tabs for node property editor
    private List<RedUiTrayInline.TrayTab> GetTrayTabs() => new()
    {
        new RedUiTrayInline.TrayTab { Id = "properties", Label = "Properties", IconClass = "fa fa-cog" },
        new RedUiTrayInline.TrayTab { Id = "description", Label = "Description", IconClass = "fa fa-file-text-o" },
        new RedUiTrayInline.TrayTab { Id = "appearance", Label = "Appearance", IconClass = "fa fa-object-group" }
    };

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

    // Help tab - selected node type from the tree view
    private string? _helpSelectedNodeType;

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
    private int _openSubmenu = 0; // 0=none, 1=node submenu, 2=group submenu, 3=insert submenu

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

    private int GetFilteredNodeCount()
    {
        return PaletteCategories
            .Where(c => FilterCategory(c))
            .SelectMany(c => c.Nodes.Where(n => FilterNode(n)))
            .Count();
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
        
        // Add to central registry (like RED.nodes.addNode in JS)
        AddNodeToRegistry(id, CurrentFlowId, nodeType, x, y, label, color, paletteNode?.IconClass ?? "");
    }

    private void InitDiagramModel()
    {
        // Create a simple sample flow
        // Users can add plugin nodes by dragging from the palette
        CreateNode("inject1", 150, 150, "inject", "timestamp");
        CreateNode("function1", 350, 150, "function", "process");
        CreateNode("debug1", 550, 150, "debug", "msg.payload");

        // Note: Connectors are not created here to avoid initialization issues
        // Users can draw connections by clicking on the output port (right side)
        // and dragging to an input port (left side)
    }

    private void CreateNode(string id, double x, double y, string nodeType, string label)
    {
        var paletteNode = GetPaletteNodeInfo(nodeType);
        var color = paletteNode?.Color ?? GetNodeColor(nodeType);
        var node = CreateNodeRedStyleNode(id, x, y, nodeType, label, color, paletteNode);
        DiagramNodes!.Add(node);
        NodeCount++;
        
        // Add to central registry (like RED.nodes.addNode in JS)
        AddNodeToRegistry(id, CurrentFlowId, nodeType, x, y, label, color, paletteNode?.IconClass ?? "");
    }
    
    /// <summary>
    /// Add a node to the central registry (like RED.nodes.addNode in JS Node-RED)
    /// </summary>
    private void AddNodeToRegistry(string id, string flowId, string nodeType, double x, double y, string name, string color, string iconClass, Dictionary<string, object?>? props = null)
    {
        var nodeData = new NodeData
        {
            Id = id,
            Z = flowId,
            Type = nodeType,
            X = x,
            Y = y,
            Name = name,
            Color = color,
            IconClass = iconClass,
            Props = props ?? new Dictionary<string, object?>(),
            Changed = true,
            Dirty = true
        };
        AllNodes[id] = nodeData;
    }
    
    /// <summary>
    /// Remove a node from the central registry
    /// </summary>
    private void RemoveNodeFromRegistry(string id)
    {
        AllNodes.Remove(id);
    }
    
    /// <summary>
    /// Add a connector to the central registry
    /// </summary>
    private void AddConnectorToRegistry(string id, string flowId, string sourceId, string sourcePortId, string targetId, string targetPortId)
    {
        var connectorData = new ConnectorData
        {
            Id = id,
            Z = flowId,
            SourceId = sourceId,
            SourcePortId = sourcePortId,
            TargetId = targetId,
            TargetPortId = targetPortId
        };
        AllConnectors[id] = connectorData;
    }
    
    /// <summary>
    /// Remove a connector from the central registry
    /// </summary>
    private void RemoveConnectorFromRegistry(string id)
    {
        AllConnectors.Remove(id);
    }
    
    /// <summary>
    /// Update a node's position in the central registry
    /// </summary>
    private void UpdateNodePosition(string id, double x, double y)
    {
        if (AllNodes.TryGetValue(id, out var nodeData))
        {
            nodeData.X = x;
            nodeData.Y = y;
            nodeData.Dirty = true;
        }
    }
    
    /// <summary>
    /// Update a node's properties in the central registry
    /// </summary>
    private void UpdateNodeProps(string id, string name, Dictionary<string, object?>? props)
    {
        if (AllNodes.TryGetValue(id, out var nodeData))
        {
            nodeData.Name = name;
            if (props != null)
            {
                foreach (var kvp in props)
                {
                    nodeData.Props[kvp.Key] = kvp.Value;
                }
            }
            nodeData.Changed = true;
            nodeData.Dirty = true;
        }
    }
    
    /// <summary>
    /// Get all nodes for the current flow from the central registry
    /// </summary>
    private IEnumerable<NodeData> GetNodesForFlow(string flowId)
    {
        return AllNodes.Values.Where(n => n.Z == flowId);
    }
    
    /// <summary>
    /// Get all connectors for the current flow from the central registry
    /// </summary>
    private IEnumerable<ConnectorData> GetConnectorsForFlow(string flowId)
    {
        return AllConnectors.Values.Where(c => c.Z == flowId);
    }
    
    /// <summary>
    /// Populate the diagram view from the central registry for the given flow
    /// </summary>
    private void PopulateDiagramFromRegistry(string flowId)
    {
        DiagramNodes?.Clear();
        DiagramConnectors?.Clear();
        
        // Add nodes for this flow
        foreach (var nodeData in GetNodesForFlow(flowId))
        {
            var paletteNode = GetPaletteNodeInfo(nodeData.Type);
            
            // Handle special subflow I/O nodes
            if (nodeData.Type == "subflow-in" || nodeData.Type == "subflow-out")
            {
                var isInput = nodeData.Type == "subflow-in";
                var ioNode = new Node
                {
                    ID = nodeData.Id,
                    OffsetX = nodeData.X,
                    OffsetY = nodeData.Y,
                    Width = nodeData.W,
                    Height = nodeData.H,
                    Shape = new BasicShape { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 3 },
                    Style = new ShapeStyle { Fill = "#A6BBCF", StrokeColor = "#7B9BAC", StrokeWidth = 1 },
                    AdditionalInfo = new Dictionary<string, object?>(nodeData.Props) { ["nodeType"] = nodeData.Type },
                    Constraints = DefaultNodeConstraints
                };
                ioNode.Annotations = new DiagramObjectCollection<ShapeAnnotation>
                {
                    new ShapeAnnotation { ID = "iconAnnotation", Content = isInput ? "→" : "←", Style = new TextStyle { Color = "#333", FontSize = 12 } },
                    new ShapeAnnotation { ID = "labelAnnotation", Content = nodeData.Name, Style = new TextStyle { Color = "#333", FontSize = 12 }, Offset = new DiagramPoint { X = 0.5, Y = 0.5 } }
                };
                // Use standard port IDs (port1 for input, port2 for output) for consistent connection handling
                ioNode.Ports = new DiagramObjectCollection<PointPort>
                {
                    new PointPort
                    {
                        ID = isInput ? "port2" : "port1",  // subflow-in has output port (port2), subflow-out has input port (port1)
                        Offset = new DiagramPoint { X = isInput ? 1 : 0, Y = 0.5 },
                        Visibility = PortVisibility.Visible,
                        Height = 10,
                        Width = 10,
                        Shape = PortShapes.Square,
                        Style = new ShapeStyle { Fill = "#d9d9d9", StrokeColor = "#999" },
                        Constraints = isInput ? (PortConstraints.Default | PortConstraints.Draw) : PortConstraints.Default
                    }
                };
                DiagramNodes?.Add(ioNode);
            }
            else
            {
                // Regular node
                var node = CreateNodeRedStyleNode(
                    nodeData.Id,
                    nodeData.X,
                    nodeData.Y,
                    nodeData.Type,
                    nodeData.Name,
                    !string.IsNullOrEmpty(nodeData.Color) ? nodeData.Color : paletteNode?.Color ?? "#ddd",
                    paletteNode
                );
                
                // Restore additional info/props
                if (nodeData.Props.Count > 0)
                {
                    node.AdditionalInfo = new Dictionary<string, object?>(nodeData.Props);
                    node.AdditionalInfo["nodeType"] = nodeData.Type;
                }
                
                DiagramNodes?.Add(node);
            }
        }
        
        // Add connectors for this flow
        foreach (var connData in GetConnectorsForFlow(flowId))
        {
            var connector = new Connector
            {
                ID = connData.Id,
                SourceID = connData.SourceId,
                SourcePortID = connData.SourcePortId,
                TargetID = connData.TargetId,
                TargetPortID = connData.TargetPortId,
                Type = ConnectorSegmentType.Orthogonal,
                Style = new ShapeStyle { StrokeColor = "#999", StrokeWidth = 2 },
                TargetDecorator = new DecoratorSettings { Shape = DecoratorShape.None },
                SourcePoint = new DiagramPoint() { X = FallbackSourcePointX, Y = FallbackSourcePointY },
                TargetPoint = new DiagramPoint() { X = FallbackTargetPointX, Y = FallbackTargetPointY }
            };
            DiagramConnectors?.Add(connector);
        }
        
        // Restore groups
        Groups.Clear();
        foreach (var group in AllGroups.Values.Where(g => g.FlowId == flowId))
        {
            Groups.Add(group);
            // Add group visual node
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
                Ports = new DiagramObjectCollection<PointPort>(),
                Constraints = GroupNodeConstraints,
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
            DiagramNodes?.Add(groupNode);
        }
    }
    
    /// <summary>
    /// Sync diagram state back to the central registry (call after any diagram modification)
    /// </summary>
    private void SyncDiagramToRegistry()
    {
        if (DiagramNodes == null || DiagramConnectors == null) return;
        
        // Sync node positions and properties
        foreach (var node in DiagramNodes)
        {
            if (AllNodes.TryGetValue(node.ID, out var nodeData))
            {
                nodeData.X = node.OffsetX;
                nodeData.Y = node.OffsetY;
                nodeData.W = node.Width ?? 122;
                nodeData.H = node.Height ?? 25;
                
                // Get name from label annotation
                var labelAnnotation = node.Annotations?.FirstOrDefault(a => a.ID == "labelAnnotation");
                if (labelAnnotation != null)
                {
                    nodeData.Name = labelAnnotation.Content ?? nodeData.Name;
                }
                
                // Sync additional props
                if (node.AdditionalInfo != null)
                {
                    foreach (var kvp in node.AdditionalInfo)
                    {
                        if (kvp.Key != "nodeType")
                        {
                            nodeData.Props[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
        }
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

        // Add trigger button for inject nodes and subflow instances with inputs
        // Node-RED JS shows a small square button with play icon on the left
        bool shouldHaveButton = nodeType == "inject";
        
        // Subflow instances (type starts with "subflow:") also have buttons if they have inputs
        if (nodeType.StartsWith("subflow:"))
        {
            var subflowId = nodeType.Substring(8); // Remove "subflow:" prefix
            var subflow = Subflows.FirstOrDefault(s => s.Id == subflowId);
            if (subflow != null && subflow.Inputs > 0)
            {
                shouldHaveButton = true;
            }
        }
        
        if (shouldHaveButton)
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

    // Flag to prevent recursive selection when auto-selecting group nodes
    private bool _isSelectingGroupNodes = false;

    private void OnSelectionChanged(Syncfusion.Blazor.Diagram.SelectionChangedEventArgs args)
    {
        if (args.NewValue?.Count > 0 && args.NewValue[0] is Node node)
        {
            SelectedDiagramNode = node;
            // Get label from labelAnnotation (index 1), not iconAnnotation (index 0)
            SelectedNodeName = node.Annotations?.FirstOrDefault(a => a.ID == "labelAnnotation")?.Content ?? "";
            LoadNodeProperties(node);
            SelectedSidebarTab = 0;
            
            // When a group is selected, also select all its contained nodes
            // This ensures they move together with the group (matching Node-RED JS behavior)
            if (!_isSelectingGroupNodes && DiagramInstance != null)
            {
                // Use AllGroups dictionary for O(1) lookup by node ID (Id == DiagramNodeId)
                if (AllGroups.TryGetValue(node.ID, out var group) && group.NodeIds.Count > 0 && DiagramNodes != null)
                {
                    _isSelectingGroupNodes = true;
                    try
                    {
                        // Build a collection of all nodes to select (group + contained nodes)
                        var nodesToSelect = new ObservableCollection<IDiagramObject> { node };
                        foreach (var nodeId in group.NodeIds)
                        {
                            var containedNode = DiagramNodes.FirstOrDefault(n => n.ID == nodeId);
                            if (containedNode != null)
                            {
                                nodesToSelect.Add(containedNode);
                            }
                        }
                        
                        // Select all nodes together
                        DiagramInstance.Select(nodesToSelect);
                    }
                    finally
                    {
                        _isSelectingGroupNodes = false;
                    }
                }
            }
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
    /// Handles main splitter resize events.
    /// This is called when the user finishes resizing the palette or sidebar panes.
    /// </summary>
    private void OnMainSplitterResize(ResizeEventArgs args)
    {
        // The splitter handles the resize automatically
        // This event can be used for logging or persistence if needed
    }

    /// <summary>
    /// Handles position changes - updates internal registry when nodes are moved.
    /// Since contained nodes are auto-selected when a group is selected, they move together
    /// during the drag operation (matching Node-RED JS behavior).
    /// </summary>
    private void OnPositionChanged(PositionChangedEventArgs args)
    {
        try
        {
            if (args.NewValue?.Nodes?.Count > 0)
            {
                foreach (var movedNode in args.NewValue.Nodes)
                {
                    // Update position in central registry
                    UpdateNodePosition(movedNode.ID, movedNode.OffsetX, movedNode.OffsetY);
                    
                    // If this is a group node, update group bounds (O(1) lookup)
                    if (movedNode.ID != null && AllGroups.TryGetValue(movedNode.ID, out var group))
                    {
                        var oldNode = args.OldValue?.Nodes?.FirstOrDefault(n => n.ID == movedNode.ID);
                        if (oldNode != null)
                        {
                            double deltaX = movedNode.OffsetX - oldNode.OffsetX;
                            double deltaY = movedNode.OffsetY - oldNode.OffsetY;
                            
                            // Update group position info
                            group.X += deltaX;
                            group.Y += deltaY;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash - this is an event handler
            Console.WriteLine($"Error in OnPositionChanged: {ex.Message}");
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

    private void DeleteSelectionAndCloseTray()
    {
        DeleteSelection();
        IsPropertyTrayOpen = false;
    }

    /// <summary>
    /// Returns true if the tray should be visible.
    /// </summary>
    private bool IsTrayVisible() => IsPropertyTrayOpen && SelectedDiagramNode != null;

    /// <summary>
    /// Gets the size for the tray pane based on visibility state.
    /// </summary>
    private string GetTraySize() => IsTrayVisible() ? "450px" : "0px";

    /// <summary>
    /// Gets the min size for the tray pane based on visibility state.
    /// </summary>
    private string GetTrayMinSize() => IsTrayVisible() ? "350px" : "0px";

    /// <summary>
    /// Gets the max size for the tray pane based on visibility state.
    /// </summary>
    private string GetTrayMaxSize() => IsTrayVisible() ? "700px" : "0px";

    /// <summary>
    /// Gets the CSS class for the tray pane based on visibility state.
    /// </summary>
    private string GetTrayCssClass()
    {
        return IsTrayVisible() 
            ? "red-ui-tray-pane" 
            : "red-ui-tray-pane red-ui-tray-hidden";
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
        
        // Sync current diagram state to the central registry
        SyncDiagramToRegistry();
        
        // Switch to the new flow
        CurrentFlowId = flowId;
        
        // Populate the diagram from the central registry for the new flow
        PopulateDiagramFromRegistry(flowId);
        
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

    /// <summary>
    /// Gets the node type to show help for - uses manually selected type from Help tree, 
    /// or falls back to the currently selected diagram node's type.
    /// </summary>
    private string GetNodeTypeForHelp()
    {
        if (!string.IsNullOrEmpty(_helpSelectedNodeType))
        {
            return _helpSelectedNodeType;
        }
        return GetSelectedNodeType();
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
        
        try
        {
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
            
            NotificationService.Success("Successfully deployed");
            StateHasChanged();
        }
        catch (Exception ex)
        {
            NotificationService.Error($"Deploy failed: {ex.Message}");
        }
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
                
                NotificationService.Success($"Imported {importedWorkspace.Flows.Count} flow(s)");
                HasUnsavedChanges = true;
            }
            catch (Exception ex)
            {
                NotificationService.Error($"Import failed: {ex.Message}");
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
        var success = await ClipboardService.CopyToClipboardAsync(ExportJson);
        if (success)
        {
            NotificationService.Success("Copied to clipboard");
        }
        else
        {
            NotificationService.Error("Failed to copy to clipboard");
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
            
            NotificationService.Success($"Downloaded {fileName}");
        }
        catch
        {
            NotificationService.Error("Download failed");
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
        // Subflows are now stored persistently and don't need to be refreshed/cleared
        // This method is kept for backward compatibility and potential future use
        // (e.g., loading subflows from storage)
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
        
        // Add the subflow as a node type to the palette
        // (Like original Node-RED - subflow is added to palette, but NO new tab is created)
        AddSubflowToPalette(newSubflow);
        
        // Add subflow I/O nodes to the central registry (for when the subflow is opened later)
        AddSubflowIONodesToRegistry(newSubflow);
        
        DebugMessages.Add(new DebugMessage
        {
            NodeId = "system",
            NodeName = "System",
            Data = $"Created subflow '{subflowName}'. The subflow is now available in the palette under 'subflows'. Drag it to any flow to use it, or click 'Edit Template' in the properties pane to edit its contents.",
            Timestamp = DateTimeOffset.Now
        });
        
        HasUnsavedChanges = true;
        IsSubflowsDialogOpen = false;
        StateHasChanged();
    }
    
    /// <summary>
    /// Add subflow I/O nodes to the central registry
    /// </summary>
    private void AddSubflowIONodesToRegistry(SubflowInfo subflow)
    {
        // Add input node
        var inputId = $"{subflow.Id}_in";
        AllNodes[inputId] = new NodeData
        {
            Id = inputId,
            Z = subflow.Id,
            Type = "subflow-in",
            X = 150,
            Y = 200,
            W = 80,
            H = 25,
            Name = "Input",
            Color = "#A6BBCF",
            IconClass = "→",
            Props = new Dictionary<string, object?> { { "subflowId", subflow.Id } }
        };
        
        // Add output node
        var outputId = $"{subflow.Id}_out";
        AllNodes[outputId] = new NodeData
        {
            Id = outputId,
            Z = subflow.Id,
            Type = "subflow-out",
            X = 500,
            Y = 200,
            W = 80,
            H = 25,
            Name = "Output",
            Color = "#A6BBCF",
            IconClass = "←",
            Props = new Dictionary<string, object?> { { "subflowId", subflow.Id } }
        };
    }
    
    private void AddSubflowIONodes(SubflowInfo subflow)
    {
        // Add subflow input node (has output port on the right)
        var inputNode = new Node
        {
            ID = $"{subflow.Id}_in",
            OffsetX = 150,
            OffsetY = 200,
            Width = 80,
            Height = 25,
            Shape = new BasicShape { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 3 },
            Style = new ShapeStyle { Fill = "#A6BBCF", StrokeColor = "#7B9BAC", StrokeWidth = 1 },
            Constraints = DefaultNodeConstraints,
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
        // Use standard port ID (port2 for output) for consistent connection handling
        inputNode.Ports = new DiagramObjectCollection<PointPort>
        {
            new PointPort
            {
                ID = "port2",
                Shape = PortShapes.Square,
                Offset = new DiagramPoint { X = 1, Y = 0.5 },
                Visibility = PortVisibility.Visible,
                Height = 10,
                Width = 10,
                Style = new ShapeStyle { Fill = "#d9d9d9", StrokeColor = "#999" },
                Constraints = PortConstraints.Default | PortConstraints.Draw
            }
        };
        DiagramNodes?.Add(inputNode);
        
        // Add subflow output node (has input port on the left)
        var outputNode = new Node
        {
            ID = $"{subflow.Id}_out",
            OffsetX = 500,
            OffsetY = 200,
            Width = 80,
            Height = 25,
            Shape = new BasicShape { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 3 },
            Style = new ShapeStyle { Fill = "#A6BBCF", StrokeColor = "#7B9BAC", StrokeWidth = 1 },
            Constraints = DefaultNodeConstraints,
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
        // Use standard port ID (port1 for input) for consistent connection handling
        outputNode.Ports = new DiagramObjectCollection<PointPort>
        {
            new PointPort
            {
                ID = "port1",
                Shape = PortShapes.Square,
                Offset = new DiagramPoint { X = 0, Y = 0.5 },
                Visibility = PortVisibility.Visible,
                Height = 10,
                Width = 10,
                Style = new ShapeStyle { Fill = "#d9d9d9", StrokeColor = "#999" },
                Constraints = PortConstraints.Default
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
        
        // Add the subflow to the palette (no tab is created yet - like original Node-RED)
        AddSubflowToPalette(newSubflow);
        
        // Add subflow I/O nodes to the central registry
        AddSubflowIONodesToRegistry(newSubflow);
        
        // Move selected nodes to the subflow registry (update their Z property)
        foreach (var node in selectedNodes)
        {
            var nodeType = node.AdditionalInfo?.TryGetValue("nodeType", out var typeObj) == true
                ? typeObj as string ?? "" : "";
            var nodeColor = node.Style?.Fill ?? "#ddd";
            var iconClass = node.AdditionalInfo?.TryGetValue("iconClass", out var iconObj) == true
                ? iconObj as string ?? "fa fa-cube" : "fa fa-cube";
            var labelContent = node.Annotations?.FirstOrDefault(a => a.ID == "labelAnnotation")?.Content ?? "";
            
            // Add to the subflow's registry (with Z = subflowId)
            var nodeData = new NodeData
            {
                Id = node.ID,
                Z = subflowId,
                Type = nodeType,
                X = node.OffsetX,
                Y = node.OffsetY,
                W = node.Width ?? 122,
                H = node.Height ?? 25,
                Name = labelContent,
                Color = nodeColor,
                IconClass = iconClass,
                Props = node.AdditionalInfo != null ? new Dictionary<string, object?>(node.AdditionalInfo) : new(),
                Changed = true,
                Dirty = true
            };
            AllNodes[node.ID] = nodeData;
            
            // Remove from current flow's diagram
            DiagramNodes?.Remove(node);
        }
        
        // Store connectors between the moved nodes in the registry
        var connectorsToMove = DiagramConnectors?
            .Where(c => nodeIds.Contains(c.SourceID) && nodeIds.Contains(c.TargetID))
            .ToList() ?? new List<Connector>();
        foreach (var connector in connectorsToMove)
        {
            AddConnectorToRegistry(connector.ID, subflowId, connector.SourceID, connector.SourcePortID ?? "", connector.TargetID, connector.TargetPortID ?? "");
            DiagramConnectors?.Remove(connector);
        }
        
        // Remove connectors that connected to/from the moved nodes (to external nodes)
        var connectorsToRemove = DiagramConnectors?
            .Where(c => nodeIds.Contains(c.SourceID) || nodeIds.Contains(c.TargetID))
            .ToList() ?? new List<Connector>();
        foreach (var connector in connectorsToRemove)
        {
            DiagramConnectors?.Remove(connector);
        }
        
        // Create a subflow instance node in place of the removed nodes
        var avgX = selectedNodes.Count > 0 ? selectedNodes.Average(n => n.OffsetX) : 300.0;
        var avgY = selectedNodes.Count > 0 ? selectedNodes.Average(n => n.OffsetY) : 200.0;
        
        var subflowInstanceId = $"subflow_instance_{Guid.NewGuid():N}";
        var subflowInstanceNode = CreateNodeRedStyleNode(subflowInstanceId, avgX, avgY, "subflow:" + subflowId, subflowName, "#DDAA99", null);
        DiagramNodes?.Add(subflowInstanceNode);
        
        // Add the instance to the registry
        AddNodeToRegistry(subflowInstanceId, CurrentFlowId, "subflow:" + subflowId, avgX, avgY, subflowName, "#DDAA99", "fa fa-th-large");
        
        DebugMessages.Add(new DebugMessage
        {
            NodeId = "system",
            NodeName = "System",
            Data = $"Created subflow '{subflowName}' from {selectedNodes.Count} selected node(s). A subflow instance has been placed in the current flow. Click 'Edit Template' in the properties pane to edit the subflow contents.",
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
            // Create the flow tab for editing if it doesn't exist yet
            var existingTab = Flows.FirstOrDefault(f => f.Id == subflowId);
            if (existingTab == null)
            {
                var newFlow = new FlowTab
                {
                    Id = subflowId,
                    Label = subflow.Name,
                    Disabled = false,
                    Info = "Subflow editor - add nodes here to define the subflow logic"
                };
                Flows.Add(newFlow);
            }
            
            // Sync current flow state before switching
            SyncDiagramToRegistry();
            
            // Use proper tab switching which saves/restores state
            CurrentFlowId = subflowId;
            PopulateDiagramFromRegistry(subflowId);
            
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
    
    /// <summary>
    /// Opens the subflow template editor for the currently selected subflow instance.
    /// Called from the properties pane when editing a subflow instance node.
    /// </summary>
    private void EditSubflowTemplate()
    {
        if (SelectedDiagramNode == null) return;
        
        var nodeType = GetSelectedNodeType();
        if (!nodeType.StartsWith("subflow:")) return;
        
        var subflowId = nodeType.Substring(8); // Remove "subflow:" prefix
        
        // Close the property tray and edit the subflow
        IsPropertyTrayOpen = false;
        EditSubflow(subflowId);
    }
    
    /// <summary>
    /// Checks if the currently selected node is a subflow instance.
    /// </summary>
    private bool IsSelectedNodeSubflowInstance()
    {
        if (SelectedDiagramNode == null) return false;
        var nodeType = GetSelectedNodeType();
        return nodeType.StartsWith("subflow:");
    }
    
    /// <summary>
    /// Gets the subflow info for the currently selected subflow instance.
    /// </summary>
    private SubflowInfo? GetSelectedSubflowInfo()
    {
        if (SelectedDiagramNode == null) return null;
        var nodeType = GetSelectedNodeType();
        if (!nodeType.StartsWith("subflow:")) return null;
        
        var subflowId = nodeType.Substring(8);
        return Subflows.FirstOrDefault(sf => sf.Id == subflowId);
    }
    
    /// <summary>
    /// Checks if the current flow tab is a subflow template.
    /// </summary>
    private bool IsCurrentFlowSubflow()
    {
        return Subflows.Any(sf => sf.Id == CurrentFlowId);
    }
    
    /// <summary>
    /// Gets the subflow info for the current flow (if it's a subflow).
    /// </summary>
    private SubflowInfo? GetCurrentSubflowInfo()
    {
        return Subflows.FirstOrDefault(sf => sf.Id == CurrentFlowId);
    }
    
    /// <summary>
    /// Opens a dialog to edit the properties of the current subflow.
    /// </summary>
    private void EditSubflowProperties()
    {
        var subflow = GetCurrentSubflowInfo();
        if (subflow == null) return;
        
        // For now, show a message - in a full implementation, this would open a properties dialog
        DebugMessages.Add(new DebugMessage
        {
            NodeId = "system",
            NodeName = "System",
            Data = $"Edit properties for subflow '{subflow.Name}'. Use the Subflows menu to rename or modify settings.",
            Timestamp = DateTimeOffset.Now
        });
        StateHasChanged();
    }
    
    /// <summary>
    /// Sets the number of inputs for the current subflow (0 or 1).
    /// </summary>
    private void SetSubflowInputs(int inputs)
    {
        var subflow = GetCurrentSubflowInfo();
        if (subflow == null) return;
        
        if (inputs < 0 || inputs > 1) return;
        
        var oldInputs = subflow.Inputs;
        subflow.Inputs = inputs;
        
        // Update the I/O nodes in the diagram
        UpdateSubflowIONodes(subflow);
        
        // Update the palette entry
        UpdateSubflowInPalette(subflow);
        
        DebugMessages.Add(new DebugMessage
        {
            NodeId = "system",
            NodeName = "System",
            Data = $"Subflow '{subflow.Name}' inputs changed from {oldInputs} to {inputs}.",
            Timestamp = DateTimeOffset.Now
        });
        
        HasUnsavedChanges = true;
        StateHasChanged();
    }
    
    /// <summary>
    /// Increments the number of outputs for the current subflow.
    /// </summary>
    private void IncrementSubflowOutputs()
    {
        var subflow = GetCurrentSubflowInfo();
        if (subflow == null) return;
        
        subflow.Outputs++;
        
        // Update the palette entry
        UpdateSubflowInPalette(subflow);
        
        DebugMessages.Add(new DebugMessage
        {
            NodeId = "system",
            NodeName = "System",
            Data = $"Subflow '{subflow.Name}' now has {subflow.Outputs} output(s).",
            Timestamp = DateTimeOffset.Now
        });
        
        HasUnsavedChanges = true;
        StateHasChanged();
    }
    
    /// <summary>
    /// Decrements the number of outputs for the current subflow (minimum 0).
    /// </summary>
    private void DecrementSubflowOutputs()
    {
        var subflow = GetCurrentSubflowInfo();
        if (subflow == null) return;
        
        if (subflow.Outputs <= 0) return;
        
        subflow.Outputs--;
        
        // Update the palette entry
        UpdateSubflowInPalette(subflow);
        
        DebugMessages.Add(new DebugMessage
        {
            NodeId = "system",
            NodeName = "System",
            Data = $"Subflow '{subflow.Name}' now has {subflow.Outputs} output(s).",
            Timestamp = DateTimeOffset.Now
        });
        
        HasUnsavedChanges = true;
        StateHasChanged();
    }
    
    /// <summary>
    /// Toggles the status output for the current subflow.
    /// When enabled, the subflow can output status information.
    /// </summary>
    private void ToggleSubflowStatus()
    {
        var subflow = GetCurrentSubflowInfo();
        if (subflow == null) return;
        
        subflow.Status = !subflow.Status;
        
        // Add or remove status node from the subflow
        var statusNodeId = $"{subflow.Id}_status";
        
        if (subflow.Status)
        {
            // Add status node to the diagram and registry
            var statusNode = new Node
            {
                ID = statusNodeId,
                OffsetX = 500,
                OffsetY = 280,
                Width = 80,
                Height = 25,
                Shape = new BasicShape { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 3 },
                Style = new ShapeStyle { Fill = "#C0C0C0", StrokeColor = "#909090", StrokeWidth = 1 },
                Constraints = DefaultNodeConstraints,
                AdditionalInfo = new Dictionary<string, object?>
                {
                    { "nodeType", "subflow-status" },
                    { "subflowId", subflow.Id }
                }
            };
            statusNode.Annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation { ID = "iconAnnotation", Content = "◉", Style = new TextStyle { Color = "#333", FontSize = 12 } },
                new ShapeAnnotation { ID = "labelAnnotation", Content = "Status", Style = new TextStyle { Color = "#333", FontSize = 12 }, Offset = new DiagramPoint { X = 0.5, Y = 0.5 } }
            };
            statusNode.Ports = new DiagramObjectCollection<PointPort>
            {
                new PointPort
                {
                    ID = "port1",
                    Shape = PortShapes.Square,
                    Offset = new DiagramPoint { X = 0, Y = 0.5 },
                    Visibility = PortVisibility.Visible,
                    Height = 10,
                    Width = 10,
                    Style = new ShapeStyle { Fill = "#d9d9d9", StrokeColor = "#999" },
                    Constraints = PortConstraints.Default
                }
            };
            DiagramNodes?.Add(statusNode);
            
            // Add to registry
            AllNodes[statusNodeId] = new NodeData
            {
                Id = statusNodeId,
                Z = subflow.Id,
                Type = "subflow-status",
                X = 500,
                Y = 280,
                W = 80,
                H = 25,
                Name = "Status",
                Color = "#C0C0C0",
                IconClass = "◉",
                Props = new Dictionary<string, object?> { { "subflowId", subflow.Id } }
            };
            
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Status output enabled for subflow '{subflow.Name}'. Connect nodes to the Status input to report status.",
                Timestamp = DateTimeOffset.Now
            });
        }
        else
        {
            // Remove status node from diagram and registry
            var existingNode = DiagramNodes?.FirstOrDefault(n => n.ID == statusNodeId);
            if (existingNode != null)
            {
                DiagramNodes?.Remove(existingNode);
            }
            AllNodes.Remove(statusNodeId);
            
            // Remove any connectors connected to the status node
            var connectorsToRemove = DiagramConnectors?.Where(c => c.SourceID == statusNodeId || c.TargetID == statusNodeId).ToList();
            if (connectorsToRemove != null)
            {
                foreach (var conn in connectorsToRemove)
                {
                    DiagramConnectors?.Remove(conn);
                    AllConnectors.Remove(conn.ID);
                }
            }
            
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Status output disabled for subflow '{subflow.Name}'.",
                Timestamp = DateTimeOffset.Now
            });
        }
        
        HasUnsavedChanges = true;
        StateHasChanged();
    }
    
    /// <summary>
    /// Deletes the current subflow (if editing a subflow template).
    /// </summary>
    private void DeleteCurrentSubflow()
    {
        var subflow = GetCurrentSubflowInfo();
        if (subflow == null) return;
        
        DeleteSubflow(subflow.Id);
    }
    
    /// <summary>
    /// Updates the I/O nodes in the subflow diagram based on inputs/outputs settings.
    /// </summary>
    private void UpdateSubflowIONodes(SubflowInfo subflow)
    {
        // Find and update/remove/add input node
        var inputNodeId = $"{subflow.Id}_in";
        var existingInputNode = DiagramNodes?.FirstOrDefault(n => n.ID == inputNodeId);
        
        if (subflow.Inputs == 0 && existingInputNode != null)
        {
            // Remove input node
            DiagramNodes?.Remove(existingInputNode);
            AllNodes.Remove(inputNodeId);
        }
        else if (subflow.Inputs == 1 && existingInputNode == null)
        {
            // Add input node
            var inputNode = new Node
            {
                ID = inputNodeId,
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
            
            // Add to registry
            AllNodes[inputNodeId] = new NodeData
            {
                Id = inputNodeId,
                Z = subflow.Id,
                Type = "subflow-in",
                X = 150,
                Y = 200,
                W = 80,
                H = 25,
                Name = "Input",
                Color = "#A6BBCF",
                IconClass = "→",
                Props = new Dictionary<string, object?> { { "subflowId", subflow.Id } }
            };
        }
    }
    
    /// <summary>
    /// Updates a subflow entry in the palette.
    /// </summary>
    private void UpdateSubflowInPalette(SubflowInfo subflow)
    {
        var subflowCategory = PaletteCategories.FirstOrDefault(c => c.Name == "subflows");
        if (subflowCategory == null) return;
        
        var paletteNode = subflowCategory.Nodes.FirstOrDefault(n => n.Type == $"subflow:{subflow.Id}");
        if (paletteNode != null)
        {
            paletteNode.Inputs = subflow.Inputs;
            paletteNode.Outputs = subflow.Outputs;
            paletteNode.Label = subflow.Name;
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
            // Remove the flow tab for this subflow (if it exists)
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
                    SwitchFlow(firstRegularFlow.Id);
                }
                else if (Flows.Count > 0)
                {
                    SwitchFlow(Flows[0].Id);
                }
                else
                {
                    // Create a new flow if none exist
                    AddNewFlow();
                }
            }
            
            // Remove from the palette
            RemoveSubflowFromPalette(subflowId);
            
            // Remove I/O nodes from the registry
            AllNodes.Remove($"{subflowId}_in");
            AllNodes.Remove($"{subflowId}_out");
            
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
    
    /// <summary>
    /// Remove a subflow from the palette
    /// </summary>
    private void RemoveSubflowFromPalette(string subflowId)
    {
        var subflowCategory = PaletteCategories.FirstOrDefault(c => c.Name == "subflows");
        if (subflowCategory != null)
        {
            var nodeToRemove = subflowCategory.Nodes.FirstOrDefault(n => n.Type == $"subflow:{subflowId}");
            if (nodeToRemove != null)
            {
                subflowCategory.Nodes.Remove(nodeToRemove);
            }
            
            // Remove the category if it's empty
            if (subflowCategory.Nodes.Count == 0)
            {
                PaletteCategories.Remove(subflowCategory);
            }
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
                Constraints = GroupNodeConstraints, // No connections - groups don't have ports
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["nodeType"] = "group",
                    ["color"] = DefaultGroupFillColor,
                    ["isGroup"] = true
                }
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
                FlowId = CurrentFlowId,
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
            AllGroups[groupId] = newGroup; // Add to central registry
            
            NotificationService.Success($"Created group '{groupName}' containing {selectedNodes.Count} node(s)");
            
            HasUnsavedChanges = true;
            StateHasChanged();
        }
        else
        {
            NotificationService.Warning("Please select nodes to group. Hold Ctrl while clicking to select multiple nodes.");
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
            AllGroups.Remove(groupToRemove.Id); // Remove from central registry
            
            NotificationService.Success($"Ungrouped '{groupToRemove.Name}'. {groupToRemove.NodeCount} node(s) are now independent.");
            
            HasUnsavedChanges = true;
            StateHasChanged();
        }
        else
        {
            NotificationService.Warning("No groups to ungroup. Select a group first.");
        }
    }

    // Group properties dialog state
    private bool IsGroupPropertiesDialogOpen = false;
    private string GroupPropertiesId = "";
    private string GroupPropertiesName = "";
    private string GroupPropertiesFillColor = "rgba(255, 204, 204, 0.3)";
    private string GroupPropertiesStrokeColor = "#FF9999";

    private void EditGroup(string groupId)
    {
        var group = Groups.FirstOrDefault(g => g.Id == groupId);
        if (group != null)
        {
            GroupPropertiesId = groupId;
            GroupPropertiesName = group.Name;
            GroupPropertiesFillColor = group.FillColor ?? DefaultGroupFillColor;
            GroupPropertiesStrokeColor = group.StrokeColor ?? DefaultGroupStrokeColor;
            IsGroupPropertiesDialogOpen = true;
        }
    }

    private void CloseGroupPropertiesDialog()
    {
        IsGroupPropertiesDialogOpen = false;
    }

    private void SaveGroupProperties()
    {
        var group = Groups.FirstOrDefault(g => g.Id == GroupPropertiesId);
        if (group != null)
        {
            group.Name = GroupPropertiesName;
            group.FillColor = GroupPropertiesFillColor;
            group.StrokeColor = GroupPropertiesStrokeColor;
            
            // Update the visual group node
            var groupNode = DiagramNodes?.FirstOrDefault(n => n.ID == group.DiagramNodeId);
            if (groupNode?.Style != null)
            {
                groupNode.Style.Fill = GroupPropertiesFillColor;
                groupNode.Style.StrokeColor = GroupPropertiesStrokeColor;
            }
            
            // Update label
            var labelAnnotation = groupNode?.Annotations?.FirstOrDefault();
            if (labelAnnotation != null)
            {
                labelAnnotation.Content = GroupPropertiesName;
            }
            
            HasUnsavedChanges = true;
        }
        IsGroupPropertiesDialogOpen = false;
        StateHasChanged();
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
            AllGroups.Remove(groupId); // Remove from central registry
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
                Version = g.Key == "core" ? GetCoreVersion() : "-", // Backend would provide actual version
                NodeCount = g.Count(),
                IsInstalled = true
            })
            .ToList();
        
        AvailableModules.AddRange(modules);
    }

    /// <summary>
    /// Gets the core Node-RED version from assembly metadata
    /// </summary>
    private string GetCoreVersion()
    {
        var assembly = typeof(Editor).Assembly;
        var version = assembly.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "-";
    }

    private void InstallPaletteModule(string moduleName)
    {
        // Backend would handle actual package installation
        // UI simulates the result for now
        
        // For now, simulate installation by adding to available modules
        var newModule = new PaletteModuleInfo
        {
            Name = moduleName,
            Version = "-", // Version would come from backend after installation
            NodeCount = 1,
            IsInstalled = true
        };
        
        if (!AvailableModules.Any(m => m.Name == moduleName))
        {
            AvailableModules.Add(newModule);
            NotificationService.Success($"Module '{moduleName}' installed successfully");
        }
        else
        {
            NotificationService.Warning($"Module '{moduleName}' is already installed");
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
            NotificationService.Success($"Module '{moduleName}' uninstalled successfully");
            StateHasChanged();
        }
        else
        {
            NotificationService.Warning($"Module '{moduleName}' not found");
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
            NotificationService.Success("Settings updated successfully");
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
        // Handle special node types that aren't in SDK
        if (nodeType == "group")
        {
            return new NodeHelpText
            {
                Summary = "A group is a visual container that organizes related nodes together. When you move a group, all nodes inside it move together.",
                Details = "Groups help organize complex flows by visually grouping related nodes. You can:\n\n- Select multiple nodes and click 'Group Selected' to create a group\n- Drag the group to move all contained nodes together\n- Ungroup to release the nodes back to independent movement"
            };
        }
        
        if (nodeType == "subflow-in" || nodeType == "subflow-out")
        {
            return new NodeHelpText
            {
                Summary = nodeType == "subflow-in" 
                    ? "Subflow Input - receives messages passed into this subflow when it's used as a node in another flow."
                    : "Subflow Output - sends messages out of this subflow when it's used as a node in another flow.",
                Details = nodeType == "subflow-in"
                    ? "The subflow input node defines the entry point for messages into this subflow. Any message sent to a subflow instance will arrive at this input node."
                    : "The subflow output node defines the exit point for messages from this subflow. Messages sent to this node will be passed out of the subflow instance."
            };
        }
        
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

    /// <summary>
    /// Gets the status color for the sidebar info panel
    /// </summary>
    private string GetSidebarStatusColor()
    {
        if (SelectedDiagramNode == null) return "transparent";
        var status = GetNodeStatus(SelectedDiagramNode.ID);
        if (status == null) return "transparent";
        return GetStatusColor(status.Color);
    }

    /// <summary>
    /// Gets the current flow name
    /// </summary>
    private string GetCurrentFlowName()
    {
        return Flows.FirstOrDefault(f => f.Id == CurrentFlowId)?.Label ?? "Flow 1";
    }

    /// <summary>
    /// Gets flows formatted for the Info sidebar outliner
    /// </summary>
    private List<RedUiSidebarInfo.FlowInfo> GetFlowsForOutliner()
    {
        return Flows.Select(f => new RedUiSidebarInfo.FlowInfo
        {
            Id = f.Id,
            Label = f.Label,
            Disabled = f.Disabled
        }).ToList();
    }

    /// <summary>
    /// Gets global config nodes for the Info sidebar outliner
    /// </summary>
    private List<RedUiSidebarInfo.NodeInfo> GetGlobalConfigNodesForOutliner()
    {
        var configCategory = PaletteCategories.FirstOrDefault(c => c.Name == "config");
        if (configCategory == null) return new();
        
        return configCategory.Nodes.Select(n => new RedUiSidebarInfo.NodeInfo
        {
            Id = n.Type,
            Type = n.Type,
            Name = n.Label,
            Color = n.Color
        }).ToList();
    }

    /// <summary>
    /// Gets nodes for a specific flow in the Info sidebar outliner
    /// </summary>
    private List<RedUiSidebarInfo.NodeInfo> GetFlowNodesForOutliner(string flowId)
    {
        var flow = Flows.FirstOrDefault(f => f.Id == flowId);
        if (flow == null) return new();
        
        // If this is the current flow, get nodes from DiagramNodes
        if (flowId == CurrentFlowId && DiagramNodes != null)
        {
            return DiagramNodes
                .Where(n => n.ID != null)
                .Select(n => new RedUiSidebarInfo.NodeInfo
                {
                    Id = n.ID!,
                    Type = GetNodeType(n),
                    Name = n.Annotations?.FirstOrDefault(a => a.ID == "labelAnnotation")?.Content,
                    Color = GetNodeColor(n)
                }).ToList();
        }
        
        // Otherwise, get from stored nodes
        return flow.StoredNodes.Select(n => new RedUiSidebarInfo.NodeInfo
        {
            Id = n.Id,
            Type = n.Type,
            Name = n.LabelContent,
            Color = n.Color
        }).ToList();
    }

    /// <summary>
    /// Gets the node type from a diagram node
    /// </summary>
    private string GetNodeType(Node node)
    {
        if (node.AdditionalInfo != null && node.AdditionalInfo.TryGetValue("nodeType", out var typeObj))
        {
            return typeObj?.ToString() ?? "unknown";
        }
        return "unknown";
    }

    /// <summary>
    /// Gets the node color from a diagram node
    /// </summary>
    private string GetNodeColor(Node node)
    {
        if (node.Style != null && !string.IsNullOrEmpty(node.Style.Fill))
        {
            return node.Style.Fill;
        }
        return "#ddd";
    }

    /// <summary>
    /// Gets the node color by type name for quick add dialog
    /// </summary>
    private string GetNodeColor(string nodeType)
    {
        var definition = NodeRegistry.GetAllDefinitions().FirstOrDefault(d => d.Type == nodeType);
        return definition?.Color ?? "#87A980"; // Use NodeDefinition default color
    }

    /// <summary>
    /// Selects a node by ID from the outliner
    /// </summary>
    private async Task SelectNodeById(string nodeId)
    {
        // Find the node in the diagram and select it
        var node = DiagramNodes?.FirstOrDefault(n => n.ID == nodeId);
        if (node != null)
        {
            SelectedDiagramNode = node;
            SelectedNodeName = node.Annotations?.FirstOrDefault(a => a.ID == "labelAnnotation")?.Content ?? "";
            StateHasChanged();
        }
    }

    /// <summary>
    /// Selects a flow by ID from the outliner
    /// </summary>
    private async Task SelectFlowById(string flowId)
    {
        if (flowId != CurrentFlowId)
        {
            SwitchFlow(flowId);
        }
    }

    /// <summary>
    /// Reveals the selected node in the workspace by panning/zooming to it.
    /// </summary>
    private void RevealSelectedNode()
    {
        if (SelectedDiagramNode != null && DiagramInstance != null)
        {
            DiagramNavigation.RevealNode(DiagramInstance, SelectedDiagramNode);
        }
        StateHasChanged();
    }

    /// <summary>
    /// Copies the node link/path to clipboard
    /// </summary>
    private async Task CopyNodeLink()
    {
        if (SelectedDiagramNode != null)
        {
            var nodePath = $"#flow/{CurrentFlowId}/node/{SelectedDiagramNode.ID}";
            await ClipboardService.CopyToClipboardAsync(nodePath);
        }
    }

    /// <summary>
    /// Gets help inputs formatted for the Help sidebar component
    /// </summary>
    private List<RedUiSidebarHelp.HelpProperty> GetHelpInputs()
    {
        var nodeHelp = GetNodeHelp(GetSelectedNodeType());
        if (nodeHelp?.Inputs == null) return new();
        
        return nodeHelp.Inputs.Select(i => new RedUiSidebarHelp.HelpProperty
        {
            Name = i.Name,
            Type = i.Type,
            Description = i.Description
        }).ToList();
    }

    /// <summary>
    /// Gets help inputs for the Help tab (uses _helpSelectedNodeType)
    /// </summary>
    private List<RedUiSidebarHelp.HelpProperty> GetHelpInputsForHelp()
    {
        var nodeHelp = GetNodeHelp(GetNodeTypeForHelp());
        if (nodeHelp?.Inputs == null) return new();
        
        return nodeHelp.Inputs.Select(i => new RedUiSidebarHelp.HelpProperty
        {
            Name = i.Name,
            Type = i.Type,
            Description = i.Description
        }).ToList();
    }

    /// <summary>
    /// Gets help outputs formatted for the Help sidebar component
    /// </summary>
    private List<RedUiSidebarHelp.HelpProperty> GetHelpOutputs()
    {
        var nodeHelp = GetNodeHelp(GetSelectedNodeType());
        if (nodeHelp?.Outputs == null) return new();
        
        return nodeHelp.Outputs.Select(o => new RedUiSidebarHelp.HelpProperty
        {
            Name = o.Name,
            Type = o.Type,
            Description = o.Description
        }).ToList();
    }

    /// <summary>
    /// Gets help outputs for the Help tab (uses _helpSelectedNodeType)
    /// </summary>
    private List<RedUiSidebarHelp.HelpProperty> GetHelpOutputsForHelp()
    {
        var nodeHelp = GetNodeHelp(GetNodeTypeForHelp());
        if (nodeHelp?.Outputs == null) return new();
        
        return nodeHelp.Outputs.Select(o => new RedUiSidebarHelp.HelpProperty
        {
            Name = o.Name,
            Type = o.Type,
            Description = o.Description
        }).ToList();
    }

    /// <summary>
    /// Gets help references formatted for the Help sidebar component
    /// </summary>
    private List<RedUiSidebarHelp.HelpReference> GetHelpReferences()
    {
        var nodeHelp = GetNodeHelp(GetSelectedNodeType());
        if (nodeHelp?.References == null) return new();
        
        return nodeHelp.References.Select(r => new RedUiSidebarHelp.HelpReference
        {
            Title = r.Title,
            Url = r.Url
        }).ToList();
    }

    /// <summary>
    /// Gets help references for the Help tab (uses _helpSelectedNodeType)
    /// </summary>
    private List<RedUiSidebarHelp.HelpReference> GetHelpReferencesForHelp()
    {
        var nodeHelp = GetNodeHelp(GetNodeTypeForHelp());
        if (nodeHelp?.References == null) return new();
        
        return nodeHelp.References.Select(r => new RedUiSidebarHelp.HelpReference
        {
            Title = r.Title,
            Url = r.Url
        }).ToList();
    }

    /// <summary>
    /// Gets categories for the Help sidebar tree view
    /// </summary>
    private List<RedUiSidebarHelp.NodeCategory> GetHelpCategories()
    {
        return PaletteCategories.Select(c => new RedUiSidebarHelp.NodeCategory
        {
            Name = c.Name,
            Expanded = false,
            Nodes = c.Nodes.Select(n => new RedUiSidebarHelp.NodeInfo
            {
                Type = n.Type,
                Label = n.Label,
                Color = n.Color
            }).ToList()
        }).ToList();
    }

    /// <summary>
    /// Selects a node type to show help for
    /// </summary>
    private void SelectNodeTypeForHelp(string nodeType)
    {
        _helpSelectedNodeType = nodeType;
        StateHasChanged();
    }

    /// <summary>
    /// Gets configuration nodes for the Config sidebar
    /// </summary>
    private List<RedUiSidebarConfig.ConfigNode> GetConfigNodes()
    {
        // Return config nodes from the palette categories
        var configCategory = PaletteCategories.FirstOrDefault(c => c.Name == "config");
        if (configCategory == null) return new();
        
        return configCategory.Nodes.Select(n => new RedUiSidebarConfig.ConfigNode
        {
            Id = n.Type,
            Type = n.Type,
            Label = n.Label,
            Color = n.Color,
            Scope = "global",
            UsageCount = 0
        }).ToList();
    }

    /// <summary>
    /// Selects a configuration node
    /// </summary>
    private void SelectConfigNode(string id)
    {
        // This would select the config node
        StateHasChanged();
    }

    /// <summary>
    /// Gets node context data from the context service
    /// </summary>
    private Dictionary<string, object?> GetNodeContextData()
    {
        if (SelectedDiagramNode?.ID == null)
            return new Dictionary<string, object?>();
            
        // Use synchronous wrapper - in real app would use async properly
        return _nodeContextCache;
    }

    /// <summary>
    /// Gets flow context data from the context service
    /// </summary>
    private Dictionary<string, object?> GetFlowContextData()
    {
        // Use cached data - refreshed via RefreshFlowContext
        return _flowContextCache;
    }

    /// <summary>
    /// Gets global context data from the context service
    /// </summary>
    private Dictionary<string, object?> GetGlobalContextData()
    {
        // Use cached data - refreshed via RefreshGlobalContext
        return _globalContextCache;
    }
    
    // Context data caches
    private Dictionary<string, object?> _nodeContextCache = new();
    private Dictionary<string, object?> _flowContextCache = new();
    private Dictionary<string, object?> _globalContextCache = new();

    /// <summary>
    /// Refreshes node context data from the context service
    /// </summary>
    private async Task RefreshNodeContext()
    {
        if (SelectedDiagramNode?.ID != null)
        {
            _nodeContextCache = await ContextDataService.GetNodeContextAsync(SelectedDiagramNode.ID);
        }
        StateHasChanged();
    }

    /// <summary>
    /// Refreshes flow context data from the context service
    /// </summary>
    private async Task RefreshFlowContext()
    {
        _flowContextCache = await ContextDataService.GetFlowContextAsync(CurrentFlowId);
        StateHasChanged();
    }

    /// <summary>
    /// Refreshes global context data from the context service
    /// </summary>
    private async Task RefreshGlobalContext()
    {
        _globalContextCache = await ContextDataService.GetGlobalContextAsync();
        StateHasChanged();
    }

    /// <summary>
    /// Gets debug messages formatted for the Debug sidebar component
    /// </summary>
    private List<RedUiSidebarDebug.DebugMessage> GetDebugMessagesForSidebar()
    {
        return DebugMessages.Select(m => new RedUiSidebarDebug.DebugMessage
        {
            NodeId = m.NodeId,
            NodeName = m.NodeName,
            Timestamp = m.Timestamp.DateTime,
            Payload = m.Data,
            Topic = "",
            Level = "log"
        }).ToList();
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

    /// <summary>
    /// Central node data storage (like RED.nodes in JS Node-RED)
    /// Each node has a 'z' property indicating which flow it belongs to
    /// </summary>
    private class NodeData
    {
        public string Id { get; set; } = "";
        public string Z { get; set; } = ""; // Flow ID (like JS Node-RED's z property)
        public string Type { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; } = 122;
        public double H { get; set; } = 25;
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#ddd";
        public string IconClass { get; set; } = "";
        public Dictionary<string, object?> Props { get; set; } = new();
        public bool Changed { get; set; } = false;
        public bool Dirty { get; set; } = false;
    }

    /// <summary>
    /// Central connector data storage
    /// </summary>
    private class ConnectorData
    {
        public string Id { get; set; } = "";
        public string Z { get; set; } = ""; // Flow ID
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
        public bool Status { get; set; } = false; // Whether status output is enabled
        public List<string> NodeIds { get; set; } = new();
        public List<(string ConnectorId, string SourceId, string SourcePort, string TargetId, string TargetPort)> Connections { get; set; } = new();
    }

    private class GroupInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string FlowId { get; set; } = ""; // Which flow this group belongs to
        public int NodeCount { get; set; }
        public List<string> NodeIds { get; set; } = new();
        public string Color { get; set; } = "#FFCCCC";
        public string? FillColor { get; set; }
        public string? StrokeColor { get; set; }
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
        _openSubmenu = 0;
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

    /// <summary>
    /// Handle right-click context menu on workspace
    /// </summary>
    private void OnWorkspaceContextMenu(MouseEventArgs e)
    {
        // If there's a selected node, show node context menu
        if (SelectedDiagramNode != null)
        {
            ShowNodeContextMenu(SelectedDiagramNode, e.ClientX, e.ClientY);
        }
        else
        {
            // Show canvas context menu
            ShowCanvasContextMenu(e.ClientX, e.ClientY);
        }
    }

    /// <summary>
    /// Show help for context menu node
    /// </summary>
    private void ContextMenuShowHelp()
    {
        if (ContextMenuNode != null)
        {
            var nodeType = ContextMenuNode.AdditionalInfo?.TryGetValue("nodeType", out var typeObj) == true
                ? typeObj as string : null;
            if (!string.IsNullOrEmpty(nodeType))
            {
                _helpSelectedNodeType = nodeType;
                _activeSidebarTabId = "help";
            }
        }
        CloseContextMenus();
    }

    /// <summary>
    /// Check if context node is disabled
    /// </summary>
    private bool IsContextNodeDisabled()
    {
        if (ContextMenuNode?.AdditionalInfo == null) return false;
        return ContextMenuNode.AdditionalInfo.TryGetValue("disabled", out var disabled) && disabled is true;
    }

    /// <summary>
    /// Enable context menu node
    /// </summary>
    private void ContextMenuEnableNode()
    {
        if (ContextMenuNode != null)
        {
            ContextMenuNode.AdditionalInfo ??= new Dictionary<string, object?>();
            ContextMenuNode.AdditionalInfo["disabled"] = false;
            HasUnsavedChanges = true;
        }
        CloseContextMenus();
    }

    /// <summary>
    /// Disable context menu node
    /// </summary>
    private void ContextMenuDisableNode()
    {
        if (ContextMenuNode != null)
        {
            ContextMenuNode.AdditionalInfo ??= new Dictionary<string, object?>();
            ContextMenuNode.AdditionalInfo["disabled"] = true;
            HasUnsavedChanges = true;
        }
        CloseContextMenus();
    }

    /// <summary>
    /// Show labels for context menu node
    /// </summary>
    private void ContextMenuShowLabels()
    {
        if (ContextMenuNode != null)
        {
            // Find label annotation and make it visible
            var labelAnnotation = ContextMenuNode.Annotations?.FirstOrDefault(a => a.ID == "labelAnnotation");
            if (labelAnnotation != null)
            {
                labelAnnotation.Visibility = true;
            }
            HasUnsavedChanges = true;
        }
        CloseContextMenus();
    }

    /// <summary>
    /// Hide labels for context menu node
    /// </summary>
    private void ContextMenuHideLabels()
    {
        if (ContextMenuNode != null)
        {
            // Find label annotation and hide it
            var labelAnnotation = ContextMenuNode.Annotations?.FirstOrDefault(a => a.ID == "labelAnnotation");
            if (labelAnnotation != null)
            {
                labelAnnotation.Visibility = false;
            }
            HasUnsavedChanges = true;
        }
        CloseContextMenus();
    }

    /// <summary>
    /// Check if context node belongs to a group
    /// </summary>
    private bool HasContextNodeGroup()
    {
        if (ContextMenuNode == null) return false;
        return Groups.Any(g => g.NodeIds.Contains(ContextMenuNode.ID));
    }

    /// <summary>
    /// Group selection from context menu
    /// </summary>
    private void ContextMenuGroupSelection()
    {
        if (ContextMenuNode != null)
        {
            SelectedDiagramNode = ContextMenuNode;
        }
        GroupSelectedNodes();
        CloseContextMenus();
    }

    /// <summary>
    /// Ungroup selection from context menu
    /// </summary>
    private void ContextMenuUngroupSelection()
    {
        if (ContextMenuNode != null)
        {
            SelectedDiagramNode = ContextMenuNode;
        }
        UngroupSelectedNodes();
        CloseContextMenus();
    }

    /// <summary>
    /// Remove node from group via context menu
    /// </summary>
    private void ContextMenuRemoveFromGroup()
    {
        if (ContextMenuNode != null)
        {
            var group = Groups.FirstOrDefault(g => g.NodeIds.Contains(ContextMenuNode.ID));
            if (group != null)
            {
                group.NodeIds.Remove(ContextMenuNode.ID);
                group.NodeCount--;
                HasUnsavedChanges = true;
            }
        }
        CloseContextMenus();
    }

    /// <summary>
    /// Undo from context menu
    /// </summary>
    private void ContextMenuUndo()
    {
        Undo();
        CloseContextMenus();
    }

    /// <summary>
    /// Redo from context menu
    /// </summary>
    private void ContextMenuRedo()
    {
        Redo();
        CloseContextMenus();
    }

    /// <summary>
    /// Delete and reconnect from context menu
    /// </summary>
    private void ContextMenuDeleteReconnect()
    {
        if (ContextMenuNode != null && DiagramConnectors != null)
        {
            // Find connectors connected to this node
            var incomingConnectors = DiagramConnectors.Where(c => c.TargetID == ContextMenuNode.ID).ToList();
            var outgoingConnectors = DiagramConnectors.Where(c => c.SourceID == ContextMenuNode.ID).ToList();

            // If there's one incoming and one outgoing, reconnect them
            if (incomingConnectors.Count == 1 && outgoingConnectors.Count == 1)
            {
                var incoming = incomingConnectors[0];
                var outgoing = outgoingConnectors[0];

                // Create a new connector that bypasses the deleted node
                var newConnector = new Connector
                {
                    ID = $"connector{++ConnectorCount}",
                    SourceID = incoming.SourceID,
                    SourcePortID = incoming.SourcePortID,
                    TargetID = outgoing.TargetID,
                    TargetPortID = outgoing.TargetPortID,
                    Type = ConnectorSegmentType.Orthogonal,
                    Style = new ShapeStyle { StrokeColor = "#999", StrokeWidth = 2 },
                    TargetDecorator = new DecoratorSettings { Shape = DecoratorShape.None },
                    SourcePoint = new DiagramPoint() { X = FallbackSourcePointX, Y = FallbackSourcePointY },
                    TargetPoint = new DiagramPoint() { X = FallbackTargetPointX, Y = FallbackTargetPointY }
                };

                // Remove old connectors
                DiagramConnectors.Remove(incoming);
                DiagramConnectors.Remove(outgoing);

                // Add new connector
                DiagramConnectors.Add(newConnector);
            }

            // Delete the node
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

    /// <summary>
    /// Export from context menu
    /// </summary>
    private void ContextMenuExport()
    {
        OnExportClick();
        CloseContextMenus();
    }

    /// <summary>
    /// Selection to subflow from context menu
    /// </summary>
    private void ContextMenuSelectionToSubflow()
    {
        CreateSubflowFromSelection();
        CloseContextMenus();
    }

    /// <summary>
    /// Select all nodes from context menu
    /// </summary>
    private async void ContextMenuSelectAll()
    {
        CloseContextMenus();
        if (DiagramInstance != null && DiagramNodes != null && DiagramNodes.Count > 0)
        {
            var allNodes = new System.Collections.ObjectModel.ObservableCollection<IDiagramObject>();
            foreach (var node in DiagramNodes)
            {
                allNodes.Add(node);
            }
            DiagramInstance.Select(allNodes);
            StateHasChanged();
        }
    }

    // Quick add node dialog state
    private bool IsQuickAddDialogOpen = false;
    private string QuickAddSearchQuery = "";
    private double QuickAddX = 0;
    private double QuickAddY = 0;

    /// <summary>
    /// Insert node (opens quick add dialog) from context menu
    /// </summary>
    private void ContextMenuInsertNode()
    {
        QuickAddX = ContextMenuX;
        QuickAddY = ContextMenuY;
        QuickAddSearchQuery = "";
        IsQuickAddDialogOpen = true;
        CloseContextMenus();
    }

    /// <summary>
    /// Gets filtered node types for quick add dialog
    /// </summary>
    private List<string> GetFilteredNodeTypes()
    {
        var allTypes = NodeRegistry.GetAllDefinitions().Select(d => d.Type).ToList();
        if (string.IsNullOrWhiteSpace(QuickAddSearchQuery))
            return allTypes.Take(10).ToList();
        
        return allTypes
            .Where(t => t.Contains(QuickAddSearchQuery, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToList();
    }

    /// <summary>
    /// Adds a node from quick add dialog
    /// </summary>
    private void QuickAddNode(string nodeType)
    {
        AddNodeAtPosition(nodeType, QuickAddX, QuickAddY);
        IsQuickAddDialogOpen = false;
    }

    /// <summary>
    /// Add junction from context menu
    /// </summary>
    private void ContextMenuAddJunction()
    {
        // Create a junction node (a simple pass-through node)
        var nodeId = $"junction{++NodeCount}";
        var node = new Node()
        {
            ID = nodeId,
            OffsetX = ContextMenuX,
            OffsetY = ContextMenuY,
            Width = 10,
            Height = 10,
            Ports = new DiagramObjectCollection<PointPort>
            {
                new PointPort()
                {
                    ID = "port1",
                    Shape = PortShapes.Circle,
                    Offset = new DiagramPoint() { X = 0, Y = 0.5 },
                    Visibility = PortVisibility.Visible,
                    Style = new ShapeStyle { Fill = "#d9d9d9", StrokeColor = "#999" },
                    Width = 8,
                    Height = 8,
                    Constraints = PortConstraints.Default
                },
                new PointPort()
                {
                    ID = "port2",
                    Shape = PortShapes.Circle,
                    Offset = new DiagramPoint() { X = 1, Y = 0.5 },
                    Visibility = PortVisibility.Visible,
                    Style = new ShapeStyle { Fill = "#d9d9d9", StrokeColor = "#999" },
                    Width = 8,
                    Height = 8,
                    Constraints = PortConstraints.Default | PortConstraints.Draw
                }
            },
            Style = new ShapeStyle { Fill = "#999", StrokeColor = "#666", StrokeWidth = 1 },
            Shape = new BasicShape() { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Ellipse },
            Constraints = DefaultNodeConstraints,
            AdditionalInfo = new Dictionary<string, object>
            {
                { "nodeType", "junction" }
            }
        };
        DiagramNodes?.Add(node);

        RecordAction(new EditorAction
        {
            Type = EditorActionType.AddNode,
            NodeId = node.ID,
            NodeData = node
        });

        HasUnsavedChanges = true;
        CloseContextMenus();
    }

    /// <summary>
    /// Import from context menu
    /// </summary>
    private void ContextMenuImport()
    {
        OnImportClick();
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
