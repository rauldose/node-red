// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using DynamicExpresso;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Nodes.Function;

/// <summary>
/// Function node - allows custom C# expressions to process messages.
/// Uses DynamicExpresso for expression evaluation.
/// </summary>
public class FunctionNode : NodeBase
{
    private Interpreter? _interpreter;
    private Lambda? _compiledFunction;

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
            { "func", "msg" },
            { "outputs", 1 },
            { "timeout", 0 },
            { "noerr", 0 },
            { "initialize", "" },
            { "finalize", "" }
        },
        HelpText = @"A C# expression to process messages. 
Available variables: msg (NodeMessage), payload, topic, node, flow, global.
Examples:
- msg (pass through)
- msg.Payload = msg.Payload.ToString().ToUpper(); msg (transform payload)
- msg.Payload = (int)msg.Payload * 2; msg (numeric operation)
- null (drop message)"
    };

    public override async Task InitializeAsync(FlowNode config, INodeContext context)
    {
        await base.InitializeAsync(config, context);
        
        _interpreter = CreateInterpreter();
        
        var func = GetConfig("func", "msg");
        try
        {
            _compiledFunction = _interpreter.Parse(func, 
                new Parameter("msg", typeof(NodeMessage)),
                new Parameter("payload", typeof(object)),
                new Parameter("topic", typeof(string)),
                new Parameter("node", typeof(FunctionNodeHelper)),
                new Parameter("flow", typeof(ContextAccessor)),
                new Parameter("global", typeof(ContextAccessor)));
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
                var initLambda = _interpreter.Parse(initialize);
                initLambda.Invoke();
            }
            catch (Exception ex)
            {
                Log($"Initialize error: {ex.Message}", LogLevel.Error);
            }
        }
    }

    private Interpreter CreateInterpreter()
    {
        var interpreter = new Interpreter();
        
        // Register common types
        interpreter.Reference(typeof(NodeMessage));
        interpreter.Reference(typeof(Math));
        interpreter.Reference(typeof(DateTime));
        interpreter.Reference(typeof(DateTimeOffset));
        interpreter.Reference(typeof(TimeSpan));
        interpreter.Reference(typeof(Guid));
        interpreter.Reference(typeof(Convert));
        interpreter.Reference(typeof(String));
        interpreter.Reference(typeof(Enumerable));
        
        // Register helper types
        interpreter.SetVariable("console", new ConsoleHelper(this));
        
        return interpreter;
    }

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        try
        {
            if (_compiledFunction == null)
            {
                // No valid function, pass through
                Send(message.Clone());
                Done();
                return Task.CompletedTask;
            }

            var nodeHelper = new FunctionNodeHelper(this, message);
            var flowContext = new ContextAccessor(key => Context.GetFlowContext<object>(key), (key, val) => Context.SetFlowContext(key, val));
            var globalContext = new ContextAccessor(key => Context.GetGlobalContext<object>(key), (key, val) => Context.SetGlobalContext(key, val));

            var result = _compiledFunction.Invoke(
                message,
                message.Payload,
                message.Topic,
                nodeHelper,
                flowContext,
                globalContext);

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

        return Task.CompletedTask;
    }

    public override async Task CloseAsync()
    {
        // Run finalize function if present
        var finalize = GetConfig("finalize", "");
        if (!string.IsNullOrWhiteSpace(finalize) && _interpreter != null)
        {
            try
            {
                var finalizeLambda = _interpreter.Parse(finalize);
                finalizeLambda.Invoke();
            }
            catch (Exception ex)
            {
                Log($"Finalize error: {ex.Message}", LogLevel.Error);
            }
        }

        await base.CloseAsync();
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
    /// Console helper for logging from functions.
    /// </summary>
    public class ConsoleHelper
    {
        private readonly FunctionNode _node;

        public ConsoleHelper(FunctionNode node)
        {
            _node = node;
        }

        public void Log(object? message) => _node.Log(message?.ToString() ?? "null", LogLevel.Info);
        public void Warn(object? message) => _node.Log(message?.ToString() ?? "null", LogLevel.Warning);
        public void Error(object? message) => _node.Log(message?.ToString() ?? "null", LogLevel.Error);
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
