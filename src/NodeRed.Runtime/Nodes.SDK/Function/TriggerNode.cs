// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;
using SdkTimer = System.Timers.Timer;

namespace NodeRed.Runtime.Nodes.SDK.Function;

/// <summary>
/// Trigger node - can be used to trigger events based on conditions.
/// Sends a message when triggered, then optionally a second message after a delay.
/// </summary>
[NodeType("trigger", "trigger",
    Category = NodeCategory.Function,
    Color = "#e6e0f8",
    Icon = "fa fa-toggle-on",
    Inputs = 1,
    Outputs = 1)]
public class TriggerNode : SdkNodeBase, IDisposable
{
    private SdkTimer? _timer;
    private NodeMessage? _pendingMessage;
    private bool _triggered;
    private SendDelegate? _currentSend;

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("op1type", "Send Type", new[]
            {
                ("str", "String"),
                ("num", "Number"),
                ("bool", "Boolean"),
                ("pay", "Existing msg.payload"),
                ("date", "Timestamp"),
                ("nul", "Nothing")
            }, defaultValue: "str")
            .AddText("op1", "Value", showWhen: "op1type=str|num|bool")
            .AddNumber("duration", "then wait for", suffix: "ms", defaultValue: 250, min: 0)
            .AddSelect("units", "Units", new[]
            {
                ("ms", "Milliseconds"),
                ("s", "Seconds"),
                ("min", "Minutes"),
                ("hr", "Hours")
            }, defaultValue: "ms")
            .AddSelect("op2type", "Then send", new[]
            {
                ("str", "String"),
                ("num", "Number"),
                ("bool", "Boolean"),
                ("pay", "Existing msg.payload"),
                ("date", "Timestamp"),
                ("nul", "Nothing")
            }, defaultValue: "str")
            .AddText("op2", "Value", showWhen: "op2type=str|num|bool")
            .AddCheckbox("extend", "Extend delay if new message arrives", defaultValue: true)
            .AddText("reset", "Reset if msg.payload equals")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "op1", "1" },
        { "op2", "0" },
        { "op1type", "str" },
        { "op2type", "str" },
        { "duration", 250.0 },
        { "extend", true },
        { "units", "ms" },
        { "reset", "" },
        { "outputs", 1 }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Creates two messages on the output separated by a timeout.")
        .AddInput("msg", "object", "Trigger message")
        .AddOutput("msg.payload", "various", "The configured values")
        .Details(@"
The Trigger node can be used to create timed sequences.

When triggered, it sends the first message immediately, then waits
for the specified duration before sending the second message.

**Options:**
- **Extend delay** - If enabled, receiving a new message resets the timer
- **Reset** - If the payload matches this value, the node resets without sending

**Use cases:**
- Creating on/off sequences
- Implementing debounce behavior
- Generating timed pulses")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var reset = GetConfig<string>("reset", "");
        var extend = GetConfig<bool>("extend", true);
        var duration = GetConfig<double>("duration", 250);
        var units = GetConfig<string>("units", "ms");
        var op1 = GetConfig<string>("op1", "1");
        var op1type = GetConfig<string>("op1type", "str");

        _currentSend = send;

        // Check for reset
        if (!string.IsNullOrEmpty(reset) && msg.Payload?.ToString() == reset)
        {
            ResetTrigger();
            done();
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
            _pendingMessage = msg;

            var msg1 = NewMessage(GetTypedValue(op1, op1type, msg), msg.Topic);
            send(0, msg1);

            // Start timer for op2
            _timer = new SdkTimer(durationMs);
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = false;
            _timer.Start();

            Status("waiting", StatusFill.Blue, SdkStatusShape.Dot);
        }
        else if (extend)
        {
            // Extend the timer
            _timer?.Stop();
            _timer?.Start();
            _pendingMessage = msg;
        }

        done();
        return Task.CompletedTask;
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var op2 = GetConfig<string>("op2", "0");
        var op2type = GetConfig<string>("op2type", "str");

        if (_currentSend != null && _pendingMessage != null)
        {
            var msg2 = NewMessage(GetTypedValue(op2, op2type, _pendingMessage), _pendingMessage.Topic);
            _currentSend(0, msg2);
        }

        ResetTrigger();
    }

    private object? GetTypedValue(string value, string type, NodeMessage msg)
    {
        return type switch
        {
            "num" => double.TryParse(value, out var num) ? num : 0,
            "bool" => value.ToLower() == "true",
            "pay" => msg.Payload,
            "date" => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            "nul" => null,
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
        ClearStatus();
    }

    protected override Task OnCloseAsync()
    {
        ResetTrigger();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
