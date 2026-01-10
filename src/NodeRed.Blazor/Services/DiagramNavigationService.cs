// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Syncfusion.Blazor.Diagram;

namespace NodeRed.Blazor.Services;

/// <summary>
/// Service for diagram navigation and view operations.
/// Handles pan, zoom, and reveal-node functionality.
/// </summary>
public interface IDiagramNavigationService
{
    /// <summary>
    /// Pans and zooms the diagram to reveal a specific node.
    /// </summary>
    /// <param name="diagram">The diagram component.</param>
    /// <param name="node">The node to reveal.</param>
    void RevealNode(SfDiagramComponent diagram, Node node);

    /// <summary>
    /// Centers the diagram view on a specific point.
    /// </summary>
    /// <param name="diagram">The diagram component.</param>
    /// <param name="x">X coordinate to center on.</param>
    /// <param name="y">Y coordinate to center on.</param>
    void CenterOnPoint(SfDiagramComponent diagram, double x, double y);

    /// <summary>
    /// Fits the diagram to show all content.
    /// </summary>
    /// <param name="diagram">The diagram component.</param>
    void FitToPage(SfDiagramComponent diagram);
}

/// <summary>
/// Implementation of diagram navigation service.
/// </summary>
public class DiagramNavigationService : IDiagramNavigationService
{
    /// <inheritdoc/>
    public void RevealNode(SfDiagramComponent diagram, Node node)
    {
        if (diagram == null || node == null)
            return;

        try
        {
            // Calculate the node's center position
            var nodeX = node.OffsetX;
            var nodeY = node.OffsetY;

            // Get the current viewport dimensions
            var scrollSettings = diagram.ScrollSettings;
            var currentZoom = scrollSettings?.CurrentZoom ?? 1.0;

            // Use Pan to bring the node into view
            // Calculate the offset needed to center the node
            var currentScrollX = scrollSettings?.HorizontalOffset ?? 0;
            var currentScrollY = scrollSettings?.VerticalOffset ?? 0;
            
            // Pan to the node position
            diagram.Pan(nodeX - currentScrollX - 400, nodeY - currentScrollY - 300);

            // Optionally zoom in slightly to focus on the node
            if (currentZoom < 0.8)
            {
                diagram.Zoom(0.8 / currentZoom, null);
            }
        }
        catch (Exception)
        {
            // Ignore pan/zoom errors
        }
    }

    /// <inheritdoc/>
    public void CenterOnPoint(SfDiagramComponent diagram, double x, double y)
    {
        if (diagram == null)
            return;

        try
        {
            var scrollSettings = diagram.ScrollSettings;
            var currentScrollX = scrollSettings?.HorizontalOffset ?? 0;
            var currentScrollY = scrollSettings?.VerticalOffset ?? 0;
            
            diagram.Pan(x - currentScrollX - 400, y - currentScrollY - 300);
        }
        catch (Exception)
        {
            // Ignore pan errors
        }
    }

    /// <inheritdoc/>
    public void FitToPage(SfDiagramComponent diagram)
    {
        if (diagram == null)
            return;

        try
        {
            // Reset zoom to 100%
            var currentZoom = diagram.ScrollSettings?.CurrentZoom ?? 1.0;
            if (Math.Abs(currentZoom - 1.0) > 0.01)
            {
                diagram.Zoom(1.0 / currentZoom, null);
            }
        }
        catch (Exception)
        {
            // Ignore errors
        }
    }
}

