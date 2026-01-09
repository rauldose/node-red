// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Enums;

namespace NodeRed.Core.Entities;

/// <summary>
/// Defines the type and configuration of a node.
/// This is the metadata about a node type, not an instance.
/// Follows Node-RED node definition principles adapted for .NET.
/// </summary>
public class NodeDefinition
{
    /// <summary>
    /// Unique identifier for this node type (e.g., "inject", "debug", "function").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Display name for the node type.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Category this node belongs to (e.g., "common", "function", "network").
    /// Determines where the node appears in the palette.
    /// </summary>
    public NodeCategory Category { get; init; } = NodeCategory.Common;

    /// <summary>
    /// Color of the node in the editor (hex color).
    /// Should match Node-RED's muted palette.
    /// </summary>
    public string Color { get; init; } = "#87A980";

    /// <summary>
    /// Icon class for the node (Font Awesome 4.7 class, e.g., "fa fa-bug").
    /// </summary>
    public string Icon { get; init; } = "fa fa-cube";

    /// <summary>
    /// Number of input ports (0 or 1 typically).
    /// </summary>
    public int Inputs { get; init; } = 1;

    /// <summary>
    /// Number of output ports (0 or more).
    /// </summary>
    public int Outputs { get; init; } = 1;

    /// <summary>
    /// Labels for output ports.
    /// </summary>
    public string[] OutputLabels { get; init; } = [];

    /// <summary>
    /// Labels for input ports.
    /// </summary>
    public string[] InputLabels { get; init; } = [];

    /// <summary>
    /// Whether this is a configuration node (shared settings between nodes).
    /// </summary>
    public bool IsConfigNode { get; init; }

    /// <summary>
    /// Default configuration values for new instances.
    /// Each key maps to a property name, value is the default.
    /// </summary>
    public Dictionary<string, object?> Defaults { get; init; } = new();

    /// <summary>
    /// Property definitions for the node editor.
    /// Defines the UI schema for editing node properties.
    /// </summary>
    public List<NodePropertyDefinition> Properties { get; init; } = new();

    /// <summary>
    /// Credentials definitions (stored separately, not exported in flows).
    /// </summary>
    public List<NodeCredentialDefinition> Credentials { get; init; } = new();

    /// <summary>
    /// Help text / documentation for this node.
    /// First paragraph is used as palette tooltip.
    /// </summary>
    public NodeHelpText Help { get; init; } = new();

    /// <summary>
    /// Simple help text string (for backwards compatibility).
    /// Use Help property for structured help.
    /// </summary>
    public string HelpText { get; init; } = string.Empty;

    /// <summary>
    /// Text alignment for the node label ("left" or "right").
    /// Use "right" for end-of-flow nodes.
    /// </summary>
    public string Align { get; init; } = "left";

    /// <summary>
    /// Whether the node has a button (like Inject or Debug).
    /// </summary>
    public bool HasButton { get; init; } = false;

    /// <summary>
    /// Button configuration if HasButton is true.
    /// </summary>
    public NodeButtonDefinition? Button { get; init; }
}

/// <summary>
/// Defines a property that can be configured in the node editor.
/// </summary>
public class NodePropertyDefinition
{
    /// <summary>
    /// Property name (used as key in node config).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Display label for the property.
    /// </summary>
    public string Label { get; init; } = "";

    /// <summary>
    /// Icon class (Font Awesome).
    /// </summary>
    public string Icon { get; init; } = "";

    /// <summary>
    /// Type of input control.
    /// </summary>
    public PropertyType Type { get; init; } = PropertyType.Text;

    /// <summary>
    /// Default value for the property.
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Whether this property is required.
    /// </summary>
    public bool Required { get; init; } = false;

    /// <summary>
    /// Placeholder text for text inputs.
    /// </summary>
    public string Placeholder { get; init; } = "";

    /// <summary>
    /// Prefix text shown before the input (e.g., "msg.").
    /// </summary>
    public string Prefix { get; init; } = "";

    /// <summary>
    /// Suffix text shown after the input (e.g., "seconds").
    /// </summary>
    public string Suffix { get; init; } = "";

    /// <summary>
    /// Options for select/dropdown type.
    /// </summary>
    public List<PropertyOptionDefinition> Options { get; init; } = new();

    /// <summary>
    /// Minimum value for number inputs.
    /// </summary>
    public double? Min { get; init; }

    /// <summary>
    /// Maximum value for number inputs.
    /// </summary>
    public double? Max { get; init; }

    /// <summary>
    /// Step value for number inputs.
    /// </summary>
    public double? Step { get; init; }

    /// <summary>
    /// Number of rows for textarea inputs.
    /// </summary>
    public int Rows { get; init; } = 3;

    /// <summary>
    /// Condition to show this field: "propertyName=value".
    /// </summary>
    public string? ShowWhen { get; init; }

    /// <summary>
    /// Condition to hide this field: "propertyName=value".
    /// </summary>
    public string? HideWhen { get; init; }

    /// <summary>
    /// Whether this is a small input field.
    /// </summary>
    public bool IsSmall { get; init; } = false;

    /// <summary>
    /// Whether this field spans the full width.
    /// </summary>
    public bool IsFullWidth { get; init; } = false;

    /// <summary>
    /// Validation regex pattern.
    /// </summary>
    public string? ValidationPattern { get; init; }

    /// <summary>
    /// Validation error message.
    /// </summary>
    public string? ValidationMessage { get; init; }

    /// <summary>
    /// Reference to a config node type (for config node selection).
    /// </summary>
    public string? ConfigNodeType { get; init; }
}

/// <summary>
/// Option for select/dropdown properties.
/// </summary>
public class PropertyOptionDefinition
{
    public required string Value { get; init; }
    public required string Label { get; init; }
}

/// <summary>
/// Types of property inputs.
/// </summary>
public enum PropertyType
{
    Text,
    Number,
    Select,
    Checkbox,
    TextArea,
    Code,
    TypedInput,  // Node-RED's TypedInput widget
    Info,        // Static information text
    Button,      // Action button
    Color,
    Password,
    ConfigNode   // Reference to a config node
}

/// <summary>
/// Credential definition for secure properties.
/// </summary>
public class NodeCredentialDefinition
{
    public required string Name { get; init; }
    public CredentialType Type { get; init; } = CredentialType.Text;
}

public enum CredentialType
{
    Text,
    Password
}

/// <summary>
/// Help text for a node, following Node-RED help format.
/// </summary>
public class NodeHelpText
{
    /// <summary>
    /// Short introduction (first paragraph, used as palette tooltip).
    /// </summary>
    public string Summary { get; init; } = "";

    /// <summary>
    /// Description of inputs section.
    /// </summary>
    public List<HelpProperty> Inputs { get; init; } = new();

    /// <summary>
    /// Description of outputs section.
    /// </summary>
    public List<HelpProperty> Outputs { get; init; } = new();

    /// <summary>
    /// Detailed usage information.
    /// </summary>
    public string Details { get; init; } = "";

    /// <summary>
    /// References to related documentation.
    /// </summary>
    public List<HelpReference> References { get; init; } = new();
}

public class HelpProperty
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string Description { get; init; } = "";
}

public class HelpReference
{
    public string Title { get; init; } = "";
    public string Url { get; init; } = "";
}

/// <summary>
/// Button definition for nodes with buttons (like Inject).
/// </summary>
public class NodeButtonDefinition
{
    /// <summary>
    /// Whether the button is a toggle.
    /// </summary>
    public bool Toggle { get; init; } = false;

    /// <summary>
    /// Icon for the button.
    /// </summary>
    public string Icon { get; init; } = "fa fa-play";

    /// <summary>
    /// Action identifier for the button click.
    /// </summary>
    public string Action { get; init; } = "inject";
}
