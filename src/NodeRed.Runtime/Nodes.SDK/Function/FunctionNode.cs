// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Function;

/// <summary>
/// Function node - runs C# code against messages.
/// </summary>
[NodeType("function", "function",
    Category = NodeCategory.Function,
    Color = "#fdd0a2",
    Icon = "fa fa-code",
    Inputs = 1,
    Outputs = 1)]
public class FunctionNode : SdkNodeBase
{
    private Script<NodeMessage?>? _compiledScript;

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddCode("func", "Function", defaultValue: "return msg;", rows: 15)
            .AddNumber("outputs", "Outputs", defaultValue: 1, min: 0, max: 10)
            .AddInfo(@"
Available variables:
- **msg** - The incoming message
- **node** - Node helpers (Send, Log, Warn, Error, NewMessage, CloneMessage)
- **flow** - Flow context (Get, Set)
- **global** - Global context (Get, Set)

The function should return a message to send, or null to stop the message.")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "func", "return msg;" },
        { "outputs", 1 },
        { "initialize", "" },
        { "finalize", "" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Allows C# code to be run against the messages.")
        .AddInput("msg", "object", "The incoming message")
        .AddOutput("msg", "object", "The message returned by the function")
        .Details(@"
The Function node allows you to write C# code that processes messages.

**Available variables:**
- `msg` - The incoming message object
- `msg.payload` - The message payload
- `msg.topic` - The message topic
- `node.Send(port, msg)` - Send a message to an output
- `node.Log(text)` - Log a message
- `flow.Get(key)` / `flow.Set(key, value)` - Flow context
- `global.Get(key)` / `global.Set(key, value)` - Global context

**Example:**
```csharp
var count = (int)(flow.Get(""count"") ?? 0);
count++;
flow.Set(""count"", count);
msg.payload = $""Count: {count}"";
return msg;
```")
        .Build();

    protected override async Task OnInitializeAsync()
    {
        var code = GetConfig("func", "return msg;");
        await CompileScript(code);
    }

    private async Task CompileScript(string code)
    {
        try
        {
            var options = ScriptOptions.Default
                .AddReferences(typeof(NodeMessage).Assembly)
                .AddImports(
                    "System",
                    "System.Collections.Generic",
                    "System.Linq",
                    "System.Text",
                    "NodeRed.Core.Entities");

            _compiledScript = CSharpScript.Create<NodeMessage?>(
                code,
                options,
                typeof(FunctionGlobals));
            
            _compiledScript.Compile();
        }
        catch (Exception ex)
        {
            Error($"Failed to compile function: {ex.Message}");
        }
        
        await Task.CompletedTask;
    }

    protected override async Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        if (_compiledScript == null)
        {
            Error("Function not compiled");
            done();
            return;
        }

        try
        {
            var globals = new FunctionGlobals
            {
                msg = msg,
                node = new NodeHelpers(send, this),
                flow = new ContextHelper(Flow),
                global = new ContextHelper(Global)
            };

            var result = await _compiledScript.RunAsync(globals);
            
            if (result.ReturnValue != null)
            {
                send(0, result.ReturnValue);
            }
            
            done();
        }
        catch (Exception ex)
        {
            Error($"Function error: {ex.Message}", msg);
            done(ex);
        }
    }

    /// <summary>
    /// Global variables available in the function script.
    /// </summary>
    public class FunctionGlobals
    {
        public NodeMessage msg { get; set; } = null!;
        public NodeHelpers node { get; set; } = null!;
        public ContextHelper flow { get; set; } = null!;
        public ContextHelper global { get; set; } = null!;
    }

    /// <summary>
    /// Node helper methods available in the function script.
    /// </summary>
    public class NodeHelpers
    {
        private readonly SendDelegate _send;
        private readonly FunctionNode _node;

        public NodeHelpers(SendDelegate send, FunctionNode node)
        {
            _send = send;
            _node = node;
        }

        public void Send(int port, NodeMessage msg) => _send(port, msg);
        public void Log(string message) => _node.Log(message);
        public void Warn(string message) => _node.Warn(message);
        public void Error(string message) => _node.Error(message);
        public NodeMessage NewMessage(object? payload = null) => _node.NewMessage(payload);
        public NodeMessage CloneMessage(NodeMessage original) => _node.CloneMessage(original);
    }

    /// <summary>
    /// Context helper for flow/global context access.
    /// </summary>
    public class ContextHelper
    {
        private readonly IContextAccessor _accessor;

        public ContextHelper(IContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public object? Get(string key) => _accessor.Get(key);
        public void Set(string key, object? value) => _accessor.Set(key, value);
    }
}
