// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Function;

/// <summary>
/// Range node - maps a numeric value to a different range.
/// </summary>
[NodeType("range", "range",
    Category = NodeCategory.Function,
    Color = "#e2d96e",
    Icon = "fa fa-sliders",
    Inputs = 1,
    Outputs = 1)]
public class RangeNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("property", "Property", defaultValue: "payload")
            .AddSelect("action", "Action", new[]
            {
                ("scale", "Scale message property"),
                ("clamp", "Scale and limit to target range"),
                ("roll", "Scale and wrap within target range")
            }, defaultValue: "scale")
            .AddNumber("minin", "Input min", defaultValue: 0)
            .AddNumber("maxin", "Input max", defaultValue: 100)
            .AddNumber("minout", "Output min", defaultValue: 0)
            .AddNumber("maxout", "Output max", defaultValue: 1)
            .AddCheckbox("round", "Round to nearest integer", defaultValue: false)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "property", "payload" },
        { "action", "scale" },
        { "minin", 0 },
        { "maxin", 100 },
        { "minout", 0 },
        { "maxout", 1 },
        { "round", false }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Maps a numeric value from one range to another.")
        .AddInput("msg.payload", "number", "The value to scale")
        .AddOutput("msg.payload", "number", "The scaled value")
        .Details(@"
The Range node maps a numeric value from an input range to an output range.

**Actions:**
- **Scale** - Linear interpolation between ranges
- **Scale and limit** - Same, but clamp to output range
- **Scale and wrap** - Same, but wrap around if outside range

**Example:**
Mapping 0-100 to 0-1 will convert 50 to 0.5")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var property = GetConfig("property", "payload");
        var action = GetConfig("action", "scale");
        var minin = GetConfig("minin", 0.0);
        var maxin = GetConfig("maxin", 100.0);
        var minout = GetConfig("minout", 0.0);
        var maxout = GetConfig("maxout", 1.0);
        var round = GetConfig("round", false);

        // Get input value
        var inputValue = property == "payload" 
            ? msg.Payload 
            : msg.Properties.GetValueOrDefault(property);

        if (!double.TryParse(inputValue?.ToString(), out var value))
        {
            Warn($"Non-numeric value: {inputValue}");
            send(0, msg);
            done();
            return Task.CompletedTask;
        }

        // Scale the value
        var scaled = ((value - minin) / (maxin - minin)) * (maxout - minout) + minout;

        // Apply action
        scaled = action switch
        {
            "clamp" => Math.Clamp(scaled, Math.Min(minout, maxout), Math.Max(minout, maxout)),
            "roll" => WrapValue(scaled, minout, maxout),
            _ => scaled
        };

        // Round if configured
        if (round)
        {
            scaled = Math.Round(scaled);
        }

        // Set output
        if (property == "payload")
        {
            msg.Payload = scaled;
        }
        else
        {
            msg.Properties[property] = scaled;
        }

        send(0, msg);
        done();
        return Task.CompletedTask;
    }

    private static double WrapValue(double value, double min, double max)
    {
        var range = max - min;
        if (range == 0) return min;
        
        var normalized = (value - min) % range;
        if (normalized < 0) normalized += range;
        return normalized + min;
    }
}
