# Node-RED .NET Example Plugin

This is an example external plugin module demonstrating how to create custom nodes for Node-RED .NET.

## Structure

```
NodeRed.Contrib.Example/
├── NodeRed.Contrib.Example.csproj  # Project file
├── AssemblyInfo.cs                  # Module metadata
├── Nodes.cs                         # Node implementations
└── README.md                        # This file
```

## Nodes Included

| Node | Type | Description |
|------|------|-------------|
| **to upper** | `example-upper` | Converts payload to uppercase |
| **to lower** | `example-lower` | Converts payload to lowercase |
| **humanize** | `example-humanize` | Text transformations using Humanizer library |
| **timer** | `example-timer` | Generates messages at intervals |
| **counter** | `example-counter` | Counts messages with persistent state |

## How to Create a Plugin

### 1. Create a new Class Library project

```bash
dotnet new classlib -n NodeRed.Contrib.MyModule
```

### 2. Reference the SDK

```xml
<ItemGroup>
  <ProjectReference Include="path/to/NodeRed.SDK/NodeRed.SDK.csproj" />
  <ProjectReference Include="path/to/NodeRed.Core/NodeRed.Core.csproj" />
</ItemGroup>
```

Or reference the NuGet packages (when published):

```xml
<ItemGroup>
  <PackageReference Include="NodeRed.SDK" Version="1.0.0" />
</ItemGroup>
```

### 3. Define Module Metadata

In `AssemblyInfo.cs`:

```csharp
using NodeRed.SDK;

[assembly: NodeModule("@myorg/node-red-contrib-mymodule",
    Version = "1.0.0",
    Description = "My custom nodes",
    Author = "My Name",
    MinVersion = "1.0.0")]
```

### 4. Create Nodes

```csharp
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;

[NodeType("my-node", "My Node",
    Category = NodeCategory.Function,
    Color = "#87A980",
    Icon = "fa fa-cube",
    Inputs = 1,
    Outputs = 1)]
public class MyNode : NodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddNumber("timeout", "Timeout", suffix: "seconds", defaultValue: 5)
            .AddSelect("mode", "Mode", new[] {
                ("a", "Option A"),
                ("b", "Option B")
            })
            .AddCheckbox("enabled", "Enabled", defaultValue: true)
            .Build();

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Short description for palette tooltip.")
        .AddInput("msg.payload", "any", "Input data")
        .AddOutput("msg.payload", "any", "Output data")
        .Details("Detailed description of the node's behavior.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Process the message
        var timeout = GetConfig("timeout", 5.0);
        var mode = GetConfig("mode", "a");
        
        // Transform payload
        msg.Payload = $"Processed with mode {mode}";
        
        // Update status indicator
        Status("Processing", StatusFill.Green);
        
        // Send to first output
        send(0, msg);
        
        // Signal completion
        done();
        return Task.CompletedTask;
    }
}
```

### 5. Build and Deploy

```bash
# Build the plugin
dotnet build -c Release

# Copy to plugins folder
cp bin/Release/net10.0/NodeRed.Contrib.MyModule.dll /path/to/NodeRed.Blazor/plugins/
```

## Using Plugin-Specific Dependencies

Plugins can have their own dependencies. They are loaded in an isolated `AssemblyLoadContext`:

```xml
<ItemGroup>
  <PackageReference Include="Humanizer.Core" Version="2.14.1" />
</ItemGroup>
```

The SDK and Core assemblies are shared with the host to ensure type compatibility.
Plugin-specific dependencies are isolated, so different plugins can use different versions of the same library.

## Node Lifecycle

| Method | When Called |
|--------|-------------|
| `OnInitializeAsync()` | When flow is deployed |
| `OnInputAsync(msg, send, done)` | When a message arrives |
| `OnCloseAsync()` | When flow is stopped or node is removed |

## Available Helpers

| Helper | Description |
|--------|-------------|
| `GetConfig<T>(name, default)` | Get configuration value |
| `NewMessage(payload, topic)` | Create a new message |
| `CloneMessage(msg)` | Clone an existing message |
| `Status(text, fill, shape)` | Update status indicator |
| `ClearStatus()` | Clear status indicator |
| `Log/Warn/Error/Debug/Trace()` | Logging methods |
| `Flow.Get/Set()` | Flow-scoped context storage |
| `Global.Get/Set()` | Global context storage |

## Property Types

| Method | Description |
|--------|-------------|
| `AddText()` | Text input |
| `AddNumber()` | Numeric input with min/max/step |
| `AddSelect()` | Dropdown selection |
| `AddCheckbox()` | Boolean toggle |
| `AddTextArea()` | Multi-line text |
| `AddCode()` | Code editor |
| `AddPassword()` | Password input (hidden) |
| `AddInfo()` | Static information display |
