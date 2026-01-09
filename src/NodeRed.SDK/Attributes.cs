// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Enums;

namespace NodeRed.SDK;

/// <summary>
/// Attribute to define a node type. Apply this to your node class.
/// 
/// Example:
/// <code>
/// [NodeType("my-node", "My Node", Category = NodeCategory.Function, Color = "#FFCC00")]
/// public class MyNode : NodeBase { }
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class NodeTypeAttribute : Attribute
{
    /// <summary>
    /// Unique type identifier for this node (e.g., "inject", "my-custom-node").
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Display name shown in the palette and editor.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Category in the palette (e.g., Common, Function, Network).
    /// </summary>
    public NodeCategory Category { get; set; } = NodeCategory.Common;

    /// <summary>
    /// Background color (hex, e.g., "#a6bbcf").
    /// </summary>
    public string Color { get; set; } = "#87A980";

    /// <summary>
    /// Font Awesome icon class (e.g., "fa fa-bug").
    /// </summary>
    public string Icon { get; set; } = "fa fa-cube";

    /// <summary>
    /// Number of input ports (0 or 1).
    /// </summary>
    public int Inputs { get; set; } = 1;

    /// <summary>
    /// Number of output ports.
    /// </summary>
    public int Outputs { get; set; } = 1;

    /// <summary>
    /// Whether this node has an action button (like Inject).
    /// </summary>
    public bool HasButton { get; set; } = false;

    public NodeTypeAttribute(string type, string displayName)
    {
        Type = type;
        DisplayName = displayName;
    }
}

/// <summary>
/// Attribute to define a property on a node.
/// Apply this to properties of your node class for automatic binding.
/// 
/// Example:
/// <code>
/// [NodeProperty("topic", "Topic", Icon = "fa fa-tag")]
/// public string Topic { get; set; } = "";
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class NodePropertyAttribute : Attribute
{
    public string Name { get; }
    public string Label { get; }
    public string Icon { get; set; } = "";
    public string Placeholder { get; set; } = "";
    public string Prefix { get; set; } = "";
    public string Suffix { get; set; } = "";
    public bool Required { get; set; } = false;
    public bool IsSmall { get; set; } = false;
    public bool IsFullWidth { get; set; } = false;
    public string? ShowWhen { get; set; }
    public string? HideWhen { get; set; }

    public NodePropertyAttribute(string name, string label)
    {
        Name = name;
        Label = label;
    }
}

/// <summary>
/// Attribute to mark a property as a credential (stored securely, not exported).
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class NodeCredentialAttribute : Attribute
{
    public string Name { get; }
    public bool IsPassword { get; set; } = false;

    public NodeCredentialAttribute(string name)
    {
        Name = name;
    }
}

/// <summary>
/// Attribute to define a node module (package of nodes).
/// Apply this at the ASSEMBLY level to define a module containing multiple nodes.
/// 
/// This follows Node-RED's pattern where:
/// - Core nodes are in @node-red/nodes package (one package, many nodes)
/// - Contrib modules like node-red-dashboard contain multiple related nodes
/// - Each module/assembly shares its dependencies
/// 
/// Example in AssemblyInfo.cs:
/// [assembly: NodeModule("@myorg/node-red-contrib-mymodule", 
///     Version = "1.0.0", 
///     Description = "My custom dashboard nodes")]
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public class NodeModuleAttribute : Attribute
{
    /// <summary>
    /// Module name (e.g., "@myorg/node-red-contrib-mymodule").
    /// Following Node-RED naming conventions.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Module version (semver format).
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Module description.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Module author.
    /// </summary>
    public string Author { get; set; } = "";

    /// <summary>
    /// Minimum Node-RED .NET runtime version required.
    /// Similar to Node-RED's "node-red": { "version": ">=1.0.0" } in package.json.
    /// </summary>
    public string MinVersion { get; set; } = "1.0.0";

    public NodeModuleAttribute(string name)
    {
        Name = name;
    }
}
