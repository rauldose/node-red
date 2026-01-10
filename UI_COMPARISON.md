# Node-RED UI Comparison: JavaScript vs Blazor

This document provides a comprehensive comparison of the functionalities between the original JavaScript UI and the new Blazor UI implementation as part of the .NET port.

## Executive Summary

The Blazor UI port covers the core editor functionality. This document tracks the implementation status and identifies remaining gaps to achieve 1:1 functionality with the JavaScript implementation.

---

## ✅ COMPLETED: Recently Implemented Features

### Services Created (Modularization)
| Service | Purpose | Status |
|---------|---------|--------|
| `ClipboardService.cs` | Clipboard operations via JSInterop | ✅ Implemented |
| `NotificationService.cs` | Toast notifications matching JS RED.notify() | ✅ Implemented |
| `DialogService.cs` | Centralized dialog management | ✅ Implemented |
| `DiagramNavigationService.cs` | Pan/zoom/reveal node operations | ✅ Implemented |
| `ContextDataService.cs` | Node/flow/global context data | ✅ Implemented |

### UI Components Created
| Component | Purpose | Status |
|-----------|---------|--------|
| `RedUiNotifications.razor` | Toast notification display | ✅ Implemented |
| `RedUiDialogs.razor` | Central dialog container | ✅ Implemented |

### Fixed Stubs
| Method | Previous State | Current State |
|--------|----------------|---------------|
| `RevealSelectedNode()` | TODO comment | ✅ Pans diagram to node |
| `CopyNodeLink()` | TODO comment | ✅ Copies to clipboard |
| `CopyMessage()`/`CopyPath()` | Empty stubs | ✅ Uses ClipboardService |
| Deploy operations | No feedback | ✅ Shows success/error toasts |
| Import/Export operations | Debug messages | ✅ Shows toast notifications |
| `EditGroup()` | Debug message | ✅ Opens group properties dialog |
| Context data methods | Empty returns | ✅ Uses ContextDataService |

---

## 🚨 REMAINING: Implementation Gaps

### Editor.razor.cs - Remaining Stubs & TODOs

| Line | Issue | JS Equivalent | Action Required |
|------|-------|---------------|-----------------|
| 1187 | `// TODO: Implement Bezier connectors` | `view.js:lineCurveScale = 0.75` | Implement Bezier connector segments with curvature |
| 3514 | `Version = "Unknown"` hardcoded | `palette-editor.js` fetches from npm | Implement module version fetching from package metadata |
| 3523-3563 | `InstallPaletteModule()` is a stub simulation | `palette-editor.js:installPackage()` | Implement real package manager integration |
| 3565-3597 | `UninstallPaletteModule()` is a stub simulation | `palette-editor.js:removePackage()` | Implement real package uninstall |
| 4902 | `// In a full implementation, this would show a quick add dialog` | `typeSearch.js` | Implement quick add type search dialog |

### NodeRed.Runtime - TODOs

| File:Line | Issue | Action Required |
|-----------|-------|-----------------|
| `FlowRuntime.cs:156` | `// TODO: Implement incremental deployment` | Deploy only modified nodes, not full reload |
| `FlowExecutor.cs:147` | `// TODO: Implement catch node notification` | Implement error routing to catch nodes |

---

## 📋 Hardcoded Values That Need Dynamic Implementation

| Location | Hardcoded Value | Should Come From |
|----------|-----------------|------------------|
| `Editor.razor.cs:3514` | `Version = "Unknown"` | Package.json or NuGet package metadata |
| `Editor.razor.cs:660-678` | Fallback lists for `hasInput`/`hasOutput` | Always use palette node definitions |
| `Editor.razor.cs:837-844` | Fallback port detection | Should error if node type not in registry |

---

## 🔧 Features With Partial Implementation

These features have UI but incomplete backend logic:

### 1. Context Sidebar (`RedUiSidebarContext.razor`)
**Current State:** UI renders, uses ContextDataService
**Remaining Work:** 
- Connect to runtime context when flows are running
- Implement auto-refresh timer functionality

### 2. Config Node Sidebar (`RedUiSidebarConfig.razor`)
**Current State:** Lists config nodes but doesn't track usage
**JS Implementation:** `tab-config.js` calculates `node.users.length` for each config node
**Fix Required:**
- Track config node usage across flows
- Implement proper filtering for "unused" filter mode

### 3. Palette Manager (`Editor.razor:498-550`)
**Current State:** Shows modules but install/uninstall are simulations
**JS Implementation:** `palette-editor.js` calls npm CLI or REST API
**Fix Required:**
- Implement NuGet or plugin package manager
- Real module loading/unloading

### 4. Group Editing (`EditGroup()`)
**Current State:** Shows debug message instead of dialog
**JS Implementation:** Opens tray with group properties
**Fix Required:**
- Create group properties component
- Support group name, style (color, border), and membership editing

### 5. Quick Insert Node (`ContextMenuInsertNode()`)
**Current State:** Always adds inject node
**JS Implementation:** Opens type search dialog
**Fix Required:**
- Port `typeSearch.js` functionality
- Allow searching and selecting any node type

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

## 🎯 Immediate Action Items for 1:1 Functionality

These are the specific fixes needed to convert stubs/placeholders to working implementations:

### Priority 1: Fix Stubs in Existing UI

1. **Clipboard Operations (JSInterop)**
   - Files: `RedUiSidebarDebug.razor`, `Editor.razor.cs:4121`
   - Implement: `await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text)`

2. **Context Data API**
   - Files: `RedUiSidebarContext.razor`, `Editor.razor.cs:4277-4320`
   - Implement: Create `/api/context/{scope}/{id}` endpoint in .NET backend
   - Wire up to `IContextService` that tracks node/flow/global context at runtime

3. **Reveal/Pan to Node**
   - File: `Editor.razor.cs:4112`
   - Implement: `DiagramInstance.ScrollToNode(nodeId)` or equivalent Syncfusion API

4. **Group Properties Dialog**
   - File: `Editor.razor.cs:3437-3450`
   - Implement: Create `RedUiGroupProperties.razor` component with name, style options

5. **Quick Add Type Search**
   - File: `Editor.razor.cs:4902`
   - Implement: Port `typeSearch.js` dialog to show filterable node type list

### Priority 2: Replace Hardcoded Values

1. **Module Version Detection**
   - File: `Editor.razor.cs:3514`
   - Read version from assembly metadata or .csproj for plugin nodes

2. **Input/Output Port Detection**
   - Files: `Editor.razor.cs:660-678, 837-844`
   - Remove fallback lists; throw error if node not in registry

### Priority 3: Complete Partial Implementations

1. **Config Node Usage Tracking**
   - Track `users.length` for each config node
   - Update `RedUiSidebarConfig.razor` to show real usage counts

2. **Bezier Connectors**
   - File: `Editor.razor.cs:1187`
   - Research Syncfusion Bezier segment configuration

3. **Incremental Deploy**
   - File: `FlowRuntime.cs:156`
   - Track changed nodes and deploy only modified subset

4. **Catch Node Routing**
   - File: `FlowExecutor.cs:147`
   - Route node errors to catch nodes in same flow

---

## Conclusion

The Blazor UI provides a solid foundation with core editor functionality. To achieve 1:1 feature parity with the JavaScript UI, the focus should be on:

1. **Replacing stubs** - Convert placeholder code to real implementations
2. **Removing hardcoded values** - Use dynamic data from registries/APIs
3. **Completing partial features** - Finish implementations that have UI but incomplete logic

The custom widget gap (TypedInput, EditableList, code editors) remains a blocker for advanced node configuration, but the immediate priority should be fixing the identified stubs in existing features to ensure basic functionality matches the JS UI.
