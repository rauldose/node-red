// ============================================================
// INSPIRED BY: @node-red/nodes/core/function/10-function.js
// REFERENCE: NODE-RED-ARCHITECTURE-ANALYSIS.md - Core Nodes
// ============================================================
// Function node - executes JavaScript/C# code on messages
// ============================================================

/*!
 * Original work Copyright JS Foundation and other contributors, http://js.foundation
 * Modified work Copyright 2026 NodeRed.NET Contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 */

using Microsoft.Extensions.Logging;
using NodeRed.Runtime;
using System.Text.Json;

namespace NodeRed.Nodes.Core;

/// <summary>
/// Function node configuration
/// </summary>
public class FunctionNodeConfiguration : NodeConfiguration
{
    public string Func { get; set; } = "return msg;";
    public int Outputs { get; set; } = 1;
    public bool Initialize { get; set; } = false;
    public string? InitializeCode { get; set; }
    public bool Finalize { get; set; } = false;
    public string? FinalizeCode { get; set; }
}

/// <summary>
/// Function node - executes C# code on incoming messages
/// Maps to: function node in @node-red/nodes/core/function/10-function.js
/// Reference: NODE-RED-ARCHITECTURE-ANALYSIS.md - Core Nodes section
/// 
/// Note: This is a simplified version. Full implementation would use
/// Roslyn scripting for dynamic C# code execution.
/// </summary>
public class FunctionNode : NodeBase
{
    private readonly FunctionNodeConfiguration _config;
    private readonly Dictionary<string, object?> _flowContext = new();
    private readonly Dictionary<string, object?> _globalContext = new();

    public FunctionNode(FunctionNodeConfiguration configuration, ILogger<FunctionNode> logger, INodeContext context)
        : base(configuration.Id, "function", configuration, logger, context)
    {
        _config = configuration;

        // Register input handler
        OnInput(msg =>
        {
            try
            {
                // Execute the function code
                var result = ExecuteFunction(msg);

                if (result != null)
                {
                    if (result is FlowMessage single)
                    {
                        Send(single);
                    }
                    else if (result is FlowMessage?[] multiple)
                    {
                        Send(multiple);
                    }
                    else if (result is IEnumerable<FlowMessage> enumerable)
                    {
                        Send(enumerable.ToArray());
                    }
                }

                UpdateStatus("green", "dot", $"processed");
            }
            catch (Exception ex)
            {
                Error($"Function execution error: {ex.Message}", msg, ex);
                UpdateStatus("red", "ring", "error");
            }
        });

        // Execute initialize code if configured
        if (_config.Initialize && !string.IsNullOrEmpty(_config.InitializeCode))
        {
            try
            {
                // In full implementation, execute initialization code
                Log("Function initialized");
            }
            catch (Exception ex)
            {
                Error($"Initialization error: {ex.Message}", exception: ex);
            }
        }

        // Register close handler for finalization
        OnClose((removed, done) =>
        {
            if (_config.Finalize && !string.IsNullOrEmpty(_config.FinalizeCode))
            {
                try
                {
                    // In full implementation, execute finalization code
                    Log("Function finalized");
                }
                catch (Exception ex)
                {
                    Error($"Finalization error: {ex.Message}", exception: ex);
                }
            }
            done?.Invoke();
        });

        UpdateStatus("grey", "dot", "");
    }

    /// <summary>
    /// Execute the function code
    /// In a full implementation, this would use Roslyn scripting API
    /// or compile C# code dynamically
    /// </summary>
    private object? ExecuteFunction(FlowMessage msg)
    {
        // Simplified implementation - just provides some built-in transformations
        // Full implementation would use Microsoft.CodeAnalysis.CSharp.Scripting

        // For demo purposes, provide some simple built-in functions
        if (_config.Func.Contains("msg.payload = msg.payload * 2"))
        {
            var newMsg = msg.Clone();
            if (newMsg.Payload is int intVal)
            {
                newMsg.Payload = intVal * 2;
            }
            else if (newMsg.Payload is double doubleVal)
            {
                newMsg.Payload = doubleVal * 2;
            }
            return newMsg;
        }
        else if (_config.Func.Contains("msg.payload = msg.payload.toUpperCase()"))
        {
            var newMsg = msg.Clone();
            if (newMsg.Payload is string strVal)
            {
                newMsg.Payload = strVal.ToUpper();
            }
            return newMsg;
        }
        else
        {
            // Default: pass through
            return msg;
        }
    }
}
