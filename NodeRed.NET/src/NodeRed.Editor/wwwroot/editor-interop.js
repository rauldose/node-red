// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/ui/sidebar.js
// Translated from: setupSidebarSeparator() draggable functionality
// ============================================================
// This file provides JavaScript interop for panel resize functionality
// that requires global mouse event tracking.
// ============================================================

// Active resize state
let activeResize = null;

// Dragged node type from palette
let draggedNodeType = null;

// Canvas interaction state
let canvasInteraction = null;

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

// ============================================================
// Drag and Drop support for palette nodes
// ============================================================

/**
 * Set the node type being dragged from palette
 * @param {string} nodeType - The type of node being dragged
 */
window.setDraggedNodeType = function(nodeType) {
    draggedNodeType = nodeType;
};

/**
 * Get the currently dragged node type
 * @returns {string|null} The node type or null
 */
window.getDraggedNodeType = function() {
    return draggedNodeType;
};

/**
 * Clear the dragged node type
 */
window.clearDraggedNodeType = function() {
    draggedNodeType = null;
};

// ============================================================
// Canvas Node/Wire Interaction support
// ============================================================

/**
 * Start tracking global mouse events for canvas interactions (node move, wire drag)
 * @param {DotNetObjectReference} dotNetRef - Reference to the Workspaces component
 * @param {string} mode - Interaction mode ('moving', 'wiring', 'lasso')
 */
window.startCanvasInteraction = function(dotNetRef, mode) {
    canvasInteraction = {
        dotNetRef: dotNetRef,
        mode: mode
    };
    
    document.addEventListener('mousemove', onCanvasMove);
    document.addEventListener('mouseup', onCanvasUp);
    
    // Prevent text selection
    document.body.style.userSelect = 'none';
    if (mode === 'moving') {
        document.body.style.cursor = 'move';
    } else if (mode === 'wiring') {
        document.body.style.cursor = 'crosshair';
    }
};

function onCanvasMove(e) {
    if (canvasInteraction && canvasInteraction.dotNetRef) {
        canvasInteraction.dotNetRef.invokeMethodAsync('OnGlobalMouseMove', e.clientX, e.clientY, e.movementX, e.movementY);
    }
}

function onCanvasUp(e) {
    if (canvasInteraction && canvasInteraction.dotNetRef) {
        canvasInteraction.dotNetRef.invokeMethodAsync('OnGlobalMouseUp', e.clientX, e.clientY);
    }
    
    document.removeEventListener('mousemove', onCanvasMove);
    document.removeEventListener('mouseup', onCanvasUp);
    document.body.style.userSelect = '';
    document.body.style.cursor = '';
    canvasInteraction = null;
}

window.stopCanvasInteraction = function() {
    if (canvasInteraction) {
        document.removeEventListener('mousemove', onCanvasMove);
        document.removeEventListener('mouseup', onCanvasUp);
        document.body.style.userSelect = '';
        document.body.style.cursor = '';
        canvasInteraction = null;
    }
};

// ============================================================
// Context Menu support
// ============================================================

/**
 * Show a context menu at specified position
 * @param {DotNetObjectReference} dotNetRef - Reference to the Blazor component
 * @param {number} x - X position
 * @param {number} y - Y position
 */
window.showContextMenu = function(dotNetRef, x, y) {
    // Close any existing context menu first
    const existingMenu = document.querySelector('.red-ui-context-menu:not(.hide)');
    if (existingMenu) {
        existingMenu.classList.add('hide');
    }
    
    // Add click handler to close menu when clicking outside
    const closeHandler = function(e) {
        if (!e.target.closest('.red-ui-context-menu')) {
            dotNetRef.invokeMethodAsync('Hide');
            document.removeEventListener('click', closeHandler);
        }
    };
    
    setTimeout(() => {
        document.addEventListener('click', closeHandler);
    }, 10);
};

/**
 * Hide any visible context menu
 */
window.hideContextMenu = function() {
    const menu = document.querySelector('.red-ui-context-menu');
    if (menu) {
        menu.classList.add('hide');
    }
};

// ============================================================
// Hamburger Menu support
// ============================================================

let menuDotNetRef = null;

/**
 * Initialize the main menu
 * @param {DotNetObjectReference} dotNetRef - Reference to the menu component
 */
window.initMainMenu = function(dotNetRef) {
    menuDotNetRef = dotNetRef;
};

/**
 * Toggle the main menu visibility
 */
window.toggleMainMenu = function() {
    if (menuDotNetRef) {
        menuDotNetRef.invokeMethodAsync('Toggle');
    }
};
