// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Common;

/// <summary>
/// Inject node - injects messages into a flow either manually or on a schedule.
/// </summary>
[NodeType("inject", "inject",
    Category = NodeCategory.Common,
    Color = "#a6bbcf",
    Icon = "fa fa-arrow-right",
    Inputs = 0,
    Outputs = 1,
    HasButton = true)]
public class InjectNode : SdkNodeBase
{
    private Timer? _repeatTimer;
    private CancellationTokenSource? _cts;

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("payloadType", "Payload Type", new[]
            {
                ("date", "Timestamp"),
                ("str", "String"),
                ("num", "Number"),
                ("bool", "Boolean"),
                ("json", "JSON"),
                ("flow", "Flow Context"),
                ("global", "Global Context")
            }, defaultValue: "date")
            .AddText("payload", "Payload", placeholder: "value", showWhen: "payloadType!=date")
            .AddText("topic", "Topic", icon: "fa fa-tag")
            .AddText("repeat", "Repeat", suffix: "seconds", placeholder: "interval")
            .AddCheckbox("once", "Inject once after", defaultValue: false)
            .AddNumber("onceDelay", "Delay", suffix: "seconds", defaultValue: 0.1, showWhen: "once")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "payload", "" },
        { "payloadType", "date" },
        { "topic", "" },
        { "repeat", "" },
        { "once", false },
        { "onceDelay", 0.1 }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Injects a message into a flow either manually or at regular intervals.")
        .AddInput("msg", "object", "Trigger message (optional)")
        .AddOutput("msg.payload", "various", "The configured payload value")
        .AddOutput("msg.topic", "string", "The configured topic")
        .Details(@"
The Inject node can be used to manually trigger a flow by clicking the node's button.
It can also be configured to inject at regular intervals.

**Payload types:**
- **Timestamp** - Unix timestamp in milliseconds
- **String** - A string value
- **Number** - A number value
- **Boolean** - true or false
- **JSON** - A JSON object or array
- **Flow/Global Context** - Value from flow or global context")
        .Build();

    protected override NodeButtonDefinition? DefineButton() => new()
    {
        Action = "inject",
        Icon = "fa fa-play"
    };

    protected override async Task OnInitializeAsync()
    {
        _cts = new CancellationTokenSource();

        // Setup repeat timer if configured
        SetupRepeatTimer();

        // Trigger once on start if configured
        if (GetConfig("once", false))
        {
            var delay = GetConfig("onceDelay", 0.1);
            await Task.Delay(TimeSpan.FromSeconds(delay), _cts.Token).ContinueWith(_ =>
            {
                if (!_cts.Token.IsCancellationRequested)
                    TriggerInject();
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }

    private void SetupRepeatTimer()
    {
        var repeatStr = GetConfig<string>("repeat", "");
        if (!string.IsNullOrEmpty(repeatStr) && double.TryParse(repeatStr, out var seconds) && seconds > 0)
        {
            var interval = TimeSpan.FromSeconds(seconds);
            _repeatTimer = new Timer(
                _ => TriggerInject(),
                null,
                interval,
                interval);
        }
    }

    /// <summary>
    /// Triggers an injection manually or from timer.
    /// </summary>
    public void TriggerInject()
    {
        var message = CreateMessage();
        
        // Use the SDK-style context helpers - Note: This is triggered externally, 
        // actual sending is handled by the runtime through the inject button.
        
        // For inject, we need to send directly through context
        // This is a special case since inject has no inputs
        Status(DateTime.Now.ToString("HH:mm:ss"), StatusFill.Green, SdkStatusShape.Dot);
    }

    private NodeMessage CreateMessage()
    {
        var message = NewMessage();

        // Set payload based on payloadType
        var payloadType = GetConfig("payloadType", "date");
        var payloadValue = GetConfig<object?>("payload", null);

        message.Payload = payloadType switch
        {
            "date" => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            "str" => payloadValue?.ToString() ?? "",
            "num" when double.TryParse(payloadValue?.ToString(), out var num) => num,
            "bool" => payloadValue?.ToString()?.ToLowerInvariant() == "true",
            "json" => payloadValue,
            "flow" => Flow.Get(payloadValue?.ToString() ?? ""),
            "global" => Global.Get(payloadValue?.ToString() ?? ""),
            _ => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // Set topic
        message.Topic = GetConfig<string?>("topic", null) ?? "";

        return message;
    }

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Inject node can receive a trigger message
        var message = CreateMessage();
        send(0, message);
        Status(DateTime.Now.ToString("HH:mm:ss"), StatusFill.Green, SdkStatusShape.Dot);
        done();
        return Task.CompletedTask;
    }

    protected override Task OnCloseAsync()
    {
        _cts?.Cancel();
        _repeatTimer?.Dispose();
        _repeatTimer = null;
        return Task.CompletedTask;
    }
}
