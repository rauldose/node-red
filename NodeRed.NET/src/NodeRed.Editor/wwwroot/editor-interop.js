// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/ui/sidebar.js
// Translated from: setupSidebarSeparator() draggable functionality
// ============================================================
// This file provides JavaScript interop for panel resize functionality
// that requires global mouse event tracking.
// ============================================================

// Active resize state
let activeResize = null;

/**
 * Start tracking mouse movement for panel resize
 * Called from Blazor when user starts dragging a resize handle
 * @param {DotNetObjectReference} dotNetRef - Reference to the Blazor component
 * @param {string} panel - Panel identifier ('sidebar' or 'palette')
 */
window.startPanelResize = function(dotNetRef, panel) {
    activeResize = {
        dotNetRef: dotNetRef,
        panel: panel
    };

    // Add global event listeners
    document.addEventListener('mousemove', onResizeMove);
    document.addEventListener('mouseup', onResizeEnd);
    
    // Prevent text selection during drag
    document.body.style.userSelect = 'none';
    document.body.style.cursor = 'col-resize';
};

/**
 * Handle mouse move during resize
 * @param {MouseEvent} e 
 */
function onResizeMove(e) {
    if (activeResize && activeResize.dotNetRef) {
        activeResize.dotNetRef.invokeMethodAsync('OnPanelResizeMove', activeResize.panel, e.clientX);
    }
}

/**
 * Handle mouse up (end of resize)
 * @param {MouseEvent} e 
 */
function onResizeEnd(e) {
    if (activeResize && activeResize.dotNetRef) {
        activeResize.dotNetRef.invokeMethodAsync('OnPanelResizeEnd', activeResize.panel);
    }
    
    // Remove global event listeners
    document.removeEventListener('mousemove', onResizeMove);
    document.removeEventListener('mouseup', onResizeEnd);
    
    // Restore normal cursor and selection
    document.body.style.userSelect = '';
    document.body.style.cursor = '';
    
    activeResize = null;
}

/**
 * Stop any active resize operation
 * Called when component is disposed
 */
window.stopPanelResize = function() {
    if (activeResize) {
        document.removeEventListener('mousemove', onResizeMove);
        document.removeEventListener('mouseup', onResizeEnd);
        document.body.style.userSelect = '';
        document.body.style.cursor = '';
        activeResize = null;
    }
};
