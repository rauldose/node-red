// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Nodes.Function;

/// <summary>
/// Function node - allows custom C# code to process messages.
/// Uses Roslyn Scripting for full C# code execution including variables, loops, conditionals, etc.
/// </summary>
/// <remarks>
/// Security Note: This node executes user-provided C# code. Roslyn scripting can execute
/// arbitrary code. Care should be taken when allowing untrusted users to create function nodes.
/// Consider implementing additional restrictions for multi-tenant scenarios.
/// </remarks>
public class FunctionNode : NodeBase
{
    private ScriptRunner<object?>? _compiledFunction;
    private ScriptOptions? _scriptOptions;

    public override NodeDefinition Definition => new()
    {
        Type = "function",
        DisplayName = "function",
        Category = NodeCategory.Function,
        Color = "#fdd0a2",
        Icon = "function",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "name", "" },
            { "func", "return msg;" },
            { "outputs", 1 },
            { "timeout", 0 },
            { "noerr", 0 },
            { "initialize", "" },
            { "finalize", "" }
        },
        HelpText = @"A C# code block to process messages. 
Available variables: msg (NodeMessage), node (helpers), flow (context), global (context).

Examples:
- return msg;  // pass through
- msg.payload = msg.payload.ToString().ToUpper(); return msg;  // transform
- var count = (int)(flow.Get(""count"") ?? 0); count++; flow.Set(""count"", count); msg.payload = count; return msg;  // use context
- if (msg.payload is int num && num > 10) { return msg; } return null;  // conditional
- for (int i = 0; i < 3; i++) { node.Send(node.NewMessage(i)); } return null;  // loop with send"
    };

    public override async Task InitializeAsync(FlowNode config, INodeContext context)
    {
        await base.InitializeAsync(config, context);
        
        _scriptOptions = ScriptOptions.Default
            .AddReferences(typeof(object).Assembly)
            .AddReferences(typeof(Enumerable).Assembly)
            .AddReferences(typeof(NodeMessage).Assembly)
            .AddImports("System", "System.Collections.Generic", "System.Linq", "System.Text");
        
        var func = GetConfig("func", "return msg;");
        try
        {
            var script = CSharpScript.Create<object?>(
                func,
                _scriptOptions,
                typeof(FunctionGlobals));
            
            _compiledFunction = script.CreateDelegate();
        }
        catch (Exception ex)
        {
            Log($"Function parse error: {ex.Message}", LogLevel.Error);
        }

        // Run initialize function if present
        var initialize = GetConfig("initialize", "");
        if (!string.IsNullOrWhiteSpace(initialize))
        {
            try
            {
                await CSharpScript.RunAsync(initialize, _scriptOptions);
            }
            catch (Exception ex)
            {
                Log($"Initialize error: {ex.Message}", LogLevel.Error);
            }
        }
    }

    public override async Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        try
        {
            if (_compiledFunction == null)
            {
                // No valid function, pass through
                Send(message.Clone());
                Done();
                return;
            }

            var globals = new FunctionGlobals(this, message);

            var result = await _compiledFunction(globals);

            if (result is NodeMessage resultMsg)
            {
                Send(resultMsg);
            }
            else if (result is NodeMessage[] messages)
            {
                // Support returning array for multiple outputs
                for (int i = 0; i < messages.Length; i++)
                {
                    if (messages[i] != null)
                    {
                        Send(i, messages[i]);
                    }
                }
            }
            else if (result is IEnumerable<NodeMessage> messageList)
            {
                int i = 0;
                foreach (var m in messageList)
                {
                    if (m != null)
                    {
                        Send(i++, m);
                    }
                }
            }
            else if (result != null)
            {
                // If result is not a message, set it as payload
                var newMsg = message.Clone();
                newMsg.Payload = result;
                Send(newMsg);
            }
            // null result = drop message

            Done();
        }
        catch (Exception ex)
        {
            Log($"Function error: {ex.Message}", LogLevel.Error);
            SetStatus(NodeStatus.Error(ex.Message));
            Done(ex);
        }
    }

    public override async Task CloseAsync()
    {
        // Run finalize function if present
        var finalize = GetConfig("finalize", "");
        if (!string.IsNullOrWhiteSpace(finalize) && _scriptOptions != null)
        {
            try
            {
                await CSharpScript.RunAsync(finalize, _scriptOptions);
            }
            catch (Exception ex)
            {
                Log($"Finalize error: {ex.Message}", LogLevel.Error);
            }
        }

        await base.CloseAsync();
    }

    /// <summary>
    /// Globals class that provides variables to the script.
    /// Using lowercase property names to match Node-RED JavaScript conventions.
    /// </summary>
    public class FunctionGlobals
    {
        private readonly FunctionNode _node;
        
        public FunctionGlobals(FunctionNode node, NodeMessage message)
        {
            _node = node;
            msg = message;
            this.node = new FunctionNodeHelper(node, message);
            flow = new ContextAccessor(
                key => node.Context.GetFlowContext<object>(key), 
                (key, val) => node.Context.SetFlowContext(key, val));
            global = new ContextAccessor(
                key => node.Context.GetGlobalContext<object>(key), 
                (key, val) => node.Context.SetGlobalContext(key, val));
        }

        /// <summary>The incoming message (Node-RED style lowercase)</summary>
        public NodeMessage msg { get; set; }
        
        /// <summary>Node helper functions</summary>
        public FunctionNodeHelper node { get; }
        
        /// <summary>Flow context accessor</summary>
        public ContextAccessor flow { get; }
        
        /// <summary>Global context accessor</summary>
        public ContextAccessor global { get; }
    }

    /// <summary>
    /// Helper class providing node-specific functions.
    /// </summary>
    public class FunctionNodeHelper
    {
        private readonly FunctionNode _node;
        private readonly NodeMessage _currentMessage;

        public FunctionNodeHelper(FunctionNode node, NodeMessage currentMessage)
        {
            _node = node;
            _currentMessage = currentMessage;
        }

        /// <summary>
        /// Sends a message to the next node(s).
        /// </summary>
        public void Send(NodeMessage message, int port = 0)
        {
            _node.Send(port, message);
        }

        /// <summary>
        /// Logs a message.
        /// </summary>
        public void Log(object? message)
        {
            _node.Log(message?.ToString() ?? "null", LogLevel.Info);
        }

        /// <summary>
        /// Logs a warning.
        /// </summary>
        public void Warn(object? message)
        {
            _node.Log(message?.ToString() ?? "null", LogLevel.Warning);
        }

        /// <summary>
        /// Logs an error.
        /// </summary>
        public void Error(object? message)
        {
            _node.Log(message?.ToString() ?? "null", LogLevel.Error);
        }

        /// <summary>
        /// Sets the node status.
        /// </summary>
        public void Status(string text, string color = "grey", string shape = "dot")
        {
            var statusColor = color.ToLowerInvariant() switch
            {
                "red" => StatusColor.Red,
                "green" => StatusColor.Green,
                "yellow" => StatusColor.Yellow,
                "blue" => StatusColor.Blue,
                _ => StatusColor.Grey
            };

            var statusShape = shape.ToLowerInvariant() switch
            {
                "ring" => StatusShape.Ring,
                _ => StatusShape.Dot
            };

            _node.SetStatus(new NodeStatus
            {
                Text = text,
                Color = statusColor,
                Shape = statusShape
            });
        }

        /// <summary>
        /// Clears the node status.
        /// </summary>
        public void Status()
        {
            _node.SetStatus(NodeStatus.Clear());
        }

        /// <summary>
        /// Gets the node ID.
        /// </summary>
        public string Id => _node.Config.Id;

        /// <summary>
        /// Gets the node name.
        /// </summary>
        public string Name => _node.DisplayName;

        /// <summary>
        /// Creates a new message.
        /// </summary>
        public NodeMessage NewMessage(object? payload = null, string? topic = null)
        {
            return new NodeMessage
            {
                Payload = payload,
                Topic = topic
            };
        }

        /// <summary>
        /// Clones the current message.
        /// </summary>
        public NodeMessage CloneMessage()
        {
            return _currentMessage.Clone();
        }
    }

    /// <summary>
    /// Context accessor for flow/global context.
    /// </summary>
    public class ContextAccessor
    {
        private readonly Func<string, object?> _getter;
        private readonly Action<string, object?> _setter;

        public ContextAccessor(Func<string, object?> getter, Action<string, object?> setter)
        {
            _getter = getter;
            _setter = setter;
        }

        public object? Get(string key) => _getter(key);
        public void Set(string key, object? value) => _setter(key, value);
        
        public object? this[string key]
        {
            get => _getter(key);
            set => _setter(key, value);
        }
    }
}
