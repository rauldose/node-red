// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/history.js
// ============================================================
// TRANSLATION: JavaScript history module to C# service
// ============================================================

namespace NodeRed.Editor.Services;

/// <summary>
/// Undo/redo history management service.
/// Translated from RED.history module.
/// </summary>
public class History
{
    private readonly EditorState _state;
    private readonly Stack<HistoryEvent> _undoStack = new();
    private readonly Stack<HistoryEvent> _redoStack = new();
    private const int MaxHistorySize = 100;
    private bool _recording = true;

    public event EventHandler? HistoryChanged;

    public History(EditorState state)
    {
        _state = state;
    }

    /// <summary>
    /// Push an event to the undo stack.
    /// Translated from push() in history.js
    /// </summary>
    public void Push(HistoryEvent ev)
    {
        if (!_recording) return;

        _undoStack.Push(ev);
        _redoStack.Clear();

        // Limit stack size
        while (_undoStack.Count > MaxHistorySize)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            foreach (var item in items.Take(MaxHistorySize))
            {
                _undoStack.Push(item);
            }
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Undo the last event.
    /// Translated from pop() in history.js
    /// </summary>
    public HistoryEvent? Undo()
    {
        if (_undoStack.Count == 0) return null;

        var ev = _undoStack.Pop();
        _redoStack.Push(ev);

        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return ev;
    }

    /// <summary>
    /// Redo the last undone event.
    /// Translated from redo() in history.js
    /// </summary>
    public HistoryEvent? Redo()
    {
        if (_redoStack.Count == 0) return null;

        var ev = _redoStack.Pop();
        _undoStack.Push(ev);

        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return ev;
    }

    /// <summary>
    /// Check if undo is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Check if redo is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Get undo stack depth.
    /// Translated from depth() in history.js
    /// </summary>
    public int Depth => _undoStack.Count;

    /// <summary>
    /// Clear all history.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Pause recording.
    /// </summary>
    public void PauseRecording() => _recording = false;

    /// <summary>
    /// Resume recording.
    /// </summary>
    public void ResumeRecording() => _recording = true;
}

/// <summary>
/// Represents a history event for undo/redo.
/// </summary>
public class HistoryEvent
{
    public HistoryEventType Type { get; set; }
    public FlowNode? Node { get; set; }
    public List<HistoryEvent>? Events { get; set; }
    public List<NodeMoveData>? Nodes { get; set; }
    public Dictionary<string, object?>? Changes { get; set; }
    public NodeLink? Link { get; set; }
    public List<NodeLink>? Links { get; set; }
    public NodeGroup? Group { get; set; }
    public string? GroupId { get; set; }
    public List<string>? NodeIds { get; set; }
}

/// <summary>
/// Data for node move operations.
/// </summary>
public class NodeMoveData
{
    public string NodeId { get; set; } = "";
    public double OldX { get; set; }
    public double OldY { get; set; }
    public double NewX { get; set; }
    public double NewY { get; set; }
}

/// <summary>
/// Types of history events.
/// </summary>
public enum HistoryEventType
{
    Multi,
    Add,
    Delete,
    Move,
    Edit,
    CreateLink,
    DeleteLink,
    CreateGroup,
    DeleteGroup,
    AddToGroup,
    RemoveFromGroup,
    CreateSubflow,
    DeleteSubflow,
    EditSubflow
}
