// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

// This file contains example nodes demonstrating how to use the SDK.
// These are NOT actual node implementations - they are templates and examples.

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.SDK.Examples;

/// <summary>
/// Example: Simple node that transforms the payload to uppercase.
/// 
/// This demonstrates the basic structure of a node:
/// - NodeTypeAttribute defines metadata
/// - DefineProperties() defines the editor UI
/// - DefineHelp() provides documentation
/// - OnInputAsync() handles messages
/// </summary>
[NodeType("example-upper-case", "upper case",
    Category = NodeCategory.Function,
    Color = "#E2D96E",
    Icon = "fa fa-font",
    Inputs = 1,
    Outputs = 1)]
public class ExampleUpperCaseNode : NodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddInfo("Converts msg.payload to uppercase string.")
            .Build();

    protected override NodeHelpText DefineHelp() =>
        HelpBuilder.Create()
            .Summary("Converts the payload to an uppercase string.")
            .AddInput("payload", "string", "The text to convert")
            .AddOutput("payload", "string", "The uppercase text")
            .Details("If the payload is not a string, it will be converted to a string first.")
            .Build();

    protected override async Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var input = msg.Payload?.ToString() ?? "";
            msg.Payload = input.ToUpperInvariant();
            send(0, msg);
            done();
        }
        catch (Exception ex)
        {
            done(ex);
        }

        await Task.CompletedTask;
    }
}

/// <summary>
/// Example: Node with multiple configuration options.
/// 
/// This demonstrates:
/// - Multiple property types (text, number, select, checkbox)
/// - Conditional visibility with ShowWhen/HideWhen
/// - Using configuration values
/// </summary>
[NodeType("example-counter", "counter",
    Category = NodeCategory.Function,
    Color = "#C7E9C0",
    Icon = "fa fa-plus",
    Inputs = 1,
    Outputs = 1)]
public class ExampleCounterNode : NodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddNumber("start", "Start value", defaultValue: 0)
            .AddNumber("step", "Step", defaultValue: 1, min: -100, max: 100)
            .AddSelect("mode", "Mode", new[]
            {
                ("increment", "Increment on each message"),
                ("reset", "Reset on each message"),
                ("set", "Set to msg.payload")
            })
            .AddCheckbox("outputOnlyOnChange", "Output only when value changes", defaultValue: false)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        ["start"] = 0,
        ["step"] = 1,
        ["mode"] = "increment",
        ["outputOnlyOnChange"] = false
    };

    protected override NodeHelpText DefineHelp() =>
        HelpBuilder.Create()
            .Summary("Counts messages or maintains a counter value.")
            .AddInput("payload", "any", "Incoming message (used in 'set' mode)")
            .AddInput("reset", "any", "If present, resets the counter to start value")
            .AddOutput("payload", "number", "The current counter value")
            .AddOutput("count", "number", "Same as payload (for compatibility)")
            .Details(@"
                The counter node maintains a numeric value that changes based on the mode:
                - **Increment**: Adds the step value on each message
                - **Reset**: Resets to start value on each message
                - **Set**: Sets the counter to msg.payload value
                
                To reset the counter, send a message with msg.reset set to any value.
            ")
            .Build();

    private double _currentValue;
    private double _lastValue;

    protected override Task OnInitializeAsync()
    {
        _currentValue = GetConfig("start", 0.0);
        _lastValue = _currentValue;
        return Task.CompletedTask;
    }

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            // Check for reset
            if (msg.Properties.ContainsKey("reset"))
            {
                _currentValue = GetConfig("start", 0.0);
            }
            else
            {
                var mode = GetConfig("mode", "increment");
                var step = GetConfig("step", 1.0);

                switch (mode)
                {
                    case "increment":
                        _currentValue += step;
                        break;
                    case "reset":
                        _currentValue = GetConfig("start", 0.0);
                        break;
                    case "set":
                        if (msg.Payload is double d)
                            _currentValue = d;
                        else if (double.TryParse(msg.Payload?.ToString(), out var parsed))
                            _currentValue = parsed;
                        break;
                }
            }

            var outputOnlyOnChange = GetConfig("outputOnlyOnChange", false);
            if (!outputOnlyOnChange || _currentValue != _lastValue)
            {
                msg.Payload = _currentValue;
                msg.Properties["count"] = _currentValue;
                send(0, msg);
            }

            _lastValue = _currentValue;
            Status($"Count: {_currentValue}", StatusFill.Green, SdkStatusShape.Dot);
            done();
        }
        catch (Exception ex)
        {
            done(ex);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Example: Node with a button (like Inject).
/// 
/// This demonstrates:
/// - HasButton = true for nodes with action buttons
/// - DefineButton() to configure the button
/// - Triggering output without input
/// </summary>
[NodeType("example-timestamp", "timestamp",
    Category = NodeCategory.Common,
    Color = "#A6BBCF",
    Icon = "fa fa-clock-o",
    Inputs = 0,
    Outputs = 1,
    HasButton = true)]
public class ExampleTimestampNode : NodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddSelect("format", "Format", new[]
            {
                ("unix", "Unix timestamp (milliseconds)"),
                ("iso", "ISO 8601 string"),
                ("local", "Local date/time string")
            }, defaultValue: "unix")
            .Build();

    protected override NodeButtonDefinition DefineButton() => new()
    {
        Icon = "fa fa-play",
        Action = "inject"
    };

    protected override NodeHelpText DefineHelp() =>
        HelpBuilder.Create()
            .Summary("Injects a timestamp into the flow.")
            .AddOutput("payload", "number | string", "The current timestamp")
            .Details("Click the button on the node to inject a timestamp message.")
            .Build();

    // This method would be called when the button is clicked
    public void Inject()
    {
        var format = GetConfig("format", "unix");
        var now = DateTimeOffset.UtcNow;

        object payload = format switch
        {
            "unix" => now.ToUnixTimeMilliseconds(),
            "iso" => now.ToString("O"),
            "local" => now.LocalDateTime.ToString(),
            _ => now.ToUnixTimeMilliseconds()
        };

        var msg = NewMessage(payload);
        // Note: In actual implementation, we'd use the context to send
        // This is just an example structure
    }

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // This node has no inputs, so this won't normally be called
        done();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Example: Configuration node for shared settings.
/// 
/// This demonstrates:
/// - IsConfigNode = true for config nodes
/// - Config nodes don't appear in the flow, but are selected from dropdowns
/// </summary>
[NodeType("example-server-config", "server",
    Category = NodeCategory.Config,
    Inputs = 0,
    Outputs = 0)]
public class ExampleServerConfigNode : NodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("host", "Host", placeholder: "localhost", required: true)
            .AddNumber("port", "Port", defaultValue: 1883, min: 1, max: 65535)
            .AddCheckbox("tls", "Use TLS", defaultValue: false)
            .Build();

    protected override NodeHelpText DefineHelp() =>
        HelpBuilder.Create()
            .Summary("Configuration for a server connection.")
            .Details("This configuration node stores server connection settings that can be shared between multiple nodes.")
            .Build();
}

/// <summary>
/// Example: Node that uses a configuration node.
/// 
/// This demonstrates:
/// - AddConfigNode() to reference a config node
/// - Accessing the config node at runtime
/// </summary>
[NodeType("example-mqtt-out", "mqtt out",
    Category = NodeCategory.Network,
    Color = "#D8BFD8",
    Icon = "fa fa-sign-out",
    Inputs = 1,
    Outputs = 0)]
public class ExampleMqttOutNode : NodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddConfigNode("server", "Server", "example-server-config", required: true)
            .AddText("topic", "Topic", placeholder: "my/topic")
            .AddSelect("qos", "QoS", new[]
            {
                ("0", "0 - At most once"),
                ("1", "1 - At least once"),
                ("2", "2 - Exactly once")
            }, defaultValue: "0")
            .AddCheckbox("retain", "Retain", defaultValue: false)
            .Build();

    protected override NodeHelpText DefineHelp() =>
        HelpBuilder.Create()
            .Summary("Publishes messages to an MQTT broker.")
            .AddInput("payload", "string | buffer", "The message to publish")
            .AddInput("topic", "string", "Override the configured topic")
            .Details("Connect to an MQTT broker and publish messages.")
            .AddReference("MQTT", "https://mqtt.org/")
            .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var topic = msg.Properties.ContainsKey("topic") 
                ? msg.Properties["topic"]?.ToString() 
                : GetConfig<string>("topic");

            // In a real implementation, we would:
            // 1. Get the config node using the server property
            // 2. Use the server settings to connect/publish

            Status($"Published to {topic}", StatusFill.Green, SdkStatusShape.Dot);
            done();
        }
        catch (Exception ex)
        {
            done(ex);
        }

        return Task.CompletedTask;
    }
}
