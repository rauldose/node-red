// ============================================================
// INSPIRED BY: @node-red/nodes/core/common/20-inject.js
// REFERENCE: NODE-RED-ARCHITECTURE-ANALYSIS.md - Core Nodes
// ============================================================
// Inject node - triggers flows on a schedule or manually
// ============================================================

/*!
 * Original work Copyright JS Foundation and other contributors, http://js.foundation
 * Modified work Copyright 2026 NodeRed.NET Contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 */

using Microsoft.Extensions.Logging;
using NodeRed.Runtime;
using System.Timers;

namespace NodeRed.Nodes.Core;

/// <summary>
/// Inject node configuration
/// </summary>
public class InjectNodeConfiguration : NodeConfiguration
{
    public object? Payload { get; set; }
    public string PayloadType { get; set; } = "str";
    public string? Topic { get; set; }
    public bool Once { get; set; } = false;
    public bool OnceDelay { get; set; } = false;
    public double Repeat { get; set; } = 0; // Repeat interval in seconds
    public string? Cron { get; set; }
}

/// <summary>
/// Inject node - triggers flows on schedule or button press
/// Maps to: inject node in @node-red/nodes/core/common/20-inject.js
/// Reference: NODE-RED-ARCHITECTURE-ANALYSIS.md - Core Nodes section
/// </summary>
public class InjectNode : NodeBase
{
    private readonly InjectNodeConfiguration _config;
    private System.Timers.Timer? _timer;

    public InjectNode(InjectNodeConfiguration configuration, ILogger<InjectNode> logger, INodeContext context)
        : base(configuration.Id, "inject", configuration, logger, context)
    {
        _config = configuration;
        
        // Set up timer if repeat is configured
        if (_config.Repeat > 0)
        {
            _timer = new System.Timers.Timer(_config.Repeat * 1000);
            _timer.Elapsed += (sender, e) => InjectMessage();
            _timer.AutoReset = true;
            _timer.Start();
            
            UpdateStatus("green", "dot", $"repeating every {_config.Repeat}s");
        }
        else
        {
            UpdateStatus("blue", "dot", "ready");
        }

        // Inject once on start if configured
        if (_config.Once)
        {
            if (_config.OnceDelay)
            {
                Task.Delay(100).ContinueWith(_ => InjectMessage());
            }
            else
            {
                InjectMessage();
            }
        }

        // Register close handler to stop timer
        OnClose((removed, done) =>
        {
            _timer?.Stop();
            _timer?.Dispose();
            done?.Invoke();
        });
    }

    /// <summary>
    /// Manually trigger injection (e.g., from button press)
    /// </summary>
    public void Inject()
    {
        InjectMessage();
    }

    private void InjectMessage()
    {
        var message = new FlowMessage
        {
            Topic = _config.Topic,
            Payload = _config.Payload
        };

        Send(message);
        Log($"Injected message with payload: {_config.Payload}");
    }
}
