// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using System.Timers;

namespace NodeRed.Runtime.Nodes.Function;

/// <summary>
/// Trigger node - can be used to trigger events based on conditions.
/// </summary>
public class TriggerNode : NodeBase, IDisposable
{
    private System.Timers.Timer? _timer;
    private NodeMessage? _pendingMessage;
    private bool _triggered;

    public override NodeDefinition Definition => new()
    {
        Type = "trigger",
        Category = NodeCategory.Function,
        DisplayName = "trigger",
        Color = "#fdd0a2",
        Icon = "fa-clock",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "op1", "1" },
            { "op2", "0" },
            { "op1type", "str" },
            { "op2type", "str" },
            { "duration", 250.0 },
            { "extend", true },
            { "overrideDelay", false },
            { "units", "ms" },
            { "reset", "" },
            { "bytopic", false },
            { "topic", "" },
            { "outputs", 1 }
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var reset = GetConfig<string>("reset", "");
        var extend = GetConfig<bool>("extend", true);
        var duration = GetConfig<double>("duration", 250);
        var units = GetConfig<string>("units", "ms");
        var op1 = GetConfig<string>("op1", "1");
        var op1type = GetConfig<string>("op1type", "str");
        var op2 = GetConfig<string>("op2", "0");
        var op2type = GetConfig<string>("op2type", "str");

        // Check for reset
        if (!string.IsNullOrEmpty(reset) && message.Payload?.ToString() == reset)
        {
            ResetTrigger();
            Done();
            return Task.CompletedTask;
        }

        // Calculate duration in milliseconds
        var durationMs = units switch
        {
            "s" => duration * 1000,
            "min" => duration * 60 * 1000,
            "hr" => duration * 60 * 60 * 1000,
            _ => duration
        };

        if (!_triggered)
        {
            // First trigger - send op1
            _triggered = true;
            _pendingMessage = message;

            var msg1 = new NodeMessage
            {
                Topic = message.Topic,
                Payload = GetTypedValue(op1, op1type)
            };
            Send(msg1);

            // Start timer for op2
            _timer = new System.Timers.Timer(durationMs);
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = false;
            _timer.Start();

            SetStatus(new NodeStatus { Color = StatusColor.Blue, Shape = StatusShape.Dot, Text = "waiting" });
        }
        else if (extend)
        {
            // Extend the timer
            _timer?.Stop();
            _timer?.Start();
            _pendingMessage = message;
        }

        Done();
        return Task.CompletedTask;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        var op2 = GetConfig<string>("op2", "0");
        var op2type = GetConfig<string>("op2type", "str");

        var msg2 = new NodeMessage
        {
            Topic = _pendingMessage?.Topic ?? "",
            Payload = GetTypedValue(op2, op2type)
        };
        Send(msg2);

        ResetTrigger();
    }

    private object GetTypedValue(string value, string type)
    {
        return type switch
        {
            "num" => double.TryParse(value, out var num) ? num : 0,
            "bool" => value.ToLower() == "true",
            "json" => value, // In a real implementation, parse JSON
            "date" => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            _ => value
        };
    }

    private void ResetTrigger()
    {
        _triggered = false;
        _pendingMessage = null;
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        SetStatus(new NodeStatus());
    }

    public override Task CloseAsync()
    {
        ResetTrigger();
        return base.CloseAsync();
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
