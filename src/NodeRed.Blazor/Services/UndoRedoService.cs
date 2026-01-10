// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Syncfusion.Blazor.Diagram;

namespace NodeRed.Blazor.Services;

/// <summary>
/// Action types for undo/redo operations
/// </summary>
public enum EditorActionType
{
    AddNode,
    DeleteNode,
    MoveNode,
    EditNode,
    AddConnector,
    DeleteConnector
}

/// <summary>
/// Represents an editor action for undo/redo
/// </summary>
public class EditorAction
{
    public EditorActionType Type { get; set; }
    public string? NodeId { get; set; }
    public Node? NodeData { get; set; }
    public double OldX { get; set; }
    public double OldY { get; set; }
    public double NewX { get; set; }
    public double NewY { get; set; }
    public Dictionary<string, object?>? OldProperties { get; set; }
    public Dictionary<string, object?>? NewProperties { get; set; }
    public string? ConnectorId { get; set; }
    public Connector? ConnectorData { get; set; }
}

/// <summary>
/// Service for managing undo/redo operations in the editor.
/// Matches the functionality of RED.history in the JS implementation.
/// </summary>
public interface IUndoRedoService
{
    /// <summary>
    /// Event raised when the undo/redo state changes
    /// </summary>
    event Action? OnChange;

    /// <summary>
    /// Whether there are actions that can be undone
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Whether there are actions that can be redone
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    /// Records an action for undo/redo
    /// </summary>
    void RecordAction(EditorAction action);

    /// <summary>
    /// Gets the next action to undo (does not remove it)
    /// </summary>
    EditorAction? PeekUndo();

    /// <summary>
    /// Gets the next action to redo (does not remove it)
    /// </summary>
    EditorAction? PeekRedo();

    /// <summary>
    /// Pops the next action to undo
    /// </summary>
    EditorAction? PopUndo();

    /// <summary>
    /// Pops the next action to redo
    /// </summary>
    EditorAction? PopRedo();

    /// <summary>
    /// Pushes an action to the redo stack
    /// </summary>
    void PushRedo(EditorAction action);

    /// <summary>
    /// Clears all undo/redo history
    /// </summary>
    void Clear();
}

/// <summary>
/// Implementation of the undo/redo service
/// </summary>
public class UndoRedoService : IUndoRedoService
{
    private readonly Stack<EditorAction> _undoStack = new();
    private readonly Stack<EditorAction> _redoStack = new();
    private const int MaxStackSize = 50;

    public event Action? OnChange;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void RecordAction(EditorAction action)
    {
        _undoStack.Push(action);
        _redoStack.Clear(); // Clear redo stack on new action
        
        // Limit stack size
        if (_undoStack.Count > MaxStackSize)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            foreach (var item in items.Take(MaxStackSize).Reverse())
            {
                _undoStack.Push(item);
            }
        }
        
        OnChange?.Invoke();
    }

    public EditorAction? PeekUndo()
    {
        return _undoStack.Count > 0 ? _undoStack.Peek() : null;
    }

    public EditorAction? PeekRedo()
    {
        return _redoStack.Count > 0 ? _redoStack.Peek() : null;
    }

    public EditorAction? PopUndo()
    {
        if (_undoStack.Count == 0) return null;
        var action = _undoStack.Pop();
        OnChange?.Invoke();
        return action;
    }

    public EditorAction? PopRedo()
    {
        if (_redoStack.Count == 0) return null;
        var action = _redoStack.Pop();
        OnChange?.Invoke();
        return action;
    }

    public void PushRedo(EditorAction action)
    {
        _redoStack.Push(action);
        OnChange?.Invoke();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        OnChange?.Invoke();
    }
}
