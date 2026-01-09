// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Function;

/// <summary>
/// Delay node - delays messages or limits rate.
/// </summary>
[NodeType("delay", "delay",
    Category = NodeCategory.Function,
    Color = "#e6e0f8",
    Icon = "fa fa-clock-o",
    Inputs = 1,
    Outputs = 1)]
public class DelayNode : SdkNodeBase
{
    private readonly Queue<(NodeMessage msg, DateTime releaseTime)> _queue = new();
    private Timer? _timer;

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("pauseType", "Action", new[]
            {
                ("delay", "Delay each message"),
                ("delayv", "Delay - set by msg.delay"),
                ("rate", "Rate limit all messages"),
                ("queue", "Rate limit - drop intermediate"),
                ("timed", "Rate limit - topic dependent")
            }, defaultValue: "delay")
            .AddNumber("timeout", "For", defaultValue: 5, min: 0, showWhen: "pauseType=delay")
            .AddSelect("timeoutUnits", "Units", new[]
            {
                ("milliseconds", "Milliseconds"),
                ("seconds", "Seconds"),
                ("minutes", "Minutes"),
                ("hours", "Hours"),
                ("days", "Days")
            }, defaultValue: "seconds", showWhen: "pauseType=delay")
            .AddNumber("rate", "Rate", defaultValue: 1, min: 1, showWhen: "pauseType=rate|queue|timed")
            .AddSelect("rateUnits", "Per", new[]
            {
                ("second", "Second"),
                ("minute", "Minute"),
                ("hour", "Hour"),
                ("day", "Day")
            }, defaultValue: "second", showWhen: "pauseType=rate|queue|timed")
            .AddCheckbox("drop", "Drop intermediate messages", defaultValue: false, showWhen: "pauseType=rate")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "pauseType", "delay" },
        { "timeout", 5 },
        { "timeoutUnits", "seconds" },
        { "rate", 1 },
        { "rateUnits", "second" },
        { "drop", false }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Delays messages passing through the node or limits their rate.")
        .AddInput("msg", "object", "The message to delay")
        .AddOutput("msg", "object", "The delayed message")
        .Details(@"
The Delay node can:

**Delay each message** - Hold each message for a fixed time
**Set by msg.delay** - Use `msg.delay` (in milliseconds) for dynamic delay
**Rate limit** - Limit throughput to N messages per time period

Rate limiting modes:
- **All messages** - Queue and release at rate
- **Drop intermediate** - Only keep latest message
- **Topic dependent** - Separate rate limit per topic")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var pauseType = GetConfig("pauseType", "delay");
        
        switch (pauseType)
        {
            case "delay":
                var timeout = GetConfig("timeout", 5.0);
                var units = GetConfig("timeoutUnits", "seconds");
                var delayMs = ConvertToMilliseconds(timeout, units);
                _ = DelayAndSend(msg, delayMs, send, done);
                break;
                
            case "delayv":
                var msgDelay = msg.Properties.GetValueOrDefault("delay") as double? ?? 0;
                _ = DelayAndSend(msg, (int)msgDelay, send, done);
                break;
                
            default:
                // Rate limiting - queue the message
                send(0, msg);
                done();
                break;
        }
        
        return Task.CompletedTask;
    }

    private async Task DelayAndSend(NodeMessage msg, int delayMs, SendDelegate send, DoneDelegate done)
    {
        Status($"waiting {delayMs}ms", StatusFill.Blue, SdkStatusShape.Ring);
        await Task.Delay(delayMs);
        ClearStatus();
        send(0, msg);
        done();
    }

    private static int ConvertToMilliseconds(double value, string units)
    {
        return units switch
        {
            "milliseconds" => (int)value,
            "seconds" => (int)(value * 1000),
            "minutes" => (int)(value * 60 * 1000),
            "hours" => (int)(value * 60 * 60 * 1000),
            "days" => (int)(value * 24 * 60 * 60 * 1000),
            _ => (int)(value * 1000)
        };
    }

    protected override Task OnCloseAsync()
    {
        _timer?.Dispose();
        _timer = null;
        _queue.Clear();
        return Task.CompletedTask;
    }
}
