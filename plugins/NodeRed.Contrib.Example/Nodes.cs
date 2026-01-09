// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Humanizer;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;

namespace NodeRed.Contrib.Example;

/// <summary>
/// Example: A node that transforms text to uppercase.
/// Demonstrates basic node creation with SDK.
/// </summary>
[NodeType("example-upper", "to upper", 
    Category = NodeCategory.Function,
    Color = "#87A980",
    Icon = "fa fa-font",
    Inputs = 1, 
    Outputs = 1)]
public class UppercaseNode : NodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .Build();

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Converts the payload to uppercase.")
        .AddInput("msg.payload", "string", "Text to convert")
        .AddOutput("msg.payload", "string", "Uppercase text")
        .Details("This node converts the incoming payload string to uppercase letters.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        if (msg.Payload is string text)
        {
            msg.Payload = text.ToUpperInvariant();
        }
        else
        {
            msg.Payload = msg.Payload?.ToString()?.ToUpperInvariant() ?? "";
        }
        
        send(0, msg);
        done();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Example: A node that transforms text to lowercase.
/// </summary>
[NodeType("example-lower", "to lower",
    Category = NodeCategory.Function,
    Color = "#87A980",
    Icon = "fa fa-font",
    Inputs = 1,
    Outputs = 1)]
public class LowercaseNode : NodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .Build();

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Converts the payload to lowercase.")
        .AddInput("msg.payload", "string", "Text to convert")
        .AddOutput("msg.payload", "string", "Lowercase text")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        if (msg.Payload is string text)
        {
            msg.Payload = text.ToLowerInvariant();
        }
        else
        {
            msg.Payload = msg.Payload?.ToString()?.ToLowerInvariant() ?? "";
        }
        
        send(0, msg);
        done();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Example: A node that humanizes text using the Humanizer library.
/// This demonstrates using a plugin-specific dependency.
/// </summary>
[NodeType("example-humanize", "humanize",
    Category = NodeCategory.Function,
    Color = "#C7E9C0",
    Icon = "fa fa-user",
    Inputs = 1,
    Outputs = 1)]
public class HumanizeNode : NodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("mode", "Mode", new[]
            {
                ("humanize", "Humanize (PascalCase → Pascal Case)"),
                ("pascalize", "Pascalize (hello world → HelloWorld)"),
                ("camelize", "Camelize (hello world → helloWorld)"),
                ("underscore", "Underscore (HelloWorld → hello_world)"),
                ("dasherize", "Dasherize (HelloWorld → hello-world)"),
                ("pluralize", "Pluralize (word → words)"),
                ("singularize", "Singularize (words → word)")
            })
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        ["mode"] = "humanize"
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Transforms text using the Humanizer library.")
        .AddInput("msg.payload", "string", "Text to transform")
        .AddOutput("msg.payload", "string", "Transformed text")
        .Details(@"This node uses the Humanizer library to transform text.
Available modes:
- **Humanize**: Converts PascalCase to 'Pascal Case'
- **Pascalize**: Converts 'hello world' to 'HelloWorld'
- **Camelize**: Converts 'hello world' to 'helloWorld'
- **Underscore**: Converts 'HelloWorld' to 'hello_world'
- **Dasherize**: Converts 'HelloWorld' to 'hello-world'
- **Pluralize**: Converts singular to plural
- **Singularize**: Converts plural to singular")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var text = msg.Payload?.ToString() ?? "";
        var mode = GetConfig("mode", "humanize");

        msg.Payload = mode switch
        {
            "humanize" => text.Humanize(),
            "pascalize" => text.Pascalize(),
            "camelize" => text.Camelize(),
            "underscore" => text.Underscore(),
            "dasherize" => text.Dasherize(),
            "pluralize" => text.Pluralize(),
            "singularize" => text.Singularize(),
            _ => text.Humanize()
        };

        Status($"Mode: {mode}", StatusFill.Green);
        send(0, msg);
        done();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Example: A timer node that generates messages at intervals.
/// Demonstrates using OnInitializeAsync and OnCloseAsync for lifecycle management.
/// </summary>
[NodeType("example-timer", "timer",
    Category = NodeCategory.Common,
    Color = "#a6bbcf",
    Icon = "fa fa-clock-o",
    Inputs = 0,
    Outputs = 1,
    HasButton = true)]
public class TimerNode : NodeBase
{
    private Timer? _timer;
    private int _count;

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddNumber("interval", "Interval", suffix: "seconds", defaultValue: 5, min: 1)
            .AddCheckbox("autoStart", "Auto Start", defaultValue: false)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        ["interval"] = 5,
        ["autoStart"] = false
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Generates messages at regular intervals.")
        .AddOutput("msg.payload", "number", "Message count")
        .AddOutput("msg.timestamp", "number", "Unix timestamp (ms)")
        .Details("Click the button to start/stop the timer. Set Auto Start to begin on deploy.")
        .Build();

    protected override NodeButtonDefinition? DefineButton() => new()
    {
        Action = "toggle"
    };

    protected override Task OnInitializeAsync()
    {
        var autoStart = GetConfig("autoStart", false);
        if (autoStart)
        {
            StartTimer();
        }
        else
        {
            Status("Stopped", StatusFill.Grey, SdkStatusShape.Ring);
        }
        return Task.CompletedTask;
    }

    protected override Task OnCloseAsync()
    {
        StopTimer();
        return Task.CompletedTask;
    }

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Toggle timer on input (button click)
        if (_timer == null)
        {
            StartTimer();
        }
        else
        {
            StopTimer();
        }
        done();
        return Task.CompletedTask;
    }

    private void StartTimer()
    {
        var intervalSeconds = GetConfig("interval", 5.0);
        var intervalMs = (int)(intervalSeconds * 1000);
        
        _count = 0;
        _timer = new Timer(_ => SendTimerMessage(), null, 0, intervalMs);
        Status("Running", StatusFill.Green);
        Log($"Timer started with interval {intervalSeconds}s");
    }

    private void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
        Status("Stopped", StatusFill.Grey, SdkStatusShape.Ring);
        Log("Timer stopped");
    }

    private void SendTimerMessage()
    {
        _count++;
        var msg = NewMessage(_count);
        msg.Properties["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        // We can't use send delegate from timer callback directly,
        // so we just create and send the message via context
        // This would need proper implementation in the runtime
    }
}

/// <summary>
/// Example: A counter node with persistent state.
/// Demonstrates using flow context to persist data.
/// </summary>
[NodeType("example-counter", "counter",
    Category = NodeCategory.Function,
    Color = "#E2D96E",
    Icon = "fa fa-plus",
    Inputs = 1,
    Outputs = 1)]
public class CounterNode : NodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddNumber("initial", "Initial Value", defaultValue: 0)
            .AddNumber("step", "Step", defaultValue: 1)
            .AddCheckbox("reset", "Reset on Deploy", defaultValue: false)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        ["initial"] = 0,
        ["step"] = 1,
        ["reset"] = false
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Counts messages and outputs the current count.")
        .AddInput("msg", "any", "Any message increments the counter")
        .AddOutput("msg.payload", "number", "Current count value")
        .AddOutput("msg.count", "number", "Same as payload")
        .Details(@"Each message received increments the counter by the configured step value.
The count is stored in flow context and persists across restarts unless 'Reset on Deploy' is enabled.

Send `msg.reset = true` to reset the counter to the initial value.")
        .Build();

    protected override Task OnInitializeAsync()
    {
        var resetOnDeploy = GetConfig("reset", false);
        if (resetOnDeploy)
        {
            var initial = GetConfig("initial", 0.0);
            Flow.Set($"counter_{Id}", initial);
        }
        
        UpdateStatus();
        return Task.CompletedTask;
    }

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var initial = GetConfig("initial", 0.0);
        var step = GetConfig("step", 1.0);
        
        // Check for reset command
        if (msg.Properties.TryGetValue("reset", out var resetVal) && resetVal is true)
        {
            Flow.Set($"counter_{Id}", initial);
            msg.Payload = initial;
            msg.Properties["count"] = initial;
            UpdateStatus();
            send(0, msg);
            done();
            return Task.CompletedTask;
        }
        
        // Get current count
        var count = Flow.Get<double>($"counter_{Id}");
        if (count == 0 && !Flow.Get<bool>($"counter_{Id}_initialized"))
        {
            count = initial;
            Flow.Set($"counter_{Id}_initialized", true);
        }
        
        // Increment
        count += step;
        Flow.Set($"counter_{Id}", count);
        
        msg.Payload = count;
        msg.Properties["count"] = count;
        
        UpdateStatus();
        send(0, msg);
        done();
        return Task.CompletedTask;
    }

    private void UpdateStatus()
    {
        var count = Flow.Get<double>($"counter_{Id}");
        Status($"Count: {count}", StatusFill.Blue);
    }
}
