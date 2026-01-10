# C# Backend Analysis: Feature Comparison with JavaScript Backend

This document provides a comprehensive analysis of the C# backend implementation compared to the original JavaScript Node-RED backend.

## Overview

The repository contains two backends:
1. **JavaScript Backend** - Original Node-RED implementation (`packages/node_modules/@node-red/`)
2. **C# Backend** - .NET/Blazor implementation (`src/NodeRed.Core`, `src/NodeRed.Runtime`, `src/NodeRed.Blazor`, `src/NodeRed.SDK`)

## Feature Comparison

### ✅ Fully Implemented Features

| Feature | JS Location | C# Location | Notes |
|---------|-------------|-------------|-------|
| **Flows** | `packages/node_modules/@node-red/runtime/lib/flows/Flow.js` | `src/NodeRed.Runtime/Execution/FlowRuntime.cs`, `FlowExecutor.cs` | Tab-based flow organization works correctly |
| **Flow Execution** | Flow.js `start()`, `stop()` | `FlowExecutor.InitializeAsync()`, `StartAsync()`, `StopAsync()` | Node lifecycle management implemented |
| **Node Registry** | `packages/node_modules/@node-red/registry/` | `src/NodeRed.Runtime/Services/NodeRegistry.cs` | Dynamic node type registration |
| **Message Routing** | Flow.js `send()` | `FlowExecutor.RouteMessage()` | Message passing between nodes via wires |
| **Deploy (Full)** | `packages/node_modules/@node-red/runtime/lib/api/flows.js` | `FlowRuntime.DeployAsync()` | Full deployment restarts all flows |
| **Debug Messages** | Debug node events | `FlowRuntime.OnDebugMessage` event | Debug output to sidebar |
| **Node Status** | `node.status()` | `FlowExecutor.UpdateNodeStatus()` | Visual status indicators on nodes |
| **Flow Storage** | `packages/node_modules/@node-red/runtime/lib/storage/` | `src/NodeRed.Runtime/Services/InMemoryFlowStorage.cs` | Save/load workspace |
| **Import/Export** | flows.js | `IFlowStorage.ExportAsync()`, `ImportAsync()` | JSON serialization |
| **Groups** | `packages/node_modules/@node-red/editor-client/src/js/ui/group.js` | `Editor.razor.cs` `GroupSelectedNodes()` | Visual grouping (UI only) |

### ⚠️ Partially Implemented Features

| Feature | Status | Gap Description |
|---------|--------|-----------------|
| **Subflows** | UI ✅, Runtime ❌ | The Blazor editor has full subflow UI support (create, edit, delete, convert to subflow) in `Editor.razor.cs`, but the **runtime has no subflow expansion logic**. The JS backend's `Subflow.js` class creates cloned node instances with remapped IDs - this is missing in C#. |
| **Deploy (Flows)** | Fallback to Full | `FlowRuntime.cs:156` has `// TODO: Implement incremental deployment` - currently falls back to full deployment |
| **Deploy (Nodes)** | Fallback to Full | Same as above - modified-nodes-only deployment not implemented |
| **Catch Node** | Node exists, runtime integration missing | `CatchNode.cs` exists, but `FlowExecutor.cs:147` has `// TODO: Implement catch node notification` - errors don't trigger catch nodes |
| **Complete Node** | Node exists, runtime integration missing | `CompleteNode.cs` exists, but runtime doesn't call it when nodes complete processing |
| **Link Nodes** | Nodes exist, cross-flow not implemented | `LinkInNode.cs`, `LinkOutNode.cs`, `LinkCallNode.cs` exist but cross-flow routing and return mode not implemented |
| **Context** | Flow/Global ✅, Node ❌ | `FlowExecutor` has `_flowContext` and `_globalContext`, but individual node context is not exposed |

### ❌ Not Implemented Features

| Feature | Description | JS Location | Priority |
|---------|-------------|-------------|----------|
| **Subflow Runtime Execution** | When a subflow instance is used, its internal nodes should be instantiated and executed | `Subflow.js` | **High** |
| **Credentials** | Secure storage for passwords, API keys | `packages/node_modules/@node-red/runtime/lib/nodes/credentials.js` | Medium |
| **Environment Variables** | Flow-level env vars, NR_NODE_ID, etc. | `flowUtil.evaluateEnvProperties()` | Medium |
| **Hooks System** | Pre/post message delivery hooks | `@node-red/util/hooks.js` | Low |
| **Complete Node Triggering** | Calling done() should notify Complete nodes | Flow.js `handleComplete()` | Medium |
| **Catch Node Triggering** | Errors should route to Catch nodes | Flow.js `handleError()` | Medium |
| **Status Node Triggering** | Status changes should route to Status nodes | Flow.js `handleStatus()` | Low |
| **Link Call Return Mode** | Request/response through Link nodes | Flow.js | Low |

## Detailed Gap Analysis

### 1. Subflow Runtime Execution (Critical Gap)

**Current State in C#:**
- The Blazor editor (`Editor.razor.cs`) has comprehensive UI support for subflows:
  - Creating subflows (empty or from selection)
  - Editing subflow templates
  - Adding/removing inputs/outputs
  - Status output support
- Subflow instance nodes appear in the palette and can be dragged to flows
- However, when flows are deployed, subflow instances are treated as unknown node types

**What's Missing:**
The JS backend's `Subflow.js` does the following when a subflow instance is started:
1. Clones all internal node definitions with new unique IDs
2. Remaps all wire connections to use new IDs
3. Creates a wrapper node to handle input/output routing
4. Manages the subflow's internal context

**C# Implementation Needed:**
```csharp
// In FlowExecutor.InitializeAsync() or a new SubflowExecutor class:
if (nodeConfig.Type.StartsWith("subflow:"))
{
    var subflowId = nodeConfig.Type.Substring(8);
    var subflowDef = workspace.Subflows.FirstOrDefault(s => s.Id == subflowId);
    // Clone nodes, remap IDs, create internal executor
}
```

### 2. Catch Node Integration

**Current State:**
- `CatchNode.cs` exists and defines properties/help
- `FlowExecutor.HandleNodeError()` logs errors but has `// TODO: Implement catch node notification`

**Fix Required:**
```csharp
// In FlowExecutor.HandleNodeError():
var catchNodes = _flow.Nodes.Where(n => n.Type == "catch").ToList();
foreach (var catchNodeConfig in catchNodes)
{
    if (_nodes.TryGetValue(catchNodeConfig.Id, out var catchNode))
    {
        var errorMsg = new NodeMessage
        {
            Payload = error.Message,
            Error = new { message = error.Message, source = new { id = nodeId } }
        };
        await catchNode.OnInputAsync(errorMsg);
    }
}
```

### 3. Deploy Modes

**Current State:**
- `FlowRuntime.DeployAsync()` accepts `DeployType` enum (Full, Flows, Nodes)
- All modes currently fall back to Full deployment

**JS Behavior:**
- **Flows mode:** Only restart flows that have changed
- **Nodes mode:** Only restart specific nodes that have changed, rewire if needed

### 4. Context Management

**Current State:**
- Flow context: `Dictionary<string, object?> _flowContext` in FlowExecutor
- Global context: `Dictionary<string, object?> _globalContext` in FlowRuntime

**Missing:**
- Node-level context (each node should have its own context store)
- Context persistence (JS supports file/memory stores)
- Context API in NodeContext class

## Recommendations

### High Priority (Required for Feature Parity)

1. **Implement Subflow Runtime Execution**
   - Create `SubflowExecutor.cs` class similar to `Subflow.js`
   - Handle node cloning and ID remapping
   - Route messages through subflow input/output nodes

2. **Implement Catch Node Triggering**
   - Modify `FlowExecutor.HandleNodeError()` to route to Catch nodes
   - Support scope filtering (all, group, uncaught)

### Medium Priority

3. **Implement Complete Node Triggering**
   - Add `done` callback tracking to message routing
   - Trigger Complete nodes when messages finish processing

4. **Add Node Context**
   - Extend `NodeContext` class to include node-specific storage
   - Persist context between deployments

5. **Implement Credentials**
   - Create `ICredentialStorage` interface
   - Secure storage for sensitive node configuration

### Low Priority

6. **Incremental Deployment**
   - Track which flows/nodes have changed
   - Implement diff-based deployment

7. **Link Call Return Mode**
   - Implement request/response pattern through Link nodes

## File References

### C# Backend Key Files

| File | Purpose |
|------|---------|
| `src/NodeRed.Core/Interfaces/IFlowRuntime.cs` | Runtime interface definition |
| `src/NodeRed.Core/Entities/Flow.cs` | Flow data model |
| `src/NodeRed.Core/Entities/FlowNode.cs` | Node configuration model |
| `src/NodeRed.Runtime/Execution/FlowRuntime.cs` | Main runtime implementation |
| `src/NodeRed.Runtime/Execution/FlowExecutor.cs` | Single flow executor |
| `src/NodeRed.Runtime/Services/NodeRegistry.cs` | Node type registration |
| `src/NodeRed.Blazor/Components/Pages/Editor.razor.cs` | UI editor logic |
| `src/NodeRed.SDK/NodeBase.cs` | Base class for SDK nodes |

### JS Backend Key Files (for reference)

| File | Purpose |
|------|---------|
| `packages/node_modules/@node-red/runtime/lib/flows/Flow.js` | Flow execution |
| `packages/node_modules/@node-red/runtime/lib/flows/Subflow.js` | Subflow execution |
| `packages/node_modules/@node-red/runtime/lib/flows/index.js` | Flow management |
| `packages/node_modules/@node-red/runtime/lib/api/flows.js` | Deployment API |

## Conclusion

The C# backend has a solid foundation with the core flow execution working correctly. The main gaps are:

1. **Runtime subflow execution** - Critical for subflow feature to work end-to-end
2. **Error handling integration** - Catch nodes need runtime support
3. **Message completion tracking** - Complete nodes need runtime support
4. **Incremental deployment** - Currently all deploys are full restarts

The UI (Blazor editor) has implemented most features, but the runtime needs additional work to match the JavaScript backend's capabilities.
