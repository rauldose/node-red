// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Core.Entities;

/// <summary>
/// Represents an active multiplayer editing session.
/// </summary>
public class MultiplayerSession
{
    /// <summary>
    /// Unique session identifier.
    /// </summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The user associated with this session.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Whether the session is currently active.
    /// </summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// Current location of the user in the editor.
    /// </summary>
    public EditorLocation? Location { get; set; }

    /// <summary>
    /// When the session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the session was last active.
    /// </summary>
    public DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Communication session ID (for SignalR connection mapping).
    /// </summary>
    public string? CommsSessionId { get; set; }
}

/// <summary>
/// Represents the current location of a user in the editor.
/// </summary>
public class EditorLocation
{
    /// <summary>
    /// The workspace/flow being viewed.
    /// </summary>
    public string? Workspace { get; set; }

    /// <summary>
    /// The currently selected node.
    /// </summary>
    public string? Node { get; set; }

    /// <summary>
    /// Cursor position in the flow editor.
    /// </summary>
    public CursorPosition? Cursor { get; set; }
}

/// <summary>
/// Represents a cursor position in the flow editor.
/// </summary>
public class CursorPosition
{
    /// <summary>
    /// X coordinate.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y coordinate.
    /// </summary>
    public double Y { get; set; }
}

/// <summary>
/// Multiplayer event types.
/// </summary>
public enum MultiplayerEventType
{
    /// <summary>
    /// A new session connected.
    /// </summary>
    ConnectionAdded,

    /// <summary>
    /// A session disconnected.
    /// </summary>
    ConnectionRemoved,

    /// <summary>
    /// A session's location changed.
    /// </summary>
    LocationUpdated,

    /// <summary>
    /// Initial session list for new connections.
    /// </summary>
    Init
}

/// <summary>
/// A multiplayer event message.
/// </summary>
public class MultiplayerEvent
{
    /// <summary>
    /// The type of event.
    /// </summary>
    public MultiplayerEventType Type { get; set; }

    /// <summary>
    /// The session that triggered the event.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Event data (varies by type).
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// When the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Connection added event data.
/// </summary>
public class ConnectionAddedData
{
    /// <summary>
    /// The session that connected.
    /// </summary>
    public MultiplayerSession Session { get; set; } = null!;
}

/// <summary>
/// Connection removed event data.
/// </summary>
public class ConnectionRemovedData
{
    /// <summary>
    /// The session ID that disconnected.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the user explicitly disconnected.
    /// </summary>
    public bool Disconnected { get; set; }
}

/// <summary>
/// Location update event data.
/// </summary>
public class LocationUpdateData
{
    /// <summary>
    /// The session ID.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// The workspace being viewed.
    /// </summary>
    public string? Workspace { get; set; }

    /// <summary>
    /// The selected node.
    /// </summary>
    public string? Node { get; set; }

    /// <summary>
    /// Cursor position.
    /// </summary>
    public CursorPosition? Cursor { get; set; }
}

/// <summary>
/// Init event data for new connections.
/// </summary>
public class InitData
{
    /// <summary>
    /// List of all active sessions.
    /// </summary>
    public List<MultiplayerSession> Sessions { get; set; } = new();
}
