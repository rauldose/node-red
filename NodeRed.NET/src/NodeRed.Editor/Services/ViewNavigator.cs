// Source: @node-red/editor-client/src/js/ui/view-navigator.js
// Translated to C# for NodeRed.NET
namespace NodeRed.Editor.Services;

/// <summary>
/// Mini-map navigator for canvas navigation.
/// Translated from RED.view.navigator module.
/// </summary>
public class ViewNavigator
{
    private readonly EditorState _state;
    
    public bool IsVisible { get; set; } = false;
    public double Scale { get; set; } = 0.1;
    public double ViewportX { get; set; }
    public double ViewportY { get; set; }
    public double ViewportWidth { get; set; }
    public double ViewportHeight { get; set; }
    
    public event Action? OnViewportChanged;
    
    public ViewNavigator(EditorState state)
    {
        _state = state;
    }
    
    /// <summary>
    /// Toggle navigator visibility.
    /// Translated from: navigator.toggle = function()
    /// </summary>
    public void Toggle()
    {
        IsVisible = !IsVisible;
        OnViewportChanged?.Invoke();
    }
    
    /// <summary>
    /// Show the navigator.
    /// </summary>
    public void Show()
    {
        IsVisible = true;
        OnViewportChanged?.Invoke();
    }
    
    /// <summary>
    /// Hide the navigator.
    /// </summary>
    public void Hide()
    {
        IsVisible = false;
        OnViewportChanged?.Invoke();
    }
    
    /// <summary>
    /// Update viewport based on current view.
    /// Translated from: navigator.refresh = function()
    /// </summary>
    public void Refresh()
    {
        // Default viewport when no nodes
        ViewportX = 0;
        ViewportY = 0;
        ViewportWidth = 1000;
        ViewportHeight = 600;
        
        OnViewportChanged?.Invoke();
    }
    
    /// <summary>
    /// Navigate to a specific position when clicking on navigator.
    /// Translated from: navigator.scrollTo = function(x, y)
    /// </summary>
    public void ScrollTo(double x, double y)
    {
        // Convert navigator coordinates to canvas coordinates
        var canvasX = ViewportX + (x / Scale);
        var canvasY = ViewportY + (y / Scale);
        
        // TODO: Implement scroll when API is available
    }
}
