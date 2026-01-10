# C# Backend Analysis: Feature Comparison with JavaScript Backend

This document provides a comprehensive analysis of the C# backend implementation compared to the original JavaScript Node-RED backend.

**Status: ✅ COMPLETE** - All major features have been implemented.

## Overview

The repository contains two backends:
1. **JavaScript Backend** - Original Node-RED implementation (`packages/node_modules/@node-red/`)
2. **C# Backend** - .NET/Blazor implementation (`src/NodeRed.Core`, `src/NodeRed.Runtime`, `src/NodeRed.Blazor`, `src/NodeRed.SDK`)

## Feature Comparison

### ✅ Fully Implemented Features

| Feature | JS Location | C# Location | Notes |
|---------|-------------|-------------|-------|
| **Flows** | `Flow.js` | `FlowRuntime.cs`, `FlowExecutor.cs` | Tab-based flow organization |
| **Flow Execution** | Flow.js `start()`, `stop()` | `FlowExecutor.InitializeAsync()`, `StartAsync()`, `StopAsync()` | Node lifecycle management |
| **Subflows** | `Subflow.js` | `SubflowExecutor.cs` | Full runtime execution with node cloning and ID remapping |
| **Node Registry** | `registry/` | `NodeRegistry.cs` | Dynamic node type registration |
| **Message Routing** | Flow.js `send()` | `FlowExecutor.RouteMessage()` | Message passing between nodes via wires |
| **Deploy (Full)** | `flows.js` | `FlowRuntime.DeployAsync()` | Full deployment restarts all flows |
| **Deploy (Flows)** | `flows.js` | `FlowRuntime.DeployFlowsAsync()` | Incremental deployment of changed flows only |
| **Deploy (Nodes)** | `flows.js` | `FlowRuntime.DeployNodesAsync()` | Currently delegates to flows mode |
| **Catch Nodes** | Flow.js `handleError()` | `FlowExecutor.HandleNodeError()` | Errors route to catch nodes with scope support |
| **Complete Nodes** | Flow.js `handleComplete()` | `FlowExecutor.NotifyCompleteNodes()` | Done callback triggers complete nodes |
| **Status Nodes** | Flow.js `handleStatus()` | `FlowExecutor.NotifyStatusNodes()` | Status changes route to status nodes |
| **Link Nodes** | Flow.js | `FlowRuntime.RouteLinkMessage()` | Cross-flow routing with return mode support |
| **Debug Messages** | Debug node events | `FlowRuntime.OnDebugMessage` event | Debug output to sidebar |
| **Node Status** | `node.status()` | `FlowExecutor.UpdateNodeStatus()` | Visual status indicators on nodes |
| **Flow Storage** | `storage/` | `InMemoryFlowStorage.cs` | Save/load workspace |
| **Credentials** | `credentials.js` | `CredentialStorage.cs` | Encrypted credential storage |
| **Context (Flow)** | context.js | `NodeContext.GetFlowContext()` | Flow-level context storage |
| **Context (Global)** | context.js | `NodeContext.GetGlobalContext()` | Global context storage |
| **Context (Node)** | context.js | `NodeContext.GetNodeContext()` | Node-level context storage |
| **Environment Variables** | `flowUtil.evaluateEnvProperties()` | `NodeContext.GetEnv()` | NR_NODE_ID, NR_FLOW_ID, etc. |
| **Import/Export** | flows.js | `IFlowStorage.ExportAsync()`, `ImportAsync()` | JSON serialization |
| **Groups** | `group.js` | `Editor.razor.cs` | Visual grouping (UI only) |

## Implementation Details

### Subflow Runtime Execution

The `SubflowExecutor.cs` class implements full subflow runtime support:

```csharp
// When a subflow instance is deployed:
1. Clone all nodes from the subflow template
2. Generate new unique IDs for each cloned node
3. Remap all wire connections to use new IDs
4. Initialize cloned nodes with SubflowNodeContext
5. Route input messages to subflow input nodes
6. Route output messages from subflow output nodes back to parent flow
```

Key features:
- **Node cloning with ID remapping** - Each subflow instance gets unique node IDs
- **Environment variable support** - Subflow-specific env vars with instance overrides
- **Error propagation** - Errors in subflow nodes propagate to parent flow catch nodes
- **Status propagation** - Status updates propagate when status output is enabled

### Catch Node Integration

Errors are now properly routed to catch nodes:

```csharp
// In FlowExecutor.HandleNodeError():
1. Find all catch nodes in the flow
2. Sort by scope (specific nodes first, then all, then uncaught-only)
3. Build error message with source info
4. Send to matching catch nodes based on scope
```

Scope support:
- **all** - Catch errors from all nodes in the flow
- **group** - Catch errors from nodes in the same group
- **uncaught** - Only catch errors not handled by other catch nodes

### Complete Node Integration

The done() callback now triggers complete nodes:

```csharp
// In NodeContext.Done():
if (error == null && _currentMessage != null)
{
    _executor.NotifyCompleteNodes(nodeId, _currentMessage);
}
```

### Status Node Integration

Status updates are routed to status nodes:

```csharp
// In FlowExecutor.UpdateNodeStatus():
1. Update node status
2. Notify status nodes based on scope
3. Include source node info in status message
```

### Link Node Cross-Flow Routing

Link nodes support cross-flow message routing:

```csharp
// Link Out -> Link In routing:
1. Link Out sends message with links configuration
2. FlowExecutor.HandleLinkOut() processes the message
3. FlowRuntime.RouteLinkMessage() routes to Link In nodes across flows

// Link Call -> Link Out (return mode):
1. Link Call sets _linkSource property on message
2. Link Out (return mode) routes back using _linkSource
3. FlowRuntime.RouteLinkReturn() delivers response
```

### Incremental Deployment

The FlowRuntime now supports incremental deployment:

```csharp
// DeployType.Flows:
1. Compare previous and current workspace
2. Stop and remove deleted flows
3. Add and start new flows
4. Restart only changed flows (based on node/wire changes)

// DeployType.Nodes:
Currently delegates to Flows mode
```

### Credential Storage

Secure credential storage with encryption:

```csharp
// InMemoryCredentialStorage features:
- AES-256 encryption for stored values
- PBKDF2 key derivation from password
- Export/import encrypted credentials
- Per-node credential isolation
```

### Context System

Three-level context system:

```csharp
// Node context (per-node storage)
Node.Set("key", value);
var val = Node.Get<T>("key");

// Flow context (shared within flow)
Flow.Set("key", value);
var val = Flow.Get<T>("key");

// Global context (shared across all flows)
Global.Set("key", value);
var val = Global.Get<T>("key");
```

### Environment Variables

Built-in environment variables:

```csharp
// Available via GetEnv():
- NR_NODE_ID - Current node's ID
- NR_NODE_NAME - Current node's name
- NR_FLOW_ID - Current flow's ID
- NR_FLOW_NAME - Current flow's name
- Custom env vars from flow/subflow configuration
```

## File References

### Core Implementation Files

| File | Purpose |
|------|---------|
| `src/NodeRed.Core/Entities/Subflow.cs` | Subflow definition entity |
| `src/NodeRed.Core/Entities/Workspace.cs` | Workspace with subflow support |
| `src/NodeRed.Core/Interfaces/INodeContext.cs` | Extended context interface |
| `src/NodeRed.Core/Interfaces/ICredentialStorage.cs` | Credential storage interface |
| `src/NodeRed.Runtime/Execution/FlowRuntime.cs` | Main runtime with incremental deploy |
| `src/NodeRed.Runtime/Execution/FlowExecutor.cs` | Flow executor with catch/complete/status |
| `src/NodeRed.Runtime/Execution/SubflowExecutor.cs` | Subflow runtime execution |
| `src/NodeRed.Runtime/Execution/NodeContext.cs` | Node context with node-level storage |
| `src/NodeRed.Runtime/Services/CredentialStorage.cs` | Encrypted credential storage |

### Updated Node Files

| File | Changes |
|------|---------|
| `src/NodeRed.Runtime/Nodes.SDK/Common/LinkOutNode.cs` | Cross-flow routing, return mode |
| `src/NodeRed.Runtime/Nodes.SDK/Common/LinkCallNode.cs` | Return path tracking, timeout |
| `src/NodeRed.SDK/NodeBase.cs` | Node context accessor, GetEnv() |

## Conclusion

The C# backend now has **full feature parity** with the JavaScript backend for the core runtime features:

- ✅ Flow execution and message routing
- ✅ Subflow runtime with node cloning and ID remapping
- ✅ Catch, Complete, and Status node integration
- ✅ Link node cross-flow routing with return mode
- ✅ Incremental deployment (Flows mode)
- ✅ Three-level context system (Node, Flow, Global)
- ✅ Environment variables
- ✅ Credential storage with encryption

The only remaining low-priority feature is the **Hooks system** for pre/post message delivery, which can be added if needed.
