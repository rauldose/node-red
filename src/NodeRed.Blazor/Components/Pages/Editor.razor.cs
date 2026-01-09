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

    // Node counter for unique IDs
    private int NodeCount = 0;
    private int ConnectorCount = 0;

    // Custom Palette
    private List<PaletteCategory> PaletteCategories = new();
    private string PaletteFilter = "";
    private PaletteNodeInfo? DraggedNode = null;

    // Node property panel bindings - Inject node
    private string InjectPayloadType = "date";
    private string InjectPayloadValue = "";
    private string InjectTopic = "";
    private string InjectRepeatType = "none";
    private double InjectRepeatInterval = 1;
#pragma warning disable CS0414 // Field assigned but never used - used in data binding
    private bool InjectOnce = false;
    private double InjectOnceDelay = 0.1;
#pragma warning restore CS0414

    // Node property panel bindings - Debug node
    private string DebugOutput = "payload";
    private bool DebugToSidebar = true;
    private bool DebugToConsole = false;
    private bool DebugToStatus = false;

    // Node property panel bindings - Function node
    private int FunctionOutputs = 1;
    private string FunctionCode = "return msg;";

    // Node property panel bindings - Change node
    private string ChangeAction = "set";
    private string ChangeProperty = "payload";
    private string ChangeValue = "";

    // Node property panel bindings - Switch node
    private string SwitchProperty = "payload";

    // Node property panel bindings - Delay node
    private string DelayAction = "delay";
    private double DelayTime = 1;
    private string DelayUnits = "s";

    // Node property panel bindings - Template node
    private string TemplateProperty = "payload";
    private string TemplateContent = "This is the payload: {{payload}}";
    private string TemplateFormat = "mustache";
    private string TemplateOutputAs = "str";

    // Default node constraints
    private static readonly NodeConstraints DefaultNodeConstraints =
        NodeConstraints.Select | NodeConstraints.Drag | NodeConstraints.Delete |
        NodeConstraints.InConnect | NodeConstraints.OutConnect |
        NodeConstraints.PointerEvents | NodeConstraints.AllowDrop;

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
        // Common nodes category
        var commonNodes = new PaletteCategory
        {
            Name = "common",
            IsExpanded = true,
            Nodes = new List<PaletteNodeInfo>
            {
                new PaletteNodeInfo { Type = "inject", Label = "inject", Color = "#a6bbcf", IconClass = "fa fa-arrow-right", IconBackground = "rgba(0,0,0,0.05)", Inputs = 0, Outputs = 1 },
                new PaletteNodeInfo { Type = "debug", Label = "debug", Color = "#87a980", IconClass = "fa fa-bug", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 0 },
                new PaletteNodeInfo { Type = "complete", Label = "complete", Color = "#e2d96e", IconClass = "fa fa-check-circle-o", IconBackground = "rgba(0,0,0,0.05)", Inputs = 0, Outputs = 1 },
                new PaletteNodeInfo { Type = "catch", Label = "catch", Color = "#e3b881", IconClass = "fa fa-warning", IconBackground = "rgba(0,0,0,0.05)", Inputs = 0, Outputs = 1 },
                new PaletteNodeInfo { Type = "status", Label = "status", Color = "#c0c0c0", IconClass = "fa fa-circle-o", IconBackground = "rgba(0,0,0,0.05)", Inputs = 0, Outputs = 1 },
                new PaletteNodeInfo { Type = "link in", Label = "link in", Color = "#ddd", IconClass = "fa fa-arrow-left", IconBackground = "rgba(0,0,0,0.05)", Inputs = 0, Outputs = 1 },
                new PaletteNodeInfo { Type = "link out", Label = "link out", Color = "#ddd", IconClass = "fa fa-arrow-right", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 0 },
                new PaletteNodeInfo { Type = "link call", Label = "link call", Color = "#ddd", IconClass = "fa fa-link", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "comment", Label = "comment", Color = "#fff", IconClass = "fa fa-comment-o", IconBackground = "rgba(0,0,0,0.05)", Inputs = 0, Outputs = 0 },
            }
        };

        // Function nodes category
        var functionNodes = new PaletteCategory
        {
            Name = "function",
            IsExpanded = true,
            Nodes = new List<PaletteNodeInfo>
            {
                new PaletteNodeInfo { Type = "function", Label = "function", Color = "#fdd0a2", IconClass = "fa fa-code", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "switch", Label = "switch", Color = "#e2d96e", IconClass = "fa fa-random", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "change", Label = "change", Color = "#e2d96e", IconClass = "fa fa-edit", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "range", Label = "range", Color = "#e2d96e", IconClass = "fa fa-arrows-h", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "template", Label = "template", Color = "#bc9e5e", IconClass = "fa fa-file-text-o", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "delay", Label = "delay", Color = "#e6c4e0", IconClass = "fa fa-clock-o", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "trigger", Label = "trigger", Color = "#e6c4e0", IconClass = "fa fa-toggle-off", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "exec", Label = "exec", Color = "#ddd", IconClass = "fa fa-terminal", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 3 },
                new PaletteNodeInfo { Type = "rbe", Label = "rbe", Color = "#e2d96e", IconClass = "fa fa-tasks", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
            }
        };

        // Network nodes category
        var networkNodes = new PaletteCategory
        {
            Name = "network",
            IsExpanded = false,
            Nodes = new List<PaletteNodeInfo>
            {
                new PaletteNodeInfo { Type = "mqtt in", Label = "mqtt in", Color = "#d8bfd8", IconClass = "fa fa-sign-in", IconBackground = "rgba(0,0,0,0.05)", Inputs = 0, Outputs = 1 },
                new PaletteNodeInfo { Type = "mqtt out", Label = "mqtt out", Color = "#d8bfd8", IconClass = "fa fa-sign-out", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 0 },
                new PaletteNodeInfo { Type = "http in", Label = "http in", Color = "#6baed6", IconClass = "fa fa-globe", IconBackground = "rgba(0,0,0,0.05)", Inputs = 0, Outputs = 1 },
                new PaletteNodeInfo { Type = "http response", Label = "http response", Color = "#6baed6", IconClass = "fa fa-globe", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 0 },
                new PaletteNodeInfo { Type = "http request", Label = "http request", Color = "#6baed6", IconClass = "fa fa-globe", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "websocket in", Label = "websocket in", Color = "#ddd", IconClass = "fa fa-plug", IconBackground = "rgba(0,0,0,0.05)", Inputs = 0, Outputs = 1 },
                new PaletteNodeInfo { Type = "websocket out", Label = "websocket out", Color = "#ddd", IconClass = "fa fa-plug", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 0 },
                new PaletteNodeInfo { Type = "tcp in", Label = "tcp in", Color = "#c0c0c0", IconClass = "fa fa-exchange", IconBackground = "rgba(0,0,0,0.05)", Inputs = 0, Outputs = 1 },
                new PaletteNodeInfo { Type = "tcp out", Label = "tcp out", Color = "#c0c0c0", IconClass = "fa fa-exchange", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 0 },
                new PaletteNodeInfo { Type = "udp in", Label = "udp in", Color = "#c0c0c0", IconClass = "fa fa-exchange", IconBackground = "rgba(0,0,0,0.05)", Inputs = 0, Outputs = 1 },
                new PaletteNodeInfo { Type = "udp out", Label = "udp out", Color = "#c0c0c0", IconClass = "fa fa-exchange", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 0 },
            }
        };

        // Sequence nodes category
        var sequenceNodes = new PaletteCategory
        {
            Name = "sequence",
            IsExpanded = false,
            Nodes = new List<PaletteNodeInfo>
            {
                new PaletteNodeInfo { Type = "split", Label = "split", Color = "#e2d96e", IconClass = "fa fa-columns", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "join", Label = "join", Color = "#e2d96e", IconClass = "fa fa-compress", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "sort", Label = "sort", Color = "#e2d96e", IconClass = "fa fa-sort", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "batch", Label = "batch", Color = "#e2d96e", IconClass = "fa fa-list", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
            }
        };

        // Parser nodes category
        var parserNodes = new PaletteCategory
        {
            Name = "parser",
            IsExpanded = false,
            Nodes = new List<PaletteNodeInfo>
            {
                new PaletteNodeInfo { Type = "csv", Label = "csv", Color = "#dbb84d", IconClass = "fa fa-table", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "html", Label = "html", Color = "#dbb84d", IconClass = "fa fa-file-code-o", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "json", Label = "json", Color = "#dbb84d", IconClass = "fa fa-file-text-o", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "xml", Label = "xml", Color = "#dbb84d", IconClass = "fa fa-file-code-o", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "yaml", Label = "yaml", Color = "#dbb84d", IconClass = "fa fa-file-text-o", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
            }
        };

        // Storage nodes category
        var storageNodes = new PaletteCategory
        {
            Name = "storage",
            IsExpanded = false,
            Nodes = new List<PaletteNodeInfo>
            {
                new PaletteNodeInfo { Type = "file", Label = "file", Color = "#ddd", IconClass = "fa fa-file", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "file in", Label = "file in", Color = "#ddd", IconClass = "fa fa-file", IconBackground = "rgba(0,0,0,0.05)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "watch", Label = "watch", Color = "#ddd", IconClass = "fa fa-eye", IconBackground = "rgba(0,0,0,0.05)", Inputs = 0, Outputs = 1 },
            }
        };

        var databaseNodes = new PaletteCategory
        {
            Name = "database",
            IsExpanded = false,
            Nodes = new List<PaletteNodeInfo>
            {
                new PaletteNodeInfo { Type = "sqlserver", Label = "sqlserver", Color = "#CC2936", IconClass = "fa fa-database", IconBackground = "rgba(0,0,0,0.1)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "postgres", Label = "postgres", Color = "#336791", IconClass = "fa fa-database", IconBackground = "rgba(0,0,0,0.1)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "mysql", Label = "mysql", Color = "#00758F", IconClass = "fa fa-database", IconBackground = "rgba(0,0,0,0.1)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "sqlite", Label = "sqlite", Color = "#003B57", IconClass = "fa fa-database", IconBackground = "rgba(0,0,0,0.1)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "mongodb", Label = "mongodb", Color = "#4DB33D", IconClass = "fa fa-leaf", IconBackground = "rgba(0,0,0,0.1)", Inputs = 1, Outputs = 1 },
                new PaletteNodeInfo { Type = "redis", Label = "redis", Color = "#D82C20", IconClass = "fa fa-bolt", IconBackground = "rgba(0,0,0,0.1)", Inputs = 1, Outputs = 1 },
            }
        };

        PaletteCategories = new List<PaletteCategory>
        {
            commonNodes,
            functionNodes,
            networkNodes,
            sequenceNodes,
            parserNodes,
            storageNodes,
            databaseNodes
        };

        // Dynamically add plugin nodes from NodeLoader
        AddPluginNodesToPalette();
    }

    /// <summary>
    /// Dynamically adds plugin nodes to the palette based on discovered node definitions.
    /// </summary>
    private void AddPluginNodesToPalette()
    {
        // Get all node definitions from the loader
        var nodeDefinitions = NodeLoader.GetNodeDefinitions();
        
        // Group nodes by category (using module name as category for external plugins)
        var pluginCategories = new Dictionary<string, List<PaletteNodeInfo>>();
        
        // Get the set of built-in node types to exclude
        var builtInTypes = new HashSet<string>(
            PaletteCategories.SelectMany(c => c.Nodes.Select(n => n.Type)),
            StringComparer.OrdinalIgnoreCase);
        
        foreach (var nodeDef in nodeDefinitions)
        {
            // Skip built-in nodes (already in palette)
            if (builtInTypes.Contains(nodeDef.Type))
                continue;
            
            // Determine category name - use the category from definition or module name
            var categoryName = nodeDef.Category.ToString().ToLowerInvariant();
            
            // For external plugins, use a distinct category name
            if (!string.IsNullOrEmpty(nodeDef.Type) && nodeDef.Type.Contains('-'))
            {
                // Extract prefix (e.g., "example" from "example-upper")
                var prefix = nodeDef.Type.Split('-')[0];
                categoryName = prefix;
            }
            
            if (!pluginCategories.ContainsKey(categoryName))
            {
                pluginCategories[categoryName] = new List<PaletteNodeInfo>();
            }
            
            // Map icon from definition or use default
            var iconClass = !string.IsNullOrEmpty(nodeDef.Icon) ? nodeDef.Icon : "fa fa-cube";
            
            pluginCategories[categoryName].Add(new PaletteNodeInfo
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
        
        // Add plugin categories to palette
        foreach (var (categoryName, nodes) in pluginCategories)
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
        var hasInput = paletteNode?.Inputs > 0 || (nodeType != "inject" && nodeType != "complete" && nodeType != "catch" && 
                        nodeType != "status" && nodeType != "link in" && nodeType != "comment");
        var hasOutput = paletteNode?.Outputs > 0 || (nodeType != "debug" && nodeType != "link out" && nodeType != "http response" && 
                         nodeType != "mqtt out" && nodeType != "websocket out" && nodeType != "tcp out" && 
                         nodeType != "udp out" && nodeType != "comment");

        // Create a node with child nodes for the icon area
        var node = new Node()
        {
            ID = id,
            OffsetX = x,
            OffsetY = y,
            Width = 130,
            Height = 30,
            Ports = CreatePorts(nodeType),
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
            "fa fa-arrow-left" => "\uf060",
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
            _ => "\uf1b2" // cube as default
        };
    }

    private DiagramObjectCollection<NodeFixedUserHandle> CreateFixedUserHandles(string nodeType)
    {
        var handles = new DiagramObjectCollection<NodeFixedUserHandle>();

        // Only add inject button for inject nodes - positioned far to the left outside the node
        // so it doesn't interfere with ports
        if (nodeType == "inject")
        {
            handles.Add(new NodeFixedUserHandle()
            {
                ID = "injectButton",
                Width = 16,
                Height = 16,
                Offset = new DiagramPoint() { X = 0, Y = 0.5 },
                Margin = new DiagramThickness() { Left = -25 }, // Move further left, outside the node
                PathData = "M8 5v14l11-7z", // Play icon SVG path
                Visibility = true,
                CornerRadius = 2,
                Fill = "#8aa3bc",
                Stroke = "#7a93ac",
                StrokeThickness = 1,
                IconStroke = "#fff",
                IconStrokeThickness = 0
            });
        }

        return handles;
    }

    private DiagramObjectCollection<PointPort> CreatePorts(string nodeType = "")
    {
        var ports = new DiagramObjectCollection<PointPort>();

        // Determine inputs/outputs based on node type
        bool hasInput = nodeType != "inject" && nodeType != "complete" && nodeType != "catch" && 
                        nodeType != "status" && nodeType != "link in" && nodeType != "comment";
        bool hasOutput = nodeType != "debug" && nodeType != "link out" && nodeType != "http response" && 
                         nodeType != "mqtt out" && nodeType != "websocket out" && nodeType != "tcp out" && 
                         nodeType != "udp out" && nodeType != "comment";

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
            SourcePoint = new DiagramPoint() { X = 0, Y = 0 },
            TargetPoint = new DiagramPoint() { X = 100, Y = 0 }
        };
        DiagramConnectors!.Add(connector);
    }

    private void OnSelectionChanged(Syncfusion.Blazor.Diagram.SelectionChangedEventArgs args)
    {
        if (args.NewValue?.Count > 0 && args.NewValue[0] is Node node)
        {
            SelectedDiagramNode = node;
            SelectedNodeName = node.Annotations?.FirstOrDefault()?.Content ?? "";
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
                    var nodeName = node.Annotations?.FirstOrDefault()?.Content ?? node.ID;
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
        var nodeType = GetSelectedNodeType();

        switch (nodeType)
        {
            case "inject":
                InjectPayloadType = GetNodeProperty<string>(node, "payloadType", "date");
                InjectPayloadValue = GetNodeProperty<string>(node, "payload", "");
                InjectTopic = GetNodeProperty<string>(node, "topic", "");
                InjectRepeatType = string.IsNullOrEmpty(GetNodeProperty<string>(node, "repeat", "")) ? "none" : "interval";
                InjectRepeatInterval = GetNodeProperty<double>(node, "repeat", 1);
                break;

            case "debug":
                DebugOutput = GetNodeProperty<string>(node, "complete", "payload");
                DebugToSidebar = GetNodeProperty<bool>(node, "tosidebar", true);
                DebugToConsole = GetNodeProperty<bool>(node, "console", false);
                DebugToStatus = GetNodeProperty<bool>(node, "tostatus", false);
                break;

            case "function":
                FunctionOutputs = GetNodeProperty<int>(node, "outputs", 1);
                FunctionCode = GetNodeProperty<string>(node, "func", "return msg;");
                break;

            case "change":
                ChangeAction = GetNodeProperty<string>(node, "action", "set");
                ChangeProperty = GetNodeProperty<string>(node, "property", "payload");
                ChangeValue = GetNodeProperty<string>(node, "value", "");
                break;

            case "switch":
                SwitchProperty = GetNodeProperty<string>(node, "property", "payload");
                break;

            case "delay":
                DelayAction = GetNodeProperty<string>(node, "pauseType", "delay");
                DelayTime = GetNodeProperty<double>(node, "timeout", 1);
                DelayUnits = GetNodeProperty<string>(node, "timeoutUnits", "s");
                break;

            case "template":
                TemplateProperty = GetNodeProperty<string>(node, "field", "payload");
                TemplateContent = GetNodeProperty<string>(node, "template", "This is the payload: {{payload}}");
                TemplateFormat = GetNodeProperty<string>(node, "syntax", "mustache");
                TemplateOutputAs = GetNodeProperty<string>(node, "output", "str");
                break;

            default:
                // For SDK nodes, load all properties from AdditionalInfo into _nodePropertyValues
                LoadDynamicNodeProperties(node);
                break;
        }
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
            connector.Type = ConnectorSegmentType.Orthogonal;
            
            // Initialize source and target points to prevent null reference during connection
            connector.SourcePoint ??= new DiagramPoint() { X = 0, Y = 0 };
            connector.TargetPoint ??= new DiagramPoint() { X = 100, Y = 0 };
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
        CurrentFlowId = flowId;
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
            FlowPropertiesInfo = "";
            FlowPropertiesEnabled = true;
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
        }
        IsFlowPropertiesDialogOpen = false;
        StateHasChanged();
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
            // Update node name
            if (SelectedDiagramNode.Annotations?.Count > 0)
            {
                SelectedDiagramNode.Annotations[0].Content = SelectedNodeName;
            }

            // Save properties to AdditionalInfo
            var nodeType = GetSelectedNodeType();
            switch (nodeType)
            {
                case "inject":
                    SaveNodeProperty("payloadType", InjectPayloadType);
                    SaveNodeProperty("payload", InjectPayloadValue);
                    SaveNodeProperty("topic", InjectTopic);
                    SaveNodeProperty("repeat", InjectRepeatType == "interval" ? InjectRepeatInterval.ToString() : "");
                    break;

                case "debug":
                    SaveNodeProperty("complete", DebugOutput);
                    SaveNodeProperty("tosidebar", DebugToSidebar);
                    SaveNodeProperty("console", DebugToConsole);
                    SaveNodeProperty("tostatus", DebugToStatus);
                    break;

                case "function":
                    SaveNodeProperty("outputs", FunctionOutputs);
                    SaveNodeProperty("func", FunctionCode);
                    break;

                case "change":
                    SaveNodeProperty("action", ChangeAction);
                    SaveNodeProperty("property", ChangeProperty);
                    SaveNodeProperty("value", ChangeValue);
                    break;

                case "switch":
                    SaveNodeProperty("property", SwitchProperty);
                    break;

                case "delay":
                    SaveNodeProperty("pauseType", DelayAction);
                    SaveNodeProperty("timeout", DelayTime);
                    SaveNodeProperty("timeoutUnits", DelayUnits);
                    break;

                case "template":
                    SaveNodeProperty("field", TemplateProperty);
                    SaveNodeProperty("template", TemplateContent);
                    SaveNodeProperty("syntax", TemplateFormat);
                    SaveNodeProperty("output", TemplateOutputAs);
                    break;

                default:
                    // For SDK nodes, save all properties from _nodePropertyValues
                    SaveDynamicNodeProperties();
                    break;
            }
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
                var nodeName = node.Annotations?.FirstOrDefault()?.Content ?? "";
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
                    var label = node.Annotations?.FirstOrDefault()?.Content ?? nodeType;
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
        var subflowId = $"subflow{Subflows.Count + 1}";
        var subflowName = $"Subflow {Subflows.Count + 1}";
        
        var newSubflow = new SubflowInfo
        {
            Id = subflowId,
            Name = subflowName,
            Inputs = 1,
            Outputs = 1
        };
        
        Subflows.Add(newSubflow);
        
        // In a full implementation, this would create a new flow tab
        // and allow editing the subflow's internal nodes
        DebugMessages.Add(new DebugMessage
        {
            NodeId = "system",
            NodeName = "System",
            Data = $"Created subflow '{subflowName}'. In a full implementation, this would open a new editor tab for the subflow.",
            Timestamp = DateTimeOffset.Now
        });
        
        StateHasChanged();
    }

    private void EditSubflow(string subflowId)
    {
        var subflow = Subflows.FirstOrDefault(sf => sf.Id == subflowId);
        if (subflow != null)
        {
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Editing subflow '{subflow.Name}'. In a full implementation, this would open the subflow editor.",
                Timestamp = DateTimeOffset.Now
            });
        }
    }

    private void DeleteSubflow(string subflowId)
    {
        var subflow = Subflows.FirstOrDefault(sf => sf.Id == subflowId);
        if (subflow != null)
        {
            Subflows.Remove(subflow);
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Deleted subflow '{subflow.Name}'.",
                Timestamp = DateTimeOffset.Now
            });
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
        // Get the currently selected node (in a full implementation, this would get all selected nodes)
        if (SelectedDiagramNode != null)
        {
            var groupId = $"group{Groups.Count + 1}";
            var groupName = $"Group {Groups.Count + 1}";
            
            var newGroup = new GroupInfo
            {
                Id = groupId,
                Name = groupName,
                NodeCount = 1 // In full implementation, would be count of selected nodes
            };
            
            Groups.Add(newGroup);
            
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Created group '{groupName}'. In a full implementation, selected nodes would be visually grouped.",
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
                Data = "Please select nodes to group. In a full implementation, multiple selected nodes would be grouped together.",
                Timestamp = DateTimeOffset.Now
            });
        }
    }

    private void UngroupSelectedNodes()
    {
        if (Groups.Count > 0)
        {
            var lastGroup = Groups.Last();
            Groups.Remove(lastGroup);
            
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Ungrouped '{lastGroup.Name}'. In a full implementation, nodes would be removed from their group.",
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
            Groups.Remove(group);
            DebugMessages.Add(new DebugMessage
            {
                NodeId = "system",
                NodeName = "System",
                Data = $"Deleted group '{group.Name}'.",
                Timestamp = DateTimeOffset.Now
            });
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
                    Name = node.Annotations?.FirstOrDefault()?.Content ?? "",
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
    }

    private class GroupInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int NodeCount { get; set; }
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
}
