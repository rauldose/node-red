// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;

namespace NodeRed.SDK;

/// <summary>
/// Fluent builder for creating node property definitions.
/// 
/// Example:
/// <code>
/// protected override List&lt;NodePropertyDefinition&gt; DefineProperties() =>
///     PropertyBuilder.Create()
///         .AddText("topic", "Topic", icon: "fa fa-tag")
///         .AddSelect("payloadType", "Payload Type", new[] {
///             ("str", "String"),
///             ("num", "Number"),
///             ("json", "JSON")
///         })
///         .AddNumber("timeout", "Timeout", min: 0, suffix: "seconds")
///         .AddCheckbox("enabled", "Enabled", defaultValue: true)
///         .AddCode("func", "Function", rows: 10)
///         .Build();
/// </code>
/// </summary>
public class PropertyBuilder
{
    private readonly List<NodePropertyDefinition> _properties = new();

    public static PropertyBuilder Create() => new();

    /// <summary>
    /// Adds a text input property.
    /// </summary>
    public PropertyBuilder AddText(
        string name,
        string label,
        string? icon = null,
        string? defaultValue = null,
        string? placeholder = null,
        string? prefix = null,
        string? suffix = null,
        bool required = false,
        bool isSmall = false,
        string? showWhen = null,
        string? hideWhen = null)
    {
        _properties.Add(new NodePropertyDefinition
        {
            Name = name,
            Label = label,
            Icon = icon ?? "",
            Type = PropertyType.Text,
            DefaultValue = defaultValue,
            Placeholder = placeholder ?? "",
            Prefix = prefix ?? "",
            Suffix = suffix ?? "",
            Required = required,
            IsSmall = isSmall,
            ShowWhen = showWhen,
            HideWhen = hideWhen
        });
        return this;
    }

    /// <summary>
    /// Adds a number input property.
    /// </summary>
    public PropertyBuilder AddNumber(
        string name,
        string label,
        string? icon = null,
        double? defaultValue = null,
        double? min = null,
        double? max = null,
        double? step = null,
        string? prefix = null,
        string? suffix = null,
        bool required = false,
        bool isSmall = false,
        string? showWhen = null,
        string? hideWhen = null)
    {
        _properties.Add(new NodePropertyDefinition
        {
            Name = name,
            Label = label,
            Icon = icon ?? "",
            Type = PropertyType.Number,
            DefaultValue = defaultValue,
            Min = min,
            Max = max,
            Step = step,
            Prefix = prefix ?? "",
            Suffix = suffix ?? "",
            Required = required,
            IsSmall = isSmall,
            ShowWhen = showWhen,
            HideWhen = hideWhen
        });
        return this;
    }

    /// <summary>
    /// Adds a select/dropdown property.
    /// </summary>
    public PropertyBuilder AddSelect(
        string name,
        string label,
        IEnumerable<(string value, string label)> options,
        string? icon = null,
        string? defaultValue = null,
        bool required = false,
        string? showWhen = null,
        string? hideWhen = null)
    {
        _properties.Add(new NodePropertyDefinition
        {
            Name = name,
            Label = label,
            Icon = icon ?? "",
            Type = PropertyType.Select,
            DefaultValue = defaultValue,
            Options = options.Select(o => new PropertyOptionDefinition { Value = o.value, Label = o.label }).ToList(),
            Required = required,
            ShowWhen = showWhen,
            HideWhen = hideWhen
        });
        return this;
    }

    /// <summary>
    /// Adds a checkbox property.
    /// </summary>
    public PropertyBuilder AddCheckbox(
        string name,
        string label,
        bool defaultValue = false,
        string? suffix = null,
        string? showWhen = null,
        string? hideWhen = null)
    {
        _properties.Add(new NodePropertyDefinition
        {
            Name = name,
            Label = label,
            Type = PropertyType.Checkbox,
            DefaultValue = defaultValue,
            Suffix = suffix ?? "",
            ShowWhen = showWhen,
            HideWhen = hideWhen
        });
        return this;
    }

    /// <summary>
    /// Adds a textarea property.
    /// </summary>
    public PropertyBuilder AddTextArea(
        string name,
        string label,
        string? defaultValue = null,
        string? placeholder = null,
        int rows = 5,
        bool isFullWidth = true,
        string? showWhen = null,
        string? hideWhen = null)
    {
        _properties.Add(new NodePropertyDefinition
        {
            Name = name,
            Label = label,
            Type = PropertyType.TextArea,
            DefaultValue = defaultValue,
            Placeholder = placeholder ?? "",
            Rows = rows,
            IsFullWidth = isFullWidth,
            ShowWhen = showWhen,
            HideWhen = hideWhen
        });
        return this;
    }

    /// <summary>
    /// Adds a code editor property.
    /// </summary>
    public PropertyBuilder AddCode(
        string name,
        string label,
        string? defaultValue = null,
        int rows = 10,
        string? showWhen = null,
        string? hideWhen = null)
    {
        _properties.Add(new NodePropertyDefinition
        {
            Name = name,
            Label = label,
            Type = PropertyType.Code,
            DefaultValue = defaultValue,
            Rows = rows,
            IsFullWidth = true,
            ShowWhen = showWhen,
            HideWhen = hideWhen
        });
        return this;
    }

    /// <summary>
    /// Adds a TypedInput property (like Node-RED's TypedInput widget).
    /// </summary>
    public PropertyBuilder AddTypedInput(
        string name,
        string label,
        IEnumerable<(string value, string label)> types,
        string? icon = null,
        string? defaultType = null,
        string? defaultValue = null,
        bool required = false,
        string? showWhen = null,
        string? hideWhen = null)
    {
        _properties.Add(new NodePropertyDefinition
        {
            Name = name,
            Label = label,
            Icon = icon ?? "",
            Type = PropertyType.TypedInput,
            DefaultValue = defaultValue,
            Options = types.Select(t => new PropertyOptionDefinition { Value = t.value, Label = t.label }).ToList(),
            Required = required,
            ShowWhen = showWhen,
            HideWhen = hideWhen
        });
        return this;
    }

    /// <summary>
    /// Adds a static info text (not editable).
    /// </summary>
    public PropertyBuilder AddInfo(string text)
    {
        _properties.Add(new NodePropertyDefinition
        {
            Name = $"_info_{_properties.Count}",
            Label = "",
            Type = PropertyType.Info,
            DefaultValue = text
        });
        return this;
    }

    /// <summary>
    /// Adds a config node reference property.
    /// </summary>
    public PropertyBuilder AddConfigNode(
        string name,
        string label,
        string configNodeType,
        bool required = false)
    {
        _properties.Add(new NodePropertyDefinition
        {
            Name = name,
            Label = label,
            Type = PropertyType.ConfigNode,
            ConfigNodeType = configNodeType,
            Required = required
        });
        return this;
    }

    /// <summary>
    /// Builds the list of property definitions.
    /// </summary>
    public List<NodePropertyDefinition> Build() => _properties;
}
