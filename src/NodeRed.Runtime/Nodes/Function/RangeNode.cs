// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Function;

/// <summary>
/// Range node - maps a numeric value from one range to another.
/// </summary>
public class RangeNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "range",
        Category = NodeCategory.Function,
        DisplayName = "range",
        Color = "#fdd0a2",
        Icon = "fa-arrows-alt-h",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "property", "payload" },
            { "inMin", 0.0 },
            { "inMax", 100.0 },
            { "outMin", 0.0 },
            { "outMax", 1.0 },
            { "action", "scale" }, // scale, clamp, roll
            { "round", false }
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var property = GetConfig<string>("property", "payload");
        var inMin = GetConfig<double>("inMin", 0);
        var inMax = GetConfig<double>("inMax", 100);
        var outMin = GetConfig<double>("outMin", 0);
        var outMax = GetConfig<double>("outMax", 1);
        var action = GetConfig<string>("action", "scale");
        var round = GetConfig<bool>("round", false);

        // Get the input value
        double inputValue;
        if (property == "payload" && message.Payload != null)
        {
            inputValue = Convert.ToDouble(message.Payload);
        }
        else if (message.Properties.TryGetValue(property, out var propValue))
        {
            inputValue = Convert.ToDouble(propValue);
        }
        else
        {
            Done();
            return Task.CompletedTask;
        }

        double outputValue;

        switch (action)
        {
            case "clamp":
                // Clamp the input to the input range, then scale
                inputValue = Math.Max(inMin, Math.Min(inMax, inputValue));
                outputValue = MapValue(inputValue, inMin, inMax, outMin, outMax);
                break;
            case "roll":
                // Roll over if outside the input range
                var inRange = inMax - inMin;
                inputValue = ((inputValue - inMin) % inRange + inRange) % inRange + inMin;
                outputValue = MapValue(inputValue, inMin, inMax, outMin, outMax);
                break;
            default: // scale
                outputValue = MapValue(inputValue, inMin, inMax, outMin, outMax);
                break;
        }

        if (round)
        {
            outputValue = Math.Round(outputValue);
        }

        // Set the output value
        if (property == "payload")
        {
            message.Payload = outputValue;
        }
        else
        {
            message.Properties[property] = outputValue;
        }

        Send(message);
        Done();
        return Task.CompletedTask;
    }

    private static double MapValue(double value, double inMin, double inMax, double outMin, double outMax)
    {
        if (Math.Abs(inMax - inMin) < double.Epsilon)
            return outMin;
        
        return (value - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
    }
}
