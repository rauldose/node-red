// Translated from: packages/node_modules/@node-red/editor-client/src/js/ui/state.js
// State management for UI components

namespace NodeRed.Editor.Services;

/// <summary>
/// UI state management - translated from ui/state.js
/// Manages the current state of the editor UI, including selection, focus, and mode
/// </summary>
public class EditorUIState
{
    // State constants
    public const int StateDefault = 0;
    public const int StateLinking = 1;
    public const int StateImport = 2;
    public const int StateExport = 3;
    public const int StateQuickJoin = 4;
    public const int StateMoving = 5;
    public const int StateMovingActive = 6;
    public const int StateEditingWire = 7;

    private int _state = StateDefault;
    private bool _dirty = false;
    private string? _focusedNode = null;
    private List<string> _selectedLinks = new();
    private bool _selectionLocked = false;
    
    public int State => _state;
    public bool IsDirty => _dirty;
    public string? FocusedNode => _focusedNode;
    public IReadOnlyList<string> SelectedLinks => _selectedLinks;
    public bool SelectionLocked => _selectionLocked;

    /// <summary>
    /// Set the current UI state
    /// </summary>
    public void SetState(int state)
    {
        _state = state;
    }

    /// <summary>
    /// Reset to default state
    /// </summary>
    public void ResetState()
    {
        _state = StateDefault;
    }

    /// <summary>
    /// Mark the editor as dirty (unsaved changes)
    /// </summary>
    public void MarkDirty()
    {
        _dirty = true;
    }

    /// <summary>
    /// Clear the dirty flag
    /// </summary>
    public void ClearDirty()
    {
        _dirty = false;
    }

    /// <summary>
    /// Set the focused node
    /// </summary>
    public void SetFocusedNode(string? nodeId)
    {
        _focusedNode = nodeId;
    }

    /// <summary>
    /// Add a link to selection
    /// </summary>
    public void AddSelectedLink(string linkId)
    {
        if (!_selectedLinks.Contains(linkId))
        {
            _selectedLinks.Add(linkId);
        }
    }

    /// <summary>
    /// Clear link selection
    /// </summary>
    public void ClearSelectedLinks()
    {
        _selectedLinks.Clear();
    }

    /// <summary>
    /// Lock the current selection
    /// </summary>
    public void LockSelection()
    {
        _selectionLocked = true;
    }

    /// <summary>
    /// Unlock the selection
    /// </summary>
    public void UnlockSelection()
    {
        _selectionLocked = false;
    }

    /// <summary>
    /// Check if we're in a linking state
    /// </summary>
    public bool IsLinking => _state == StateLinking;

    /// <summary>
    /// Check if we're moving nodes
    /// </summary>
    public bool IsMoving => _state == StateMoving || _state == StateMovingActive;

    /// <summary>
    /// Check if we're editing a wire
    /// </summary>
    public bool IsEditingWire => _state == StateEditingWire;
}
