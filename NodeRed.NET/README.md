# NodeRed.NET

A flow-based programming tool for event-driven applications, built with Blazor Server (.NET 8).

> **Note**: NodeRed.NET is inspired by and derived from [Node-RED](https://nodered.org), created by the OpenJS Foundation. This implementation uses the architecture analysis document as a reference while implementing original code in C#/Blazor following .NET conventions.

## Overview

NodeRed.NET provides a browser-based editor for wiring together flows using nodes. The runtime is built on .NET 8 and uses Blazor Server for the web interface.

### Core Concepts from Node-RED

- **Flows**: Visual representation of message flows between nodes
- **Nodes**: Building blocks that process messages
- **Messages**: Data objects passed between nodes
- **Wiring**: Connections between node outputs and inputs
- **Deploy**: Mechanism to activate flow changes
- **Context**: Shared state (node, flow, global scopes)

## Project Structure

```
NodeRed.NET/
├── src/
│   ├── NodeRed.Util/          # Utilities (logging, events, i18n)
│   ├── NodeRed.Runtime/       # Flow engine and message routing
│   ├── NodeRed.Registry/      # Node type registration and loading
│   ├── NodeRed.EditorApi/     # REST API for editor operations
│   ├── NodeRed.Editor/        # Blazor web UI
│   ├── NodeRed.Nodes.Core/    # Core node implementations
│   └── NodeRed.Server/        # Host application
└── tests/
```

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later

### Building

```bash
cd NodeRed.NET
dotnet build
```

### Running

```bash
cd src/NodeRed.Server
dotnet run
```

Then open your browser to the URL shown in the console output (typically http://localhost:5000).

## Development Status

This project is in active development. Implementation follows the architecture documented in `NODE-RED-ARCHITECTURE-ANALYSIS.md`.

**Current Phase**: Core implementation
- [x] Project structure setup
- [x] Core utilities (events, logging)
- [ ] Runtime engine (flow execution, message routing)
- [ ] Node registry and lifecycle
- [ ] Editor UI (workspace, palette, sidebar)
- [ ] Core nodes (inject, debug, function, etc.)

## Architecture

NodeRed.NET follows a layered architecture inspired by Node-RED's module structure:

1. **Util Layer**: Core utilities, logging, events, i18n
2. **Runtime Layer**: Flow execution, node lifecycle, message passing
3. **Registry Layer**: Node type management and loading
4. **Editor API Layer**: REST endpoints for editor operations
5. **Editor UI Layer**: Blazor-based visual editor
6. **Nodes Layer**: Individual node implementations

## Key Design Decisions

- **Event-Driven**: Uses C# events and delegates instead of Node.js EventEmitter
- **Async/Await**: Leverages C# async patterns instead of callbacks
- **Dependency Injection**: Uses ASP.NET Core DI container
- **Microsoft.Extensions.Logging**: Standard .NET logging abstractions
- **System.Text.Json**: For JSON serialization
- **SignalR**: For WebSocket communication
- **Blazor Server**: For interactive UI with C# code

## Attribution

This software is inspired by and derived from Node-RED:
- Original work: Copyright OpenJS Foundation and other contributors
- Original project: https://github.com/node-red/node-red
- License: Apache 2.0
- Architecture Analysis: See `NODE-RED-ARCHITECTURE-ANALYSIS.md`

Node-RED is a trademark of the OpenJS Foundation. NodeRed.NET is an independent derivative work and is not affiliated with, endorsed by, or sponsored by the OpenJS Foundation or the Node-RED project.

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! This project aims to maintain the core concepts of Node-RED while using idiomatic .NET patterns.
