# Node-RED UI Comparison: JavaScript vs Blazor

This document provides a comprehensive comparison of the functionalities between the original JavaScript UI and the new Blazor UI implementation as part of the .NET port.

## Executive Summary

The Blazor UI port covers the core editor functionality but is missing several advanced features present in the original JavaScript implementation. This document categorizes features by priority and provides recommendations for achieving feature parity.

---

## Feature Comparison Table

### Legend
- ✅ **Implemented** - Feature is fully implemented in Blazor
- ⚠️ **Partial** - Feature is partially implemented or has limitations
- ❌ **Missing** - Feature is not yet implemented in Blazor

---

## 1. Core Editor Features

| Feature | JS UI | Blazor UI | Notes |
|---------|-------|-----------|-------|
| Node canvas (workspace) | ✅ | ✅ | Blazor uses Syncfusion Diagram |
| Node drag & drop from palette | ✅ | ✅ | |
| Node selection | ✅ | ✅ | |
| Multi-node selection | ✅ | ⚠️ | Limited selection modes |
| Node deletion | ✅ | ✅ | |
| Connector creation | ✅ | ✅ | |
| Connector deletion | ✅ | ✅ | |
| Orthogonal connectors | ✅ | ✅ | |
| Bezier curve connectors | ✅ | ⚠️ | Commented out, using Orthogonal |
| Node labels | ✅ | ✅ | |
| Node icons | ✅ | ✅ | Using Font Awesome |
| Node ports (input/output) | ✅ | ✅ | |
| Multiple output ports | ✅ | ⚠️ | Limited support |
| Node status display | ✅ | ✅ | |
| Grid display | ✅ | ✅ | |
| Snap to grid | ✅ | ✅ | |
| Zoom in/out | ✅ | ✅ | |
| Zoom reset | ✅ | ✅ | |
| Pan canvas | ✅ | ⚠️ | Basic support |
| Canvas background | ✅ | ✅ | |

---

## 2. Flow Management

| Feature | JS UI | Blazor UI | Notes |
|---------|-------|-----------|-------|
| Multiple flow tabs | ✅ | ✅ | |
| Add new flow | ✅ | ✅ | |
| Delete flow | ✅ | ✅ | |
| Rename flow | ✅ | ✅ | Via flow properties dialog |
| Flow properties dialog | ✅ | ✅ | |
| Enable/Disable flow | ✅ | ✅ | |
| Flow disabled visual indicator | ✅ | ✅ | Dashed tab borders |
| Flow info/description | ✅ | ✅ | |
| Switch between flows | ✅ | ✅ | State persisted in central registry |
| Flow scroll position persistence | ✅ | ❌ | |
| Locked flows | ✅ | ❌ | |

---

## 3. Palette

| Feature | JS UI | Blazor UI | Notes |
|---------|-------|-----------|-------|
| Category organization | ✅ | ✅ | |
| Category expand/collapse | ✅ | ✅ | |
| Node filter/search | ✅ | ✅ | |
| Node count display | ✅ | ✅ | |
| Node drag to canvas | ✅ | ✅ | |
| Dynamic palette from SDK nodes | ✅ | ✅ | Loaded from NodeLoader |
| Category persistence (collapsed state) | ✅ | ❌ | |
| Palette editor (install modules) | ✅ | ⚠️ | Basic UI, npm integration placeholder |
| Module version display | ✅ | ⚠️ | Shows "Unknown" |
| Module uninstall | ✅ | ⚠️ | Placeholder implementation |
| Online module search | ✅ | ❌ | |

---

## 4. Sidebar

| Feature | JS UI | Blazor UI | Notes |
|---------|-------|-----------|-------|
| Sidebar tabs | ✅ | ✅ | Using Syncfusion Tabs |
| Info tab | ✅ | ✅ | `RedUiSidebarInfo` component |
| Help tab | ✅ | ✅ | `RedUiSidebarHelp` component |
| Config tab | ✅ | ✅ | `RedUiSidebarConfig` component |
| Context tab | ✅ | ✅ | `RedUiSidebarContext` component |
| Debug tab | ✅ | ✅ | `RedUiSidebarDebug` component |
| Outliner (tree view) | ✅ | ✅ | In Info tab |
| Sidebar resize | ✅ | ✅ | Using Syncfusion Splitter |
| Sidebar collapse | ✅ | ✅ | Splitter pane collapse |
| Tab pinning | ✅ | ⚠️ | Visual only, no reordering |
| Custom sidebar tabs (plugins) | ✅ | ❌ | |

---

## 5. Node Property Editor (Tray)

| Feature | JS UI | Blazor UI | Notes |
|---------|-------|-----------|-------|
| Tray slide-in animation | ✅ | ✅ | CSS transition |
| Node name editing | ✅ | ✅ | |
| Dynamic property form | ✅ | ✅ | From SDK node definitions |
| Properties tab | ✅ | ✅ | |
| Description tab | ✅ | ✅ | Markdown editor |
| Appearance tab | ✅ | ✅ | Basic implementation |
| Delete button | ✅ | ✅ | |
| Done/Cancel buttons | ✅ | ✅ | |
| Tray resize | ✅ | ❌ | Fixed width |
| Multiple tray stack | ✅ | ❌ | Single tray only |
| TypedInput widget | ✅ | ❌ | Critical missing feature |
| EditableList widget | ✅ | ❌ | Critical missing feature |
| JSONata expression editor | ✅ | ❌ | |
| Buffer editor | ✅ | ❌ | |
| Color picker | ✅ | ❌ | |
| Icon picker | ✅ | ❌ | |
| Code editor (Ace/Monaco) | ✅ | ⚠️ | Basic textarea only |

---

## 6. Groups

| Feature | JS UI | Blazor UI | Notes |
|---------|-------|-----------|-------|
| Create group from selection | ✅ | ✅ | |
| Group visual box | ✅ | ✅ | |
| Group label | ✅ | ✅ | |
| Group move with nodes | ✅ | ✅ | Nodes auto-selected |
| Ungroup | ✅ | ✅ | |
| Remove node from group | ✅ | ✅ | Via context menu |
| Group color | ✅ | ⚠️ | Fixed color |
| Group resize | ✅ | ❌ | |
| Nested groups | ✅ | ❌ | |
| Group in group add mode | ✅ | ❌ | |

---

## 7. Subflows

| Feature | JS UI | Blazor UI | Notes |
|---------|-------|-----------|-------|
| Create subflow | ✅ | ✅ | |
| Subflow from selection | ✅ | ✅ | |
| Subflow editing tab | ✅ | ✅ | |
| Subflow toolbar | ✅ | ✅ | |
| Subflow input/output ports | ✅ | ✅ | |
| Subflow instances | ✅ | ✅ | |
| Edit subflow template | ✅ | ✅ | From instance |
| Subflow status output | ✅ | ✅ | Toggle in toolbar |
| Subflow properties | ✅ | ⚠️ | Basic dialog |
| Subflow as module | ✅ | ❌ | |
| Subflow environment variables | ✅ | ❌ | |
| Subflow credential inputs | ✅ | ❌ | |

---

## 8. Deploy

| Feature | JS UI | Blazor UI | Notes |
|---------|-------|-----------|-------|
| Deploy button | ✅ | ✅ | |
| Deploy modes (Full/Flows/Nodes) | ✅ | ✅ | |
| Start flows | ✅ | ✅ | |
| Stop flows | ✅ | ✅ | |
| Restart flows | ✅ | ✅ | |
| Deploy status indicator | ✅ | ✅ | |
| Deploy confirmation dialog | ✅ | ❌ | |
| Deploy conflict detection | ✅ | ❌ | |

---

## 9. Import/Export

| Feature | JS UI | Blazor UI | Notes |
|---------|-------|-----------|-------|
| Import dialog | ✅ | ✅ | |
| Export dialog | ✅ | ✅ | |
| Export formatted/minified | ✅ | ✅ | |
| Copy to clipboard | ✅ | ✅ | |
| Download as file | ✅ | ✅ | |
| Import from examples | ✅ | ❌ | |
| Import from library | ✅ | ❌ | |
| Export to library | ✅ | ❌ | |

---

## 10. Undo/Redo & Clipboard

| Feature | JS UI | Blazor UI | Notes |
|---------|-------|-----------|-------|
| Undo | ✅ | ✅ | |
| Redo | ✅ | ✅ | |
| Cut | ✅ | ✅ | |
| Copy | ✅ | ✅ | |
| Paste | ✅ | ✅ | |
| Delete | ✅ | ✅ | |
| Delete & Reconnect | ✅ | ✅ | |
| Select All | ✅ | ✅ | |
| Keyboard shortcuts | ✅ | ⚠️ | Limited implementation |
| Keyboard shortcuts dialog | ✅ | ✅ | |

---

## 11. Context Menu

| Feature | JS UI | Blazor UI | Notes |
|---------|-------|-----------|-------|
| Node context menu | ✅ | ✅ | |
| Canvas context menu | ✅ | ✅ | |
| Submenu support | ✅ | ✅ | |
| Keyboard shortcut display | ✅ | ✅ | |
| Disabled menu items | ✅ | ✅ | |
| Quick insert node | ✅ | ⚠️ | Limited to inject |
| Insert junction | ✅ | ✅ | |
| Link nodes insertion | ✅ | ❌ | |

---

## 12. Search

| Feature | JS UI | Blazor UI | Notes |
|---------|-------|-----------|-------|
| Search dialog | ✅ | ✅ | |
| Search by node name | ✅ | ✅ | |
| Search by node type | ✅ | ✅ | |
| Search results highlight | ✅ | ⚠️ | Basic implementation |
| Quick search (Ctrl+.) | ✅ | ❌ | Dialog only |
| Search across flows | ✅ | ⚠️ | Current flow only |

---

## 13. Debug

| Feature | JS UI | Blazor UI | Notes |
|---------|-------|-----------|-------|
| Debug message list | ✅ | ✅ | |
| Debug message highlight | ✅ | ✅ | Node flash |
| Debug message filter | ✅ | ⚠️ | Basic filter |
| Debug message count | ✅ | ✅ | |
| Clear debug messages | ✅ | ✅ | |
| Debug data expansion | ✅ | ⚠️ | JSON formatted |
| Debug message path | ✅ | ❌ | |
| Filter by node | ✅ | ⚠️ | Basic |
| Debug sidebar icon indicator | ✅ | ❌ | |

---

## 14. Settings & Configuration

| Feature | JS UI | Blazor UI | Notes |
|---------|-------|-----------|-------|
| User settings dialog | ✅ | ✅ | Basic |
| Grid settings | ✅ | ✅ | |
| View settings | ✅ | ⚠️ | Limited options |
| Editor settings | ✅ | ❌ | |
| Keyboard shortcuts customization | ✅ | ❌ | |
| Language/locale support | ✅ | ❌ | |

---

## 15. Projects (Version Control)

| Feature | JS UI | Blazor UI | Notes |
|---------|-------|-----------|-------|
| Project creation | ✅ | ❌ | |
| Project settings | ✅ | ❌ | |
| Version control tab | ✅ | ❌ | |
| Git integration | ✅ | ❌ | |
| File history | ✅ | ❌ | |
| Branch management | ✅ | ❌ | |

---

## 16. Additional JS UI Features Not in Blazor

| Feature | Description | Priority |
|---------|-------------|----------|
| **TypedInput widget** | Multi-type input (string, number, boolean, msg, flow, global, JSONata, etc.) | **Critical** |
| **EditableList widget** | Dynamic list editor for rules, properties, etc. | **Critical** |
| **View navigator** | Mini-map for large flows | Medium |
| **View tools** | Alignment, distribution, spacing tools | Medium |
| **Action list** | Command palette (Ctrl+Shift+P) | Medium |
| **Diff viewer** | Visual flow diff | Medium |
| **Tour guide** | Interactive tutorials | Low |
| **Touch support** | Radial menu, touch gestures | Low |
| **Multiplayer** | Multi-user editing | Low |
| **Event log** | System event logging | Low |
| **Diagnostics** | System diagnostics | Low |
| **Link nodes** | Link in/out for virtual wires | Medium |
| **Annotations** | Canvas annotations | Low |
| **Status bar** | Bottom status bar | Low |

---

## 17. Common UI Components

### JS UI Common Components (`/ui/common/`)
| Component | JS UI | Blazor UI | Notes |
|-----------|-------|-----------|-------|
| panels.js | ✅ | ⚠️ | Using Syncfusion Splitter |
| stack.js | ✅ | ❌ | Accordion-like stacking |
| tabs.js | ✅ | ✅ | Using Syncfusion Tabs |
| menu.js | ✅ | ✅ | Custom dropdown menus |
| treeList.js | ✅ | ⚠️ | Basic outliner |
| searchBox.js | ✅ | ✅ | Basic input |
| typedInput.js | ✅ | ❌ | **Critical** |
| popover.js | ✅ | ❌ | Tooltips |
| toggleButton.js | ✅ | ❌ | |
| autoComplete.js | ✅ | ❌ | |
| checkboxSet.js | ✅ | ❌ | |
| editableList.js | ✅ | ❌ | **Critical** |

---

## 18. Editor Types (`/ui/editors/`)

| Editor | JS UI | Blazor UI | Notes |
|--------|-------|-----------|-------|
| text.js | ✅ | ⚠️ | Basic textarea |
| code-editor.js | ✅ | ❌ | Ace/Monaco integration |
| json.js | ✅ | ❌ | JSON editor |
| js.js | ✅ | ❌ | JavaScript editor |
| expression.js | ✅ | ❌ | JSONata expression |
| markdown.js | ✅ | ❌ | Markdown editor |
| buffer.js | ✅ | ❌ | Binary buffer editor |
| colorPicker.js | ✅ | ❌ | Color picker |
| iconPicker.js | ✅ | ❌ | Icon picker |
| mermaid.js | ✅ | ❌ | Diagram editor |
| envVarList.js | ✅ | ❌ | Environment variables |

---

## Priority Recommendations

### Critical (Must Have for .NET Port)
1. **TypedInput widget** - Used extensively in all node property editors
2. **EditableList widget** - Used for rules, outputs, properties lists
3. **Code editor integration** - For function nodes, templates, etc.
4. **JSONata expression editor** - For transformation expressions

### High Priority
1. Link nodes support
2. Project/version control support
3. Full keyboard shortcuts
4. View navigator (mini-map)
5. Advanced search across flows

### Medium Priority
1. View tools (alignment, distribution)
2. Action list (command palette)
3. Diff viewer
4. Better multi-output port support
5. Subflow environment variables

### Low Priority
1. Tour guide
2. Touch/mobile support
3. Multiplayer editing
4. Event log
5. Diagnostics

---

## Architectural Differences

### JavaScript UI
- Uses D3.js for SVG-based canvas rendering
- jQuery for DOM manipulation
- Event-driven architecture with RED events
- Local storage for settings persistence
- WebSocket for runtime communication

### Blazor UI
- Uses Syncfusion Blazor Diagram for canvas
- Syncfusion Blazor components (Tabs, Splitter, etc.)
- Component-based architecture
- SignalR for server communication
- .NET services for runtime (IFlowRuntime, INodeRegistry, etc.)

---

## Recommendations for Feature Parity

1. **Implement TypedInput Component**
   - Create a Blazor component that replicates the multi-type input functionality
   - Support for: string, number, boolean, msg, flow, global, JSONata, env, bin, date, JSON

2. **Implement EditableList Component**
   - Create a Blazor component for dynamic list editing
   - Used in: switch node, change node, function outputs, etc.

3. **Integrate Monaco Editor**
   - Add Syncfusion Code Editor or BlazorMonaco package
   - Required for function nodes, template nodes, etc.

4. **Add JSONata Support**
   - Integrate jsonata-cs or similar .NET library
   - Create expression editor with syntax highlighting

5. **Complete Keyboard Shortcuts**
   - Implement JS interop for global keyboard handling
   - Add configurable shortcut mapping

6. **Add Link Nodes**
   - Implement link-in and link-out node types
   - Virtual wire rendering

7. **Project Support**
   - Design .NET project structure
   - Git integration via LibGit2Sharp

---

## Conclusion

The Blazor UI provides a solid foundation with core editor functionality. However, several critical widgets (TypedInput, EditableList, code editors) are missing and are essential for full node configuration. The priority should be implementing these components before addressing the medium and low priority features.

The architectural transition from JavaScript/D3/jQuery to Blazor/Syncfusion is well underway, but the custom widget gap is the primary blocker for achieving feature parity with the original Node-RED editor.
