# NodeRed.NET

A flow-based programming tool for event-driven applications, built with Blazor Server (.NET 8).

> **Note**: NodeRed.NET is inspired by and derived from [Node-RED](https://nodered.org), created by the OpenJS Foundation. This is an independent implementation in C#/Blazor that maintains the core concepts of flow-based visual programming while leveraging .NET technologies.

## Overview

NodeRed.NET provides a browser-based editor for wiring together flows using nodes. The runtime is built on .NET 8 and uses Blazor Server for the web interface.

### Core Concepts Maintained from Node-RED

- **Flows**: Visual representation of message flows between nodes
- **Nodes**: Building blocks that process messages
- **Messages**: Data objects passed between nodes
- **Wiring**: Connections between node outputs and inputs
- **Deploy**: Mechanism to activate flow changes

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

Then open your browser to the URL shown in the console output.

## Development Status

This project is in early development. Current phase:

- [x] Project structure setup
- [ ] Core utilities implementation
- [ ] Runtime engine
- [ ] Editor UI
- [ ] Core nodes

## Architecture

NodeRed.NET follows a layered architecture inspired by Node-RED's module structure:

1. **Util Layer**: Core utilities, logging, events
2. **Runtime Layer**: Flow execution, node lifecycle, message passing
3. **Registry Layer**: Node type management
4. **Editor API Layer**: REST endpoints for editor operations
5. **Editor UI Layer**: Blazor-based visual editor
6. **Nodes Layer**: Individual node implementations

## Attribution

This software is derived from Node-RED:
- Original work: Copyright OpenJS Foundation and other contributors
- Original project: https://github.com/node-red/node-red
- License: Apache 2.0

Node-RED is a trademark of the OpenJS Foundation. NodeRed.NET is an independent derivative work and is not affiliated with, endorsed by, or sponsored by the OpenJS Foundation or the Node-RED project.

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please read the contribution guidelines before submitting pull requests.
