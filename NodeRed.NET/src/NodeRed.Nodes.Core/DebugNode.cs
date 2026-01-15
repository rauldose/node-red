// ============================================================
// INSPIRED BY: @node-red/nodes/core/common/24-debug.js
// REFERENCE: NODE-RED-ARCHITECTURE-ANALYSIS.md - Core Nodes
// ============================================================
// Debug node - outputs messages to debug panel and console
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
/// Debug node configuration
/// </summary>
public class DebugNodeConfiguration : NodeConfiguration
{
    public bool Console { get; set; } = false;
    public bool TostMsg { get; set; } = false; // Show in UI notification
    public bool Active { get; set; } = true;
    public string Complete { get; set; } = "false"; // What to output: "false" = payload, "true" = complete msg
    public string? TargetType { get; set; } = "msg";
    public int StatusVal { get; set; } = 0;
    public string StatusType { get; set; } = "auto";
}

/// <summary>
/// Debug node - outputs messages for debugging
/// Maps to: debug node in @node-red/nodes/core/common/24-debug.js
/// Reference: NODE-RED-ARCHITECTURE-ANALYSIS.md - Core Nodes section
/// </summary>
public class DebugNode : NodeBase
{
    private readonly DebugNodeConfiguration _config;
    private int _messageCount = 0;

    public DebugNode(DebugNodeConfiguration configuration, ILogger<DebugNode> logger, INodeContext context)
        : base(configuration.Id, "debug", configuration, logger, context)
    {
        _config = configuration;

        // Register input handler
        OnInput(msg =>
        {
            if (!_config.Active)
            {
                return;
            }

            _messageCount++;

            // Determine what to output
            object? output = _config.Complete == "true" ? msg : msg.Payload;

            // Format output
            string outputStr;
            try
            {
                outputStr = JsonSerializer.Serialize(output, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            catch
            {
                outputStr = output?.ToString() ?? "(null)";
            }

            // Output to console if configured
            if (_config.Console)
            {
                Console.WriteLine($"[{Name ?? Id}] {outputStr}");
            }

            // Always log
            Log($"Debug output: {outputStr}");

            // Raise event for UI to capture
            if (DebugOutput != null)
            {
                DebugOutput(this, new DebugOutputEventArgs
                {
                    Message = msg,
                    Output = output,
                    OutputString = outputStr,
                    Timestamp = DateTimeOffset.UtcNow
                });
            }

            // Update status
            if (_config.StatusType == "auto")
            {
                UpdateStatus("green", "dot", $"msg #{_messageCount}");
            }
        });

        UpdateStatus("grey", "ring", "");
    }

    /// <summary>
    /// Enable or disable debug output
    /// </summary>
    public void SetActive(bool active)
    {
        _config.Active = active;
        UpdateStatus(active ? "grey" : "grey", active ? "ring" : "dot", active ? "" : "disabled");
    }

    /// <summary>
    /// Event raised when debug output is generated
    /// Can be captured by UI for display
    /// </summary>
    public event EventHandler<DebugOutputEventArgs>? DebugOutput;
}

/// <summary>
/// Debug output event args
/// </summary>
public class DebugOutputEventArgs : EventArgs
{
    public FlowMessage Message { get; set; } = null!;
    public object? Output { get; set; }
    public string OutputString { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}
