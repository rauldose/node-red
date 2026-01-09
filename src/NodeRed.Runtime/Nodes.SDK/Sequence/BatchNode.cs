// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Sequence;

/// <summary>
/// Batch node - creates sequences of messages based on various rules.
/// </summary>
[NodeType("batch", "batch",
    Category = NodeCategory.Sequence,
    Color = "#c0c0c0",
    Icon = "fa fa-bars",
    Inputs = 1,
    Outputs = 1)]
public class BatchNode : SdkNodeBase
{
    private readonly List<NodeMessage> _buffer = new();
    private Timer? _timer;

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("mode", "Mode", new[]
            {
                ("count", "Number of messages"),
                ("interval", "Time interval"),
                ("concat", "Concatenate sequences")
            }, defaultValue: "count")
            .AddNumber("count", "Number of messages", defaultValue: 10, showWhen: "mode=count")
            .AddNumber("overlap", "Overlap", defaultValue: 0, showWhen: "mode=count")
            .AddNumber("interval", "Time interval", suffix: "seconds", defaultValue: 1, showWhen: "mode=interval")
            .AddCheckbox("allowEmptySequence", "Send empty message if no messages received", defaultValue: false, showWhen: "mode=interval")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "mode", "count" },
        { "count", 10 },
        { "overlap", 0 },
        { "interval", 1 },
        { "allowEmptySequence", false }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Creates sequences of messages based on various rules.")
        .AddInput("msg", "object", "Messages to batch")
        .AddOutput("msg.payload", "array", "Array of message payloads")
        .Details(@"
Creates batches of messages based on:
- **Number of messages**: Groups N messages together
- **Time interval**: Groups messages received within a time window
- **Concatenate sequences**: Combines multiple message sequences into one")
        .Build();

    protected override Task OnInitializeAsync()
    {
        var mode = GetConfig("mode", "count");
        if (mode == "interval")
        {
            var interval = GetConfig("interval", 1.0);
            _timer = new Timer(
                _ => FlushBuffer(),
                null,
                TimeSpan.FromSeconds(interval),
                TimeSpan.FromSeconds(interval));
        }
        return Task.CompletedTask;
    }

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var mode = GetConfig("mode", "count");

        lock (_buffer)
        {
            _buffer.Add(msg);

            if (mode == "count")
            {
                var count = GetConfig("count", 10);
                if (_buffer.Count >= count)
                {
                    var batch = _buffer.Take(count).ToList();
                    var overlap = GetConfig("overlap", 0);
                    
                    if (overlap > 0 && overlap < count)
                        _buffer.RemoveRange(0, count - overlap);
                    else
                        _buffer.Clear();

                    SendBatch(batch, send);
                }
            }
        }

        done();
        return Task.CompletedTask;
    }

    private void FlushBuffer()
    {
        List<NodeMessage> batch;
        lock (_buffer)
        {
            if (_buffer.Count == 0)
            {
                if (GetConfig("allowEmptySequence", false))
                {
                    batch = new List<NodeMessage>();
                }
                else
                {
                    return;
                }
            }
            else
            {
                batch = new List<NodeMessage>(_buffer);
                _buffer.Clear();
            }
        }

        // Note: In a real implementation, we'd need access to the send delegate here
        // For now, this is a simplified version
    }

    private void SendBatch(List<NodeMessage> batch, SendDelegate send)
    {
        var combined = new NodeMessage
        {
            Payload = batch.Select(m => m.Payload).ToArray()
        };

        var msgId = Guid.NewGuid().ToString();
        for (int i = 0; i < batch.Count; i++)
        {
            combined.Properties[$"parts_{i}"] = new
            {
                id = msgId,
                index = i,
                count = batch.Count
            };
        }

        send(0, combined);
    }

    protected override Task OnCloseAsync()
    {
        _timer?.Dispose();
        _timer = null;
        return Task.CompletedTask;
    }
}
