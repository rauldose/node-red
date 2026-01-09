// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Enums;

namespace NodeRed.Core.Entities;

/// <summary>
/// Represents the status of a node in the editor.
/// </summary>
public class NodeStatus
{
    /// <summary>
    /// Status text to display.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Shape of the status indicator.
    /// </summary>
    public StatusShape Shape { get; set; } = StatusShape.Dot;

    /// <summary>
    /// Color of the status indicator.
    /// </summary>
    public StatusColor Color { get; set; } = StatusColor.Grey;

    /// <summary>
    /// Creates a cleared status.
    /// </summary>
    public static NodeStatus Clear() => new();

    /// <summary>
    /// Creates a success status.
    /// </summary>
    public static NodeStatus Success(string text) => new()
    {
        Text = text,
        Shape = StatusShape.Dot,
        Color = StatusColor.Green
    };

    /// <summary>
    /// Creates an error status.
    /// </summary>
    public static NodeStatus Error(string text) => new()
    {
        Text = text,
        Shape = StatusShape.Ring,
        Color = StatusColor.Red
    };

    /// <summary>
    /// Creates a processing status.
    /// </summary>
    public static NodeStatus Processing(string text) => new()
    {
        Text = text,
        Shape = StatusShape.Ring,
        Color = StatusColor.Blue
    };
}
