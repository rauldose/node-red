// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Function;

/// <summary>
/// RBE (Report By Exception) node - only passes on messages when the value changes.
/// Also known as "filter" node.
/// </summary>
[NodeType("rbe", "rbe",
    Category = NodeCategory.Function,
    Color = "#e6e0f8",
    Icon = "fa fa-filter",
    Inputs = 1,
    Outputs = 1)]
public class RbeNode : SdkNodeBase
{
    private readonly Dictionary<string, object?> _lastValues = new();

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("mode", "Mode", new[]
            {
                ("rbe", "Block unless value changes"),
                ("deadband", "Block unless value changes by more than"),
                ("narrowband", "Block if value changes by more than")
            }, defaultValue: "rbe")
            .AddText("property", "Property", defaultValue: "payload", icon: "fa fa-ellipsis-h")
            .AddNumber("gap", "Threshold", defaultValue: 0, showWhen: "mode=deadband|narrowband")
            .AddSelect("gaptype", "Threshold Type", new[]
            {
                ("num", "Absolute value"),
                ("percent", "Percentage")
            }, defaultValue: "num", showWhen: "mode=deadband|narrowband")
            .AddSelect("inout", "Compare", new[]
            {
                ("out", "Outside threshold"),
                ("in", "Inside threshold")
            }, defaultValue: "out", showWhen: "mode=deadband|narrowband")
            .AddCheckbox("septopics", "Separate values per topic", defaultValue: true)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "mode", "rbe" },
        { "property", "payload" },
        { "gap", 0.0 },
        { "gaptype", "num" },
        { "inout", "out" },
        { "septopics", true }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Only passes on messages when the value changes (Report By Exception).")
        .AddInput("msg", "object", "The message to filter")
        .AddOutput("msg", "object", "The message (only if value changed)")
        .Details(@"
The RBE (Report By Exception) node filters messages based on value changes.

**Modes:**
- **Block unless value changes** - Only pass messages when the value is different
- **Block unless value changes by more than** (deadband) - For numeric values, only
  pass messages when the change exceeds a threshold
- **Block if value changes by more than** (narrowband) - Opposite of deadband

**Options:**
- **Separate values per topic** - Maintain separate previous values per msg.topic

Useful for:
- Reducing message volume from sensors
- Detecting state changes
- Implementing hysteresis")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
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
            currentValue = msg.Payload;
        }
        else if (msg.Properties.TryGetValue(property, out var propValue))
        {
            currentValue = propValue;
        }
        else
        {
            done();
            return Task.CompletedTask;
        }

        // Determine the key for storing last value
        var key = septopics ? msg.Topic ?? "" : "";

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
            send(0, msg);
        }

        done();
        return Task.CompletedTask;
    }

    private static bool CheckDeadband(object? current, object? last, double gap, string gaptype, string mode, string inout)
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
