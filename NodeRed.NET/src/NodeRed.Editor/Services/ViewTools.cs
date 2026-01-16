// Source: @node-red/editor-client/src/js/ui/view-tools.js + view-annotations.js
// Translated to C# for NodeRed.NET
namespace NodeRed.Editor.Services;

/// <summary>
/// View tools for canvas manipulation.
/// Translated from RED.view.tools module.
/// </summary>
public class ViewTools
{
    private readonly EditorState _state;
    
    public bool GridVisible { get; set; } = true;
    public bool SnapToGrid { get; set; } = true;
    public int GridSize { get; set; } = 20;
    public bool ShowNodeStatus { get; set; } = true;
    public bool ShowLinkLabels { get; set; } = false;
    
    public event Action? OnToolsChanged;
    
    public ViewTools(EditorState state)
    {
        _state = state;
    }
    
    /// <summary>
    /// Toggle grid visibility.
    /// </summary>
    public void ToggleGrid()
    {
        GridVisible = !GridVisible;
        OnToolsChanged?.Invoke();
    }
    
    /// <summary>
    /// Toggle snap to grid.
    /// </summary>
    public void ToggleSnapToGrid()
    {
        SnapToGrid = !SnapToGrid;
        OnToolsChanged?.Invoke();
    }
    
    /// <summary>
    /// Set grid size.
    /// </summary>
    public void SetGridSize(int size)
    {
        GridSize = Math.Max(5, Math.Min(50, size));
        OnToolsChanged?.Invoke();
    }
    
    /// <summary>
    /// Toggle node status display.
    /// </summary>
    public void ToggleNodeStatus()
    {
        ShowNodeStatus = !ShowNodeStatus;
        OnToolsChanged?.Invoke();
    }
    
    /// <summary>
    /// Toggle link labels display.
    /// </summary>
    public void ToggleLinkLabels()
    {
        ShowLinkLabels = !ShowLinkLabels;
        OnToolsChanged?.Invoke();
    }
    
    /// <summary>
    /// Snap a position to the grid.
    /// </summary>
    public (double x, double y) SnapToGridPosition(double x, double y)
    {
        if (!SnapToGrid) return (x, y);
        
        return (
            Math.Round(x / GridSize) * GridSize,
            Math.Round(y / GridSize) * GridSize
        );
    }
    
    /// <summary>
    /// Align selected nodes to grid.
    /// Translated from: view.alignSelectionToGrid = function()
    /// </summary>
    public void AlignSelectionToGrid()
    {
        // TODO: Implement when selection API is available
    }
    
    /// <summary>
    /// Distribute selected nodes horizontally.
    /// Translated from: view.distributeSelection = function(direction)
    /// </summary>
    public void DistributeSelectionHorizontally()
    {
        // TODO: Implement when selection API is available
    }
    
    /// <summary>
    /// Distribute selected nodes vertically.
    /// </summary>
    public void DistributeSelectionVertically()
    {
        // TODO: Implement when selection API is available
    }
}

/// <summary>
/// Annotations on canvas for highlighting and commenting.
/// Translated from RED.view.annotations module.
/// </summary>
public class ViewAnnotations
{
    private readonly List<Annotation> _annotations = new();
    
    public event Action? OnAnnotationsChanged;
    
    /// <summary>
    /// Add an annotation.
    /// Translated from: annotations.add = function(annotation)
    /// </summary>
    public Annotation Add(AnnotationType type, double x, double y, string text, string? color = null)
    {
        var annotation = new Annotation
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            X = x,
            Y = y,
            Text = text,
            Color = color ?? "#ffd700"
        };
        
        _annotations.Add(annotation);
        OnAnnotationsChanged?.Invoke();
        
        return annotation;
    }
    
    /// <summary>
    /// Remove an annotation.
    /// </summary>
    public void Remove(string id)
    {
        _annotations.RemoveAll(a => a.Id == id);
        OnAnnotationsChanged?.Invoke();
    }
    
    /// <summary>
    /// Get all annotations.
    /// </summary>
    public IEnumerable<Annotation> GetAll() => _annotations;
    
    /// <summary>
    /// Get annotations for a specific flow.
    /// </summary>
    public IEnumerable<Annotation> GetForFlow(string flowId)
    {
        return _annotations.Where(a => a.FlowId == flowId);
    }
    
    /// <summary>
    /// Clear all annotations.
    /// </summary>
    public void Clear()
    {
        _annotations.Clear();
        OnAnnotationsChanged?.Invoke();
    }
    
    /// <summary>
    /// Update annotation position.
    /// </summary>
    public void Move(string id, double x, double y)
    {
        var annotation = _annotations.FirstOrDefault(a => a.Id == id);
        if (annotation != null)
        {
            annotation.X = x;
            annotation.Y = y;
            OnAnnotationsChanged?.Invoke();
        }
    }
    
    /// <summary>
    /// Update annotation text.
    /// </summary>
    public void UpdateText(string id, string text)
    {
        var annotation = _annotations.FirstOrDefault(a => a.Id == id);
        if (annotation != null)
        {
            annotation.Text = text;
            OnAnnotationsChanged?.Invoke();
        }
    }
}

public class Annotation
{
    public string Id { get; set; } = "";
    public AnnotationType Type { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 200;
    public double Height { get; set; } = 100;
    public string Text { get; set; } = "";
    public string Color { get; set; } = "#ffd700";
    public string? FlowId { get; set; }
}

public enum AnnotationType
{
    Note,
    Highlight,
    Arrow,
    Rectangle,
    Ellipse
}
