// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Core.Enums;

/// <summary>
/// Log levels for node messages.
/// </summary>
public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}

/// <summary>
/// Shapes for the node status indicator.
/// </summary>
public enum StatusShape
{
    Ring,
    Dot
}

/// <summary>
/// Colors for the node status indicator.
/// </summary>
public enum StatusColor
{
    Red,
    Green,
    Yellow,
    Blue,
    Grey
}

/// <summary>
/// Node categories for palette organization.
/// </summary>
public enum NodeCategory
{
    Common,
    Function,
    Network,
    Sequence,
    Parser,
    Storage,
    Config  // Configuration nodes (not shown in palette)
}

/// <summary>
/// Flow execution state.
/// </summary>
public enum FlowState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Error
}

/// <summary>
/// Property value types (matching Node-RED's typedInput).
/// </summary>
public enum PropertyValueType
{
    String,
    Number,
    Boolean,
    Json,
    Date,
    Buffer,
    Message,
    Flow,
    Global,
    Environment,
    JsonPath,
    Expression
}
