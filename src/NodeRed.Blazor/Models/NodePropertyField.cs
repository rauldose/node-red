// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;

namespace NodeRed.Blazor.Models;

/// <summary>
/// Defines the schema for a node property field in the UI.
/// </summary>
public class NodePropertyField
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public string Icon { get; set; } = "";
    public PropertyFieldType Type { get; set; } = PropertyFieldType.Text;
    public string DefaultValue { get; set; } = "";
    public string Placeholder { get; set; } = "";
    public string Prefix { get; set; } = "";
    public string Suffix { get; set; } = "";
    public List<PropertyOption>? Options { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Step { get; set; }
    public int? Rows { get; set; }
    public string? ShowWhen { get; set; }
    public string? HideWhen { get; set; }
    public bool IsSmall { get; set; } = false;
    public bool IsFullWidth { get; set; } = false;

    /// <summary>
    /// Converts SDK NodePropertyDefinition to UI NodePropertyField.
    /// </summary>
    public static NodePropertyField FromDefinition(NodePropertyDefinition def)
    {
        return new NodePropertyField
        {
            Name = def.Name,
            Label = def.Label,
            Icon = def.Icon,
            Type = ConvertPropertyType(def.Type),
            DefaultValue = def.DefaultValue?.ToString() ?? "",
            Placeholder = def.Placeholder,
            Prefix = def.Prefix,
            Suffix = def.Suffix,
            Options = def.Options?.Select(o => new PropertyOption { Value = o.Value, Label = o.Label }).ToList(),
            Min = def.Min,
            Max = def.Max,
            Step = def.Step,
            Rows = def.Rows,
            ShowWhen = def.ShowWhen,
            HideWhen = def.HideWhen,
            IsSmall = def.IsSmall,
            IsFullWidth = def.IsFullWidth
        };
    }

    /// <summary>
    /// Converts SDK PropertyType to UI PropertyFieldType.
    /// </summary>
    private static PropertyFieldType ConvertPropertyType(PropertyType type)
    {
        return type switch
        {
            PropertyType.Text => PropertyFieldType.Text,
            PropertyType.Number => PropertyFieldType.Number,
            PropertyType.Select => PropertyFieldType.Select,
            PropertyType.Checkbox => PropertyFieldType.Checkbox,
            PropertyType.TextArea => PropertyFieldType.TextArea,
            PropertyType.Code => PropertyFieldType.Code,
            PropertyType.Info => PropertyFieldType.Info,
            PropertyType.Button => PropertyFieldType.Button,
            _ => PropertyFieldType.Text
        };
    }
}

public class PropertyOption
{
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
}

public enum PropertyFieldType
{
    Text,
    Number,
    Select,
    Checkbox,
    TextArea,
    Code,
    Info,
    Button
}
