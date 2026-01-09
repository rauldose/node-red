// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Function;

/// <summary>
/// RBE (Report By Exception) node - only passes on messages when the value changes.
/// </summary>
public class RbeNode : NodeBase
{
    private readonly Dictionary<string, object?> _lastValues = new();

    public override NodeDefinition Definition => new()
    {
        Type = "rbe",
        Category = NodeCategory.Function,
        DisplayName = "rbe",
        Color = "#fdd0a2",
        Icon = "fa-filter",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "mode", "rbe" }, // rbe, deadband, narrowband
            { "property", "payload" },
            { "start", "" },
            { "inout", "out" }, // out, in
            { "septopics", true },
            { "gap", 0.0 },
            { "gaptype", "num" } // num, percent
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var mode = GetConfig<string>("mode", "rbe");
        var property = GetConfig<string>("property", "payload");
        var septopics = GetConfig<bool>("septopics", true);
        var gap = GetConfig<double>("gap", 0);
        var gaptype = GetConfig<string>("gaptype", "num");
        var inout = GetConfig<string>("inout", "out");

        // Get the current value
        object? currentValue;
        if (property == "payload")
        {
            currentValue = message.Payload;
        }
        else if (message.Properties.TryGetValue(property, out var propValue))
        {
            currentValue = propValue;
        }
        else
        {
            Done();
            return Task.CompletedTask;
        }

        // Determine the key for storing last value
        var key = septopics ? message.Topic ?? "" : "";

        // Get the last value
        _lastValues.TryGetValue(key, out var lastValue);

        bool shouldSend;

        switch (mode)
        {
            case "deadband":
            case "narrowband":
                shouldSend = CheckDeadband(currentValue, lastValue, gap, gaptype, mode, inout);
                break;
            default: // rbe
                shouldSend = !ValuesEqual(currentValue, lastValue);
                break;
        }

        if (shouldSend)
        {
            _lastValues[key] = currentValue;
            Send(message);
        }

        Done();
        return Task.CompletedTask;
    }

    private bool CheckDeadband(object? current, object? last, double gap, string gaptype, string mode, string inout)
    {
        if (last == null) return true;

        try
        {
            var currentNum = Convert.ToDouble(current);
            var lastNum = Convert.ToDouble(last);

            var threshold = gaptype == "percent" 
                ? Math.Abs(lastNum * gap / 100) 
                : gap;

            var diff = Math.Abs(currentNum - lastNum);

            if (mode == "deadband")
            {
                // Deadband: only send if outside the band
                return inout == "out" ? diff >= threshold : diff < threshold;
            }
            else // narrowband
            {
                // Narrowband: only send if inside the band
                return inout == "out" ? diff < threshold : diff >= threshold;
            }
        }
        catch
        {
            // Non-numeric values, fall back to equality check
            return !ValuesEqual(current, last);
        }
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.Equals(b);
    }
}
