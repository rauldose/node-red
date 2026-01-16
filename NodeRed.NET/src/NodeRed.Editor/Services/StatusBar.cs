// Source: @node-red/editor-client/src/js/ui/statusBar.js
// Translated to C# for NodeRed.NET
namespace NodeRed.Editor.Services;

/// <summary>
/// Status bar at the bottom of the editor.
/// Translated from RED.statusBar module.
/// </summary>
public class StatusBar
{
    private readonly EditorState _state;
    private readonly Dictionary<string, StatusBarItem> _items = new();
    
    public event Action? OnStatusChanged;
    
    public StatusBar(EditorState state)
    {
        _state = state;
    }
    
    /// <summary>
    /// Add a status bar item.
    /// Translated from: statusBar.add = function(options)
    /// </summary>
    public void Add(string id, StatusBarItem item)
    {
        _items[id] = item;
        OnStatusChanged?.Invoke();
    }
    
    /// <summary>
    /// Remove a status bar item.
    /// </summary>
    public void Remove(string id)
    {
        _items.Remove(id);
        OnStatusChanged?.Invoke();
    }
    
    /// <summary>
    /// Update a status bar item.
    /// </summary>
    public void Update(string id, Action<StatusBarItem> updateAction)
    {
        if (_items.TryGetValue(id, out var item))
        {
            updateAction(item);
            OnStatusChanged?.Invoke();
        }
    }
    
    /// <summary>
    /// Get all status bar items.
    /// </summary>
    public IEnumerable<StatusBarItem> GetItems()
    {
        return _items.Values.OrderBy(i => i.Order);
    }
}

public class StatusBarItem
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public string? Icon { get; set; }
    public string? Tooltip { get; set; }
    public int Order { get; set; } = 100;
    public bool Visible { get; set; } = true;
    public Action? OnClick { get; set; }
}
