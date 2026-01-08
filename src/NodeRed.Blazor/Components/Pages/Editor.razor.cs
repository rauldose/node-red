// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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

    // Debug messages
    private List<DebugMessage> DebugMessages = new();

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

        PaletteCategories = new List<PaletteCategory>
        {
            commonNodes,
            functionNodes,
            networkNodes,
            sequenceNodes,
            parserNodes,
            storageNodes
        };
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
        var node = new Node()
        {
            ID = id,
            OffsetX = x,
            OffsetY = y,
            Width = 120,
            Height = 25,
            Ports = CreatePorts(nodeType),
            Style = new ShapeStyle { Fill = color, StrokeColor = "#999", StrokeWidth = 1 },
            Shape = new BasicShape() { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 5 },
            Constraints = DefaultNodeConstraints,
            Annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation
                {
                    Content = label,
                    Style = new TextStyle() { Color = "#333", FontSize = 13 }
                }
            },
            AdditionalInfo = new Dictionary<string, object> { { "nodeType", nodeType }, { "color", color } }
        };

        DiagramNodes!.Add(node);
    }

    private void InitDiagramModel()
    {
        // Create sample flow nodes only (no connectors initially)
        // Users can draw connections by clicking on ports
        CreateNode("inject1", 150, 150, "inject", "timestamp", "#a6bbcf");
        CreateNode("function1", 350, 150, "function", "function", "#fdd0a2");
        CreateNode("debug1", 550, 150, "debug", "msg.payload", "#87a980");

        // Note: Connectors are not created here to avoid initialization issues
        // Users can draw connections by clicking on the output port (right side)
        // and dragging to an input port (left side)
    }

    private void CreateNode(string id, double x, double y, string nodeType, string label, string color)
    {
        var node = new Node()
        {
            ID = id,
            OffsetX = x,
            OffsetY = y,
            Width = 120,
            Height = 25,
            Ports = CreatePorts(nodeType),
            Style = new ShapeStyle { Fill = color, StrokeColor = "#999", StrokeWidth = 1 },
            Shape = new BasicShape() { Type = NodeShapes.Basic, Shape = NodeBasicShapes.Rectangle, CornerRadius = 5 },
            Constraints = DefaultNodeConstraints,
            Annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation
                {
                    Content = label,
                    Style = new TextStyle() { Color = "#333", FontSize = 13 }
                }
            },
            AdditionalInfo = new Dictionary<string, object> { { "nodeType", nodeType }, { "color", color } }
        };
        DiagramNodes!.Add(node);
        NodeCount++;
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
        // Double-click now shows properties in sidebar (no dialog needed)
        // Just ensure the node is selected and sidebar shows Info tab
        if (args.Count == 2 && SelectedDiagramNode != null)
        {
            SelectedSidebarTab = 0; // Switch to Info/Properties tab
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

    private void EditFlowProperties(string flowId)
    {
        // TODO: Open flow properties dialog
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
            }
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
            try
            {
                await FlowRuntime.InjectAsync(SelectedDiagramNode.ID);
                DebugMessages.Add(new DebugMessage
                {
                    NodeId = SelectedDiagramNode.ID,
                    NodeName = SelectedNodeName,
                    Data = $"Injected at {DateTimeOffset.Now:HH:mm:ss}",
                    Timestamp = DateTimeOffset.Now
                });
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

    private void FilterDebugMessages()
    {
        // TODO: Implement debug message filtering
    }

    private void HighlightDebugNode(string nodeId)
    {
        // TODO: Highlight node in diagram
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
}
