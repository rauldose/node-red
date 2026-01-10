// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Blazor.Models;

/// <summary>
/// Represents a flow tab in the editor
/// </summary>
public class FlowTab
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
public class FlowNodeData
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
public class FlowConnectorData
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
public class NodeData
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
public class ConnectorData
{
    public string Id { get; set; } = "";
    public string Z { get; set; } = ""; // Flow ID
    public string SourceId { get; set; } = "";
    public string SourcePortId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string TargetPortId { get; set; } = "";
}

/// <summary>
/// Palette category for organizing nodes
/// </summary>
public class PaletteCategory
{
    public string Name { get; set; } = "";
    public bool IsExpanded { get; set; } = true;
    public List<PaletteNodeInfo> Nodes { get; set; } = new();
}

/// <summary>
/// Palette node information
/// </summary>
public class PaletteNodeInfo
{
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
    public string Color { get; set; } = "#ddd";
    public string IconClass { get; set; } = "";
    public string IconBackground { get; set; } = "rgba(0,0,0,0.05)";
    public int Inputs { get; set; } = 1;
    public int Outputs { get; set; } = 1;
}

/// <summary>
/// Search result for the search dialog
/// </summary>
public class SearchResult
{
    public string Type { get; set; } = "";
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public string FlowId { get; set; } = "";
    public string FlowName { get; set; } = "";
}

/// <summary>
/// Configuration node information
/// </summary>
public class ConfigNodeInfo
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
    public int UsageCount { get; set; }
}

/// <summary>
/// Subflow information
/// </summary>
public class SubflowInfo
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

/// <summary>
/// Group information for visual grouping of nodes
/// </summary>
public class GroupInfo
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

/// <summary>
/// Palette module information
/// </summary>
public class PaletteModuleInfo
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public int NodeCount { get; set; }
    public bool IsInstalled { get; set; }
}

/// <summary>
/// Keyboard shortcut definition
/// </summary>
public class KeyboardShortcut
{
    public string Key { get; set; } = "";
    public string Description { get; set; } = "";
}
