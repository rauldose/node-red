# C# Backend Implementation Roadmap

This document outlines the remaining features to implement for full feature parity between the C# backend and the JavaScript backend.

## Current Status

### ✅ Already Implemented
- Core runtime (flows, subflows, nodes)
- Catch/Complete/Status nodes
- Link node cross-flow routing
- Environment variables (NR_NODE_ID, NR_NODE_NAME, NR_FLOW_ID, NR_FLOW_NAME)
- Credential storage (AES-256 encrypted)
- Incremental deployment (Full/Flows/Nodes modes)
- Node/Flow/Global context
- Comprehensive unit tests (104 tests)

---

## Implementation Phases

### Phase 1: Authentication & Authorization (Critical)
**Priority: HIGH** | **Estimated: 2-3 days**

Reference: `packages/node_modules/@node-red/editor-api/lib/auth/`

#### 1.1 User Management
- [ ] Create `IUserService` interface
- [ ] Create `User` entity with properties:
  - `Username`
  - `Password` (hashed)
  - `Permissions` (list of scopes)
  - `Anonymous` flag
- [ ] Implement `InMemoryUserService` for development
- [ ] Support for user authentication strategies

#### 1.2 Token Management
- [ ] Create `ITokenService` interface
- [ ] Implement JWT token generation
- [ ] Token expiration and refresh
- [ ] Token revocation
- [ ] Store tokens securely

#### 1.3 Permission System
- [ ] Define permission scopes:
  - `flows.read` - Read flow configurations
  - `flows.write` - Modify flow configurations
  - `nodes.read` - Read node information
  - `nodes.write` - Install/remove nodes
  - `library.read` - Read library content
  - `library.write` - Modify library content
  - `*` - Full access
- [ ] Create `PermissionAttribute` for API endpoints
- [ ] Implement permission checking middleware

#### 1.4 Authentication Strategies
- [ ] Username/password authentication
- [ ] OAuth2 token exchange
- [ ] Generic strategy support (for SSO integration)

**Files to Create:**
```
src/NodeRed.Core/Entities/User.cs
src/NodeRed.Core/Interfaces/IUserService.cs
src/NodeRed.Core/Interfaces/ITokenService.cs
src/NodeRed.Runtime/Services/UserService.cs
src/NodeRed.Runtime/Services/TokenService.cs
src/NodeRed.Runtime/Services/PermissionService.cs
```

---

### Phase 2: Flow Validation (Critical)
**Priority: HIGH** | **Estimated: 1-2 days**

Reference: `packages/node_modules/@node-red/runtime/lib/flows/util.js`

#### 2.1 Node Diff Detection
- [ ] Create `FlowDiffService` to detect changes between flow versions
- [ ] Implement `DiffNodes(oldNode, newNode)` method
- [ ] Ignore position-only changes (x, y)
- [ ] Support group node comparison

#### 2.2 Wire Validation
- [ ] Validate wire connections reference existing nodes
- [ ] Check for circular dependencies
- [ ] Validate port indices are valid

#### 2.3 Environment Variable Validation
- [ ] Validate `${ENV_VAR}` syntax
- [ ] Check for undefined required variables
- [ ] Support credential type env vars

#### 2.4 Configuration Validation
- [ ] Validate required node properties
- [ ] Check node type exists in registry
- [ ] Validate config node references

**Files to Create:**
```
src/NodeRed.Core/Interfaces/IFlowValidator.cs
src/NodeRed.Runtime/Services/FlowValidator.cs
src/NodeRed.Runtime/Services/FlowDiffService.cs
```

---

### Phase 3: Version Conflict Detection (Critical)
**Priority: HIGH** | **Estimated: 0.5 days**

Reference: `packages/node_modules/@node-red/runtime/lib/api/flows.js:78-85`

#### 3.1 Flow Revision Tracking
- [ ] Add `Revision` property to `Workspace`
- [ ] Generate new revision on each save (UUID or hash)
- [ ] Store revision history (optional, for rollback)

#### 3.2 Conflict Detection
- [ ] Accept `rev` parameter on save operations
- [ ] Compare with current revision
- [ ] Return HTTP 409 on version mismatch
- [ ] Include current revision in error response

**Files to Modify:**
```
src/NodeRed.Core/Entities/Workspace.cs (add Revision)
src/NodeRed.Runtime/Execution/FlowRuntime.cs (check revision on deploy)
```

---

### Phase 4: Multiuser/Multiplayer Editing (Important)
**Priority: MEDIUM** | **Estimated: 2-3 days**

Reference: `packages/node_modules/@node-red/runtime/lib/multiplayer/index.js`

#### 4.1 Session Management
- [ ] Create `MultiplayerSession` class
- [ ] Track active sessions with session ID
- [ ] Handle connection/disconnection events
- [ ] Implement session timeout (30 seconds idle)

#### 4.2 User Presence
- [ ] Track which user is editing which flow/node
- [ ] Broadcast location changes to other sessions
- [ ] Support anonymous users with random names

#### 4.3 Real-time Communication
- [ ] Implement WebSocket/SignalR hub for multiplayer events
- [ ] Message types:
  - `multiplayer/connect` - New connection
  - `multiplayer/disconnect` - Connection removed
  - `multiplayer/location` - Cursor/selection update
  - `multiplayer/init` - Initial session list

**Files to Create:**
```
src/NodeRed.Core/Entities/MultiplayerSession.cs
src/NodeRed.Runtime/Services/MultiplayerService.cs
src/NodeRed.Blazor/Hubs/MultiplayerHub.cs (SignalR)
```

---

### Phase 5: Projects (Nice to Have)
**Priority: LOW** | **Estimated: 3-4 days**

Reference: `packages/node_modules/@node-red/runtime/lib/api/projects.js`

#### 5.1 Project Structure
- [ ] Create `Project` entity
- [ ] Support multiple flows per project
- [ ] Project settings and metadata

#### 5.2 Git Integration
- [ ] Initialize Git repository
- [ ] Commit changes
- [ ] Branch management
- [ ] Remote operations (push, pull)

**Files to Create:**
```
src/NodeRed.Core/Entities/Project.cs
src/NodeRed.Core/Interfaces/IProjectService.cs
src/NodeRed.Runtime/Services/ProjectService.cs
src/NodeRed.Runtime/Services/GitService.cs
```

---

### Phase 6: Diagnostics (Nice to Have)
**Priority: LOW** | **Estimated: 0.5 days**

Reference: `packages/node_modules/@node-red/runtime/lib/api/diagnostics.js`

#### 6.1 System Metrics
- [ ] Memory usage (heap, external)
- [ ] CPU usage
- [ ] Uptime
- [ ] .NET runtime version

#### 6.2 Node-RED Metrics
- [ ] Active flows count
- [ ] Active nodes count
- [ ] Message throughput
- [ ] Error count

**Files to Create:**
```
src/NodeRed.Core/Entities/DiagnosticsReport.cs
src/NodeRed.Runtime/Services/DiagnosticsService.cs
```

---

## Implementation Order

Based on criticality and dependencies:

1. **Phase 3: Version Conflict Detection** (0.5 days)
   - Simple to implement
   - Critical for preventing data loss
   
2. **Phase 2: Flow Validation** (1-2 days)
   - Required before deployment
   - Prevents invalid configurations

3. **Phase 1: Authentication & Authorization** (2-3 days)
   - Security critical
   - Required for multi-user support

4. **Phase 4: Multiuser Editing** (2-3 days)
   - Depends on Authentication
   - Enhanced collaboration

5. **Phase 5-6: Projects & Diagnostics** (4 days)
   - Nice to have features
   - Can be deferred

---

## Testing Strategy

Each phase should include:
- Unit tests for all new services
- Integration tests for API endpoints
- Edge case testing (null inputs, invalid data)

---

## Notes

- All implementations should follow existing code patterns in the repository
- Use dependency injection for all services
- Maintain backward compatibility with existing flows
- Reference JavaScript implementation for behavior details
