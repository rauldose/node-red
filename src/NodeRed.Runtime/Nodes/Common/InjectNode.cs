// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Nodes.Common;

/// <summary>
/// Inject node - injects messages into a flow either manually or on a schedule.
/// </summary>
public class InjectNode : NodeBase
{
    private Timer? _repeatTimer;
    private CancellationTokenSource? _cts;

    public override NodeDefinition Definition => new()
    {
        Type = "inject",
        DisplayName = "inject",
        Category = NodeCategory.Common,
        Color = "#a6bbcf",
        Icon = "inject",
        Inputs = 0,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "name", "" },
            { "props", new List<object>() },
            { "repeat", "" },
            { "crontab", "" },
            { "once", false },
            { "onceDelay", 0.1 },
            { "topic", "" },
            { "payload", "" },
            { "payloadType", "date" }
        },
        HelpText = "Injects a message into a flow either manually or at regular intervals."
    };

    public override async Task InitializeAsync(FlowNode config, INodeContext context)
    {
        await base.InitializeAsync(config, context);
        _cts = new CancellationTokenSource();
        
        // Setup repeat timer if configured
        SetupRepeatTimer();

        // Trigger once on start if configured
        if (GetConfig("once", false))
        {
            var delay = GetConfig("onceDelay", 0.1);
            _ = Task.Delay(TimeSpan.FromSeconds(delay), _cts.Token)
                .ContinueWith(_ => TriggerInject(), TaskContinuationOptions.OnlyOnRanToCompletion);
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
        Send(message);
        SetStatus(NodeStatus.Success(DateTime.Now.ToString("HH:mm:ss")));
    }

    private NodeMessage CreateMessage()
    {
        var message = new NodeMessage();

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
            "flow" => Context.GetFlowContext<object>(payloadValue?.ToString() ?? ""),
            "global" => Context.GetGlobalContext<object>(payloadValue?.ToString() ?? ""),
            _ => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // Set topic
        message.Topic = GetConfig<string?>("topic", null);

        return message;
    }

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // Inject node can receive a trigger message
        TriggerInject();
        Done();
        return Task.CompletedTask;
    }

    public override Task CloseAsync()
    {
        _cts?.Cancel();
        _repeatTimer?.Dispose();
        _repeatTimer = null;
        return Task.CompletedTask;
    }
}
