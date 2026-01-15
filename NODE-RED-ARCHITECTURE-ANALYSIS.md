# Node-RED Architecture Analysis

**Document Version:** 1.0  
**Node-RED Version Analyzed:** 4.1.3  
**Analysis Date:** January 15, 2026  
**Purpose:** Comprehensive architectural analysis of Node-RED for reference and understanding

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [High-Level Architecture](#high-level-architecture)
3. [Module Structure](#module-structure)
4. [Core Components Deep Dive](#core-components-deep-dive)
5. [Data Flow and Message Passing](#data-flow-and-message-passing)
6. [Editor Architecture](#editor-architecture)
7. [Node Lifecycle](#node-lifecycle)
8. [Storage and Persistence](#storage-and-persistence)
9. [Security Model](#security-model)
10. [Extension Points](#extension-points)
11. [Key Design Patterns](#key-design-patterns)
12. [Technology Stack](#technology-stack)
13. [Deployment Considerations](#deployment-considerations)

---

## Executive Summary

Node-RED is a flow-based programming tool built on Node.js, designed for wiring together hardware devices, APIs, and online services in new and interesting ways. This analysis examines its architecture, components, and design patterns.

### Key Architectural Characteristics

- **Modular Monorepo**: Uses Lerna/npm workspaces with 6 core packages
- **Event-Driven**: Heavily relies on EventEmitter pattern for inter-component communication
- **Flow-Based Programming**: Implements classic FBP paradigm with nodes, wires, and messages
- **Browser-Based Editor**: Full-featured visual editor using jQuery, D3.js, and custom JavaScript
- **Extensible**: Plugin architecture for custom nodes, storage backends, and authentication

### Statistics

- **Total JavaScript Files**: 302+ core files
- **Core Packages**: 6 (@node-red/util, runtime, registry, nodes, editor-api, editor-client)
- **Core Nodes**: 50+ built-in node types
- **Lines of Code**: ~100,000+ (estimated)
- **Dependencies**: 85+ npm packages

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Browser (Client)                         │
│  ┌────────────────────────────────────────────────────────┐ │
│  │         Editor UI (@node-red/editor-client)            │ │
│  │  - Canvas/Workspace  - Node Palette  - Sidebar         │ │
│  │  - Flow Editor       - Configuration  - Debug Panel    │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                            │ HTTP/WebSocket
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                   Node.js Server (Runtime)                   │
│  ┌────────────────────────────────────────────────────────┐ │
│  │         Editor API (@node-red/editor-api)              │ │
│  │  - REST Endpoints  - Authentication  - WebSocket       │ │
│  └────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────┐ │
│  │            Runtime (@node-red/runtime)                 │ │
│  │  - Flow Engine     - Node Registry    - Context Store  │ │
│  │  - Message Router  - Settings         - Storage        │ │
│  └────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────┐ │
│  │              Utility (@node-red/util)                  │ │
│  │  - Events  - Logging  - i18n  - Hooks                 │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                            │
                            ↓
┌─────────────────────────────────────────────────────────────┐
│              File System / External Systems                  │
│  - flows.json  - credentials  - node_modules  - libraries   │
└─────────────────────────────────────────────────────────────┘
```

---

## Module Structure

### Package Organization

Node-RED uses a monorepo structure with packages organized under `packages/node_modules/@node-red/`:

#### 1. @node-red/util
**Purpose**: Core utilities used by all other modules  
**Key Files**:
- `lib/events.js` - EventEmitter wrapper for runtime events
- `lib/log.js` - Logging system with multiple levels (fatal, error, warn, info, debug, trace, audit, metric)
- `lib/i18n.js` - Internationalization support
- `lib/hooks.js` - Hook system for extending behavior
- `lib/util.js` - General utility functions

**Dependencies**: None (base layer)

#### 2. @node-red/runtime
**Purpose**: Core flow execution engine  
**Key Directories**:
- `lib/api/` - Internal APIs for runtime operations
- `lib/flows/` - Flow and subflow management
- `lib/nodes/` - Node lifecycle, credentials, context
- `lib/storage/` - Persistence layer abstraction
- `lib/library/` - Flow library management

**Key Files**:
- `lib/index.js` - Main runtime API
- `lib/flows/Flow.js` - Individual flow execution
- `lib/nodes/Node.js` - Base node class
- `lib/nodes/index.js` - Node management

**Dependencies**: @node-red/util, @node-red/registry

#### 3. @node-red/registry
**Purpose**: Node type registration and loading  
**Key Files**:
- `lib/registry.js` - Central registry for node types
- `lib/loader.js` - Loads nodes from file system
- `lib/installer.js` - Handles node installation/removal
- `lib/library.js` - Node example library

**Dependencies**: @node-red/util

#### 4. @node-red/nodes
**Purpose**: Core node implementations  
**Structure**:
- `core/common/` - Common nodes (inject, debug, complete, catch, status, link, comment)
- `core/function/` - Function nodes (function, switch, change, range, template, delay, trigger, exec)
- `core/network/` - Network nodes (mqtt, http, websocket, tcp, udp)
- `core/parsers/` - Parser nodes (JSON, XML, YAML, HTML, CSV)
- `core/sequence/` - Sequence nodes (split, join, sort, batch)
- `core/storage/` - Storage nodes (file, watch)

**Node Count**: 50+ core nodes

#### 5. @node-red/editor-api
**Purpose**: REST API and WebSocket server for editor  
**Key Directories**:
- `lib/admin/` - Admin endpoints (flows, nodes, settings)
- `lib/auth/` - Authentication and authorization
- `lib/editor/` - Editor-specific endpoints

**Key Files**:
- `lib/index.js` - Main API setup
- `lib/admin/flows.js` - Flow CRUD operations
- `lib/admin/nodes.js` - Node management endpoints
- `lib/auth/index.js` - Authentication strategies

**Dependencies**: @node-red/util, @node-red/runtime

#### 6. @node-red/editor-client
**Purpose**: Browser-based visual editor  
**Key Directories**:
- `src/js/` - Main JavaScript source
- `src/sass/` - SCSS stylesheets
- `templates/` - HTML templates

**Key JavaScript Modules**:
- `src/js/red.js` - Main RED object
- `src/js/ui/` - UI components (workspace, palette, sidebar, tray)
- `src/js/nodes.js` - Client-side node management
- `src/js/history.js` - Undo/redo system
- `src/js/deploy.js` - Deploy mechanism

---

## Core Components Deep Dive

### 1. Flow Engine (Runtime)

**Location**: `@node-red/runtime/lib/flows/`

**Key Classes**:

```javascript
// Flow.js - Represents a single flow or tab
class Flow {
    constructor(parent, globalFlow, flow) {
        this.id = flow.id
        this.type = flow.type
        this.nodes = {}
        this.subflows = {}
        this.env = flow.env
        // ... initialization
    }
    
    start(configDiff) {
        // Start all nodes in the flow
        // Handle node initialization order
        // Set up message routing
    }
    
    stop(stopList) {
        // Stop nodes in reverse order
        // Clean up resources
    }
}
```

**Flow Execution Model**:
1. Flows are parsed from JSON configuration
2. Nodes are instantiated in dependency order
3. Wires define message routing between nodes
4. Messages flow asynchronously through the graph
5. Context provides shared state

### 2. Node Lifecycle

**States**: `loading → stopped → started → stopped → unloaded`

**Lifecycle Hooks**:
```javascript
// Node definition structure
module.exports = function(RED) {
    function MyNode(config) {
        RED.nodes.createNode(this, config);
        
        // Node-specific initialization
        this.on('input', function(msg, send, done) {
            // Process message
            send(msg);
            done();
        });
        
        this.on('close', function(removed, done) {
            // Cleanup
            done();
        });
    }
    
    RED.nodes.registerType("my-node", MyNode);
}
```

**Key Methods**:
- `createNode(node, config)` - Initialize node
- `send(msg)` - Send message to wired nodes
- `done()` - Signal completion (for async operations)
- `error(err, msg)` - Report error
- `status({fill, shape, text})` - Update node status

### 3. Message Structure

**Base Message Object**:
```javascript
{
    "_msgid": "unique-id",        // Unique message identifier
    "topic": "string",             // Optional message topic
    "payload": any,                // Message payload (any type)
    "_event": "event-name",        // Internal event name (optional)
    // Additional properties can be added dynamically
}
```

**Message Flow**:
1. Message created by input node (e.g., inject)
2. Message passed through wired nodes
3. Each node can modify, split, join, or filter messages
4. Message ends at output nodes (e.g., debug, http response)

### 4. Context Store

**Purpose**: Shared state management across nodes

**Scopes**:
- **Node Context**: Private to a single node instance
- **Flow Context**: Shared within a flow/tab
- **Global Context**: Shared across all flows

**API**:
```javascript
// Get/Set context
node.context().get('key')
node.context().set('key', value)
flow.context().get('key')
global.context().get('key')

// Async context (for persistent stores)
node.context().get('key', 'storeName', callback)
node.context().set('key', value, 'storeName', callback)
```

**Storage Plugins**:
- `memory` (default) - In-memory only
- `localfilesystem` - Persistent to disk
- Custom plugins can be added

### 5. Event System

**Location**: `@node-red/util/lib/events.js`

**Core Events**:
```javascript
// Runtime lifecycle
"runtime:started"
"runtime:stopped"
"flows:started"
"flows:stopped"

// Node events
"node-red:runtime-event"
"registry:node-added"
"registry:node-removed"

// Flow events
"flows:change"
"flows:restarted"

// Deprecated (with warnings)
"nodes-stopped" → "flows:stopped"
"nodes-started" → "flows:started"
```

**Usage Pattern**:
```javascript
const RED = require('@node-red/util').events;
RED.on('flows:started', function() {
    console.log('Flows have started');
});
```

---

## Data Flow and Message Passing

### Message Routing

**Wire Structure**:
```javascript
// In flow JSON
{
    "id": "node-1",
    "type": "inject",
    "wires": [["node-2", "node-3"]]  // Send to multiple nodes
}
```

**Routing Logic**:
1. Node calls `send(msg)` or `send([msg1, msg2])` for multiple outputs
2. Runtime looks up wired nodes from wire configuration
3. Messages are cloned (using rfdc - really fast deep clone)
4. Each wired node receives message via its `input` event
5. Asynchronous delivery (non-blocking)

### Message Cloning

**Why**: Prevent unintended side effects when message is sent to multiple nodes

**Implementation**: Uses `rfdc` library for fast deep cloning
```javascript
const clone = require('rfdc')({circles: true});
const clonedMsg = clone(originalMsg);
```

### Flow Control

**Back Pressure**: Not implemented at the flow level
**Rate Limiting**: Done at individual node level (e.g., delay node)
**Error Handling**: 
- Catch nodes can handle errors from specific nodes
- Global error handler for uncaught errors

---

## Editor Architecture

### Client-Side Structure

**Main Components**:

1. **Workspace** (`src/js/ui/workspace.js`)
   - SVG canvas for flow visualization
   - D3.js for rendering and interaction
   - Zoom, pan, grid snapping

2. **Palette** (`src/js/ui/palette.js`)
   - Left sidebar showing available node types
   - Categorized by type (input, output, function, etc.)
   - Drag-and-drop to workspace

3. **Sidebar** (`src/js/ui/sidebar.js`)
   - Right panel with tabs
   - Info, Debug, Config, Context panels
   - Extensible with custom tabs

4. **Tray System** (`src/js/ui/tray.js`)
   - Sliding panel for node configuration
   - Used for node edit dialogs
   - Stack multiple trays for nested config

5. **Deploy Button** (`src/js/deploy.js`)
   - Triggers flow deployment
   - Options: Full, Modified Nodes, Modified Flows
   - Shows dirty state indicator

### Editor-Server Communication

**REST API Endpoints**:
```
GET    /flows              - Get all flows
POST   /flows              - Deploy flows
GET    /nodes              - List installed nodes
POST   /nodes              - Install node
DELETE /nodes/:module      - Remove node
GET    /settings           - Get runtime settings
POST   /auth/token         - Authenticate
GET    /library/*          - Access flow library
```

**WebSocket** (`/comms`):
- Real-time status updates
- Deploy notifications
- Debug message streaming
- Multiplayer collaboration (experimental)

### Node Configuration UI

**HTML Definition**:
```html
<script type="text/html" data-template-name="inject">
    <div class="form-row">
        <label for="node-input-topic"><i class="fa fa-tasks"></i> Topic</label>
        <input type="text" id="node-input-topic">
    </div>
</script>

<script type="text/javascript">
    RED.nodes.registerType('inject', {
        category: 'input',
        color: '#a6bbcf',
        defaults: {
            topic: {value: ""}
        },
        icon: "inject.svg",
        oneditprepare: function() {
            // Setup UI
        },
        oneditsave: function() {
            // Validate and save
        }
    });
</script>
```

---

## Node Lifecycle

### Registration Phase

1. **Discovery**: Runtime scans `node_modules` for Node-RED nodes
2. **Loading**: Each node's `.js` file is `require()`d
3. **Registration**: Node calls `RED.nodes.registerType()`
4. **Validation**: Schema validation of node definition

### Runtime Phase

```
┌──────────────┐
│   Created    │ ← Constructor called
└──────┬───────┘
       │
       ↓
┌──────────────┐
│   Starting   │ ← 'input' handler registered
└──────┬───────┘
       │
       ↓
┌──────────────┐
│   Running    │ ← Processing messages
└──────┬───────┘
       │
       ↓
┌──────────────┐
│   Stopping   │ ← 'close' event emitted
└──────┬───────┘
       │
       ↓
┌──────────────┐
│   Destroyed  │ ← Cleanup complete
└──────────────┘
```

### Node Configuration vs Instance

**Configuration**: Defines node behavior (stored in flow JSON)
**Instance**: Runtime object executing the node logic

One configuration can have multiple instances (e.g., copied nodes).

---

## Storage and Persistence

### Storage Interface

**Location**: `@node-red/runtime/lib/storage/`

**Key Operations**:
```javascript
// Storage interface
{
    init: function(settings),
    getFlows: function(),
    saveFlows: function(flows),
    getCredentials: function(),
    saveCredentials: function(credentials),
    getSettings: function(),
    saveSettings: function(settings),
    getSessions: function(),
    saveSessions: function(sessions),
    getLibraryEntry: function(type, path),
    saveLibraryEntry: function(type, path, meta, body)
}
```

### Default Implementation (Local Filesystem)

**Files**:
- `flows.json` - Flow configurations
- `flows_cred.json` - Encrypted credentials
- `.config.json` - Runtime settings
- `.sessions.json` - User sessions

**Location**: `~/.node-red/` by default

### Custom Storage

Plugins can implement the storage interface:
```javascript
// In settings.js
storageModule: require('./my-storage')
```

Examples: MongoDB, PostgreSQL, Git-based storage

---

## Security Model

### Authentication

**Strategies Supported**:
- **Local**: Username/password (bcrypt hashed)
- **OAuth2**: Third-party authentication
- **Token-based**: Bearer tokens

**Configuration** (settings.js):
```javascript
adminAuth: {
    type: "credentials",
    users: [{
        username: "admin",
        password: "$2a$...",  // bcrypt hash
        permissions: "*"
    }]
}
```

### Authorization

**Permission Levels**:
- `*` - Full access
- `read` - Read-only access
- Custom permissions can be defined

**Editor API Protection**: All endpoints check authentication

### Credential Encryption

**Mechanism**:
- Credentials encrypted with AES-256
- Key derived from `credentialSecret` in settings
- Stored separately from flow configuration

**Credential Structure**:
```javascript
{
    "node-id": {
        "username": "encrypted-value",
        "password": "encrypted-value"
    }
}
```

---

## Extension Points

### 1. Custom Nodes

**Structure**:
```
my-node/
├── package.json       # Node-RED metadata
├── my-node.js        # Runtime implementation
└── my-node.html      # Editor UI definition
```

**package.json**:
```json
{
    "node-red": {
        "nodes": {
            "my-node": "my-node.js"
        }
    }
}
```

### 2. Storage Plugins

Implement the storage interface and register in settings.js

### 3. Authentication Strategies

Use Passport.js strategies:
```javascript
adminAuth: {
    type: "strategy",
    strategy: {
        name: "oauth2",
        // ... strategy config
    }
}
```

### 4. Context Store Plugins

Register custom context stores:
```javascript
contextStorage: {
    default: "memory",
    custom: {
        module: require("my-context-store"),
        config: { /* options */ }
    }
}
```

### 5. Editor Plugins

Add custom sidebar tabs, workspace tools:
```javascript
RED.plugins.registerPlugin("my-plugin", {
    type: "node-red-dashboard-plugin",
    scripts: ["/path/to/script.js"]
});
```

---

## Key Design Patterns

### 1. Event-Driven Architecture

**Pattern**: Loose coupling through events
**Implementation**: Node.js EventEmitter
**Usage**: Runtime events, node communication

### 2. Registry Pattern

**Pattern**: Central registry for node types
**Implementation**: `@node-red/registry`
**Benefits**: Dynamic loading, type management

### 3. Factory Pattern

**Pattern**: Node instantiation
**Implementation**: `RED.nodes.createNode()`
**Benefits**: Consistent node creation, lifecycle management

### 4. Observer Pattern

**Pattern**: Node status updates, message flow
**Implementation**: Event listeners, wire connections
**Benefits**: Reactive updates, decoupled components

### 5. Strategy Pattern

**Pattern**: Pluggable authentication, storage
**Implementation**: Interface-based plugins
**Benefits**: Flexibility, extensibility

### 6. Command Pattern

**Pattern**: Deploy operations, undo/redo
**Implementation**: History manager
**Benefits**: Reversible actions, audit trail

---

## Technology Stack

### Server-Side

| Component | Technology | Version | Purpose |
|-----------|-----------|---------|---------|
| Runtime | Node.js | 18.5+ | JavaScript runtime |
| Web Server | Express.js | 4.22.1 | HTTP server |
| WebSocket | ws | 7.5.10 | Real-time communication |
| Authentication | Passport.js | 0.7.0 | Auth strategies |
| Encryption | bcryptjs | 3.0.2 | Password hashing |
| i18n | i18next | 24.2.3 | Internationalization |
| MQTT | mqtt.js | 5.11.0 | MQTT client |
| JSON Query | JSONata | 2.0.6 | JSON transformation |

### Client-Side

| Component | Technology | Version | Purpose |
|-----------|-----------|---------|---------|
| UI Framework | jQuery | 3.x | DOM manipulation |
| Visualization | D3.js | 7.x | SVG rendering |
| Icons | Font Awesome | 4.7 | Icon font |
| ACE Editor | Ace | 1.x | Code editor |
| Markdown | Marked | 4.3.0 | Markdown rendering |

### Build Tools

| Tool | Purpose |
|------|---------|
| Grunt | Build automation |
| SASS | CSS preprocessing |
| UglifyJS | JavaScript minification |
| JSHint | Linting |

---

## Deployment Considerations

### Installation Methods

1. **npm Global Install**:
   ```bash
   npm install -g --unsafe-perm node-red
   ```

2. **Local Install**:
   ```bash
   npm install node-red
   node node_modules/node-red/red.js
   ```

3. **Docker**:
   ```bash
   docker run -p 1880:1880 nodered/node-red
   ```

### Configuration

**Primary Config**: `settings.js`
- Server port and bind address
- Authentication settings
- Storage configuration
- Logging levels
- Function node settings
- Editor theme

**Environment Variables**:
- `NODE_RED_HOME` - User directory
- `PORT` - Server port
- `NODE_RED_CREDENTIAL_SECRET` - Credential encryption key

### Performance Tuning

**Node.js Options**:
- `--max-old-space-size` - Increase heap size
- `--expose-gc` - Manual garbage collection

**Flow Design**:
- Avoid tight loops
- Use rate limiting for high-frequency sources
- Minimize function node complexity
- Use subflows for reusability

### Scalability

**Horizontal Scaling**: Not natively supported (single process)
**Workarounds**:
- Multiple instances with load balancer
- Message queue integration (MQTT, RabbitMQ)
- Distributed flows architecture

---

## Advanced Features

### Subflows

**Purpose**: Reusable flow components  
**Structure**: Like a function with inputs/outputs  
**Benefits**: Encapsulation, reusability

**Definition**:
```json
{
    "type": "subflow",
    "id": "subflow-id",
    "name": "My Subflow",
    "info": "Description",
    "in": [{"x": 50, "y": 50, "wires": [{"id": "internal-node"}]}],
    "out": [{"x": 250, "y": 50, "wires": [{"id": "internal-node", "port": 0}]}]
}
```

### Projects

**Purpose**: Git-based flow management  
**Features**:
- Version control integration
- Branch management
- Merge flows
- Remote repositories

**Location**: `~/.node-red/projects/`

### Function Node

**Purpose**: Custom JavaScript logic in flows  
**Environment**: Sandboxed JavaScript VM  
**Available APIs**:
- `node.*` - Node methods
- `context.*` - Context access
- `flow.*` - Flow context
- `global.*` - Global context
- `RED.*` - Limited RED API

**External Modules**: Can be whitelisted in settings

### Link Nodes

**Purpose**: Virtual wires across flows  
**Types**: `link in`, `link out`, `link call`  
**Benefits**: Clean flow organization, cross-flow communication

---

## Performance Characteristics

### Message Throughput

- **Typical**: 1,000-10,000 msg/sec per flow
- **Factors**: Node complexity, message size, I/O operations
- **Bottlenecks**: Synchronous operations, function node overhead

### Memory Usage

- **Base**: ~50-100 MB
- **Per Flow**: +1-5 MB
- **Per Node**: +100 KB - 1 MB
- **Messages**: Transient (GC'd after processing)

### Startup Time

- **Base Runtime**: <1 second
- **With Flows**: 1-5 seconds (depends on node count)
- **Node Loading**: Parallel where possible

---

## Comparison with Other Platforms

### vs. Apache NiFi
- **Lighter weight**: Node-RED is simpler, NiFi is enterprise-grade
- **Deployment**: Node-RED easier to deploy
- **Scalability**: NiFi better for large-scale data flows

### vs. Integromat/Zapier
- **Self-hosted**: Node-RED runs on-premise
- **Customization**: Node-RED fully programmable
- **Cost**: Node-RED is free and open-source

### vs. AWS Step Functions
- **Infrastructure**: Node-RED on any server vs. AWS-only
- **Visual**: Both visual, Node-RED more flexible
- **Integration**: Step Functions better AWS integration

---

## Conclusions and Recommendations

### Strengths

1. **Low barrier to entry**: Visual programming accessible to non-programmers
2. **Extensive ecosystem**: 4,000+ community nodes available
3. **Flexibility**: Runs on Raspberry Pi to cloud servers
4. **Active community**: Strong community support and development
5. **IoT focus**: Excellent for device integration and automation

### Weaknesses

1. **Single-threaded**: Node.js single-process limitations
2. **No native HA**: High availability requires external tools
3. **Limited debugging**: Flow debugging can be challenging
4. **State management**: No built-in distributed state
5. **Version control**: Flow JSON can be hard to merge

### Best Use Cases

- **IoT Integration**: Connecting sensors, devices, APIs
- **Rapid Prototyping**: Quick proof-of-concepts
- **Data Transformation**: ETL for small-to-medium data
- **Home Automation**: Smart home integration
- **Webhook Processing**: API integration and automation

### When NOT to Use

- **High-throughput data**: >10K messages/sec consistently
- **Complex business logic**: Better suited to traditional code
- **Distributed systems**: No native clustering support
- **Strict governance**: Limited enterprise features
- **Real-time critical**: Millisecond-level latency requirements

---

## Appendix A: File Structure Reference

```
node-red/
├── packages/
│   └── node_modules/
│       └── @node-red/
│           ├── editor-api/
│           │   └── lib/
│           │       ├── admin/      # Admin API endpoints
│           │       ├── auth/       # Authentication
│           │       └── editor/     # Editor endpoints
│           ├── editor-client/
│           │   ├── src/
│           │   │   ├── js/         # Client JavaScript
│           │   │   └── sass/       # Stylesheets
│           │   └── templates/      # HTML templates
│           ├── nodes/
│           │   └── core/           # Core node implementations
│           │       ├── common/
│           │       ├── function/
│           │       ├── network/
│           │       ├── parsers/
│           │       ├── sequence/
│           │       └── storage/
│           ├── registry/
│           │   └── lib/            # Node registry logic
│           ├── runtime/
│           │   └── lib/
│           │       ├── api/        # Runtime API
│           │       ├── flows/      # Flow management
│           │       ├── nodes/      # Node lifecycle
│           │       └── storage/    # Storage abstraction
│           └── util/
│               └── lib/            # Utility functions
├── test/                           # Test suites
└── red.js                         # Main entry point
```

---

## Appendix B: Key API Reference

### Runtime API

```javascript
const RED = require("node-red");

// Initialize
RED.init(server, settings);

// Start runtime
RED.start().then(() => {
    console.log('Node-RED started');
});

// Stop runtime
RED.stop().then(() => {
    console.log('Node-RED stopped');
});
```

### Node API

```javascript
// In node implementation
module.exports = function(RED) {
    function MyNode(config) {
        RED.nodes.createNode(this, config);
        
        // Receive messages
        this.on('input', function(msg, send, done) {
            // Process
            msg.payload = "transformed";
            send(msg);
            done();
        });
        
        // Cleanup
        this.on('close', function(done) {
            // Close connections, clear timers
            done();
        });
    }
    
    RED.nodes.registerType("my-node", MyNode);
};
```

### Context API

```javascript
// Node context
let value = this.context().get('key');
this.context().set('key', value);

// Flow context
let flowValue = this.context().flow.get('key');
this.context().flow.set('key', value);

// Global context
let globalValue = this.context().global.get('key');
this.context().global.set('key', value);
```

---

## Appendix C: Message Flow Example

```
┌────────────┐      ┌────────────┐      ┌────────────┐
│   Inject   │─────→│  Function  │─────→│   Debug    │
│  (Timer)   │      │ (Transform)│      │  (Output)  │
└────────────┘      └────────────┘      └────────────┘
      │                    │                    │
      │ {payload: 1}       │                    │
      ├───────────────────→│                    │
      │                    │ {payload: 2}       │
      │                    ├───────────────────→│
      │                    │                    │
```

**Flow JSON**:
```json
[
    {
        "id": "inject1",
        "type": "inject",
        "topic": "",
        "payload": "1",
        "repeat": "5",
        "wires": [["function1"]]
    },
    {
        "id": "function1",
        "type": "function",
        "func": "msg.payload = parseInt(msg.payload) + 1; return msg;",
        "wires": [["debug1"]]
    },
    {
        "id": "debug1",
        "type": "debug",
        "name": "Output"
    }
]
```

---

## Document Metadata

**Author**: Architecture Analysis Team  
**Last Updated**: January 15, 2026  
**Node-RED Version**: 4.1.3  
**License**: This document may be freely shared with attribution to Node-RED project  

**References**:
- Node-RED Official Docs: https://nodered.org/docs/
- Node-RED GitHub: https://github.com/node-red/node-red
- Node-RED API Docs: https://nodered.org/docs/api/

---

*End of Architecture Analysis*
