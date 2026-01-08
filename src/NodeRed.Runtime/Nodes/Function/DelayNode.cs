// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Function;

/// <summary>
/// Delay node - delays messages passing through the node.
/// </summary>
public class DelayNode : NodeBase
{
    private readonly Queue<(NodeMessage Message, DateTime ReleaseTime)> _queue = new();
    private Timer? _timer;

    public override NodeDefinition Definition => new()
    {
        Type = "delay",
        DisplayName = "delay",
        Category = NodeCategory.Function,
        Color = "#e6e0f8",
        Icon = "delay",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "name", "" },
            { "pauseType", "delay" },
            { "timeout", "5" },
            { "timeoutUnits", "seconds" },
            { "rate", "1" },
            { "nbRateUnits", "1" },
            { "rateUnits", "second" },
            { "randomFirst", "1" },
            { "randomLast", "5" },
            { "randomUnits", "seconds" },
            { "drop", false }
        },
        HelpText = "Delays each message passing through the node or limits the rate at which they can pass."
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var pauseType = GetConfig("pauseType", "delay");

        switch (pauseType)
        {
            case "delay":
                HandleDelay(message);
                break;
            case "rate":
                HandleRateLimit(message);
                break;
            case "random":
                HandleRandomDelay(message);
                break;
            default:
                Send(message);
                Done();
                break;
        }

        return Task.CompletedTask;
    }

    private void HandleDelay(NodeMessage message)
    {
        var timeout = GetConfig("timeout", "5");
        var units = GetConfig("timeoutUnits", "seconds");
        var delay = ParseDuration(timeout, units);

        _ = Task.Delay(delay).ContinueWith(_ =>
        {
            Send(message);
            Done();
        });
    }

    private void HandleRateLimit(NodeMessage message)
    {
        var rate = GetConfig("rate", "1");
        var nbRateUnits = GetConfig("nbRateUnits", "1");
        var units = GetConfig("rateUnits", "second");
        var drop = GetConfig("drop", false);

        if (!int.TryParse(rate, out var rateValue)) rateValue = 1;
        if (!int.TryParse(nbRateUnits, out var intervalValue)) intervalValue = 1;

        var interval = ParseDuration(intervalValue.ToString(), units + "s");
        var releaseTime = DateTime.UtcNow.Add(interval);

        if (_queue.Count > 0 && drop)
        {
            // Drop message
            Done();
            return;
        }

        _queue.Enqueue((message, releaseTime));
        StartTimer();
    }

    private void HandleRandomDelay(NodeMessage message)
    {
        var first = GetConfig("randomFirst", "1");
        var last = GetConfig("randomLast", "5");
        var units = GetConfig("randomUnits", "seconds");

        if (!double.TryParse(first, out var minValue)) minValue = 1;
        if (!double.TryParse(last, out var maxValue)) maxValue = 5;

        var delaySeconds = minValue + (Random.Shared.NextDouble() * (maxValue - minValue));
        var delay = ParseDuration(delaySeconds.ToString(), units);

        _ = Task.Delay(delay).ContinueWith(_ =>
        {
            Send(message);
            Done();
        });
    }

    private void StartTimer()
    {
        if (_timer != null) return;

        _timer = new Timer(_ =>
        {
            while (_queue.Count > 0 && _queue.Peek().ReleaseTime <= DateTime.UtcNow)
            {
                var item = _queue.Dequeue();
                Send(item.Message);
            }

            if (_queue.Count == 0)
            {
                _timer?.Dispose();
                _timer = null;
            }

            Done();
        }, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
    }

    private static TimeSpan ParseDuration(string value, string units)
    {
        if (!double.TryParse(value, out var numValue)) numValue = 1;

        return units.ToLowerInvariant() switch
        {
            "milliseconds" or "millis" => TimeSpan.FromMilliseconds(numValue),
            "seconds" or "second" => TimeSpan.FromSeconds(numValue),
            "minutes" or "minute" => TimeSpan.FromMinutes(numValue),
            "hours" or "hour" => TimeSpan.FromHours(numValue),
            "days" or "day" => TimeSpan.FromDays(numValue),
            _ => TimeSpan.FromSeconds(numValue)
        };
    }

    public override Task CloseAsync()
    {
        _timer?.Dispose();
        _timer = null;
        _queue.Clear();
        return Task.CompletedTask;
    }
}
