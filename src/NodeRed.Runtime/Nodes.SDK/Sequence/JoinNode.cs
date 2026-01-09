// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using System.Collections.Concurrent;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Sequence;

/// <summary>
/// Join node - joins a sequence of messages into a single message.
/// </summary>
[NodeType("join", "join",
    Category = NodeCategory.Sequence,
    Color = "#c0c0c0",
    Icon = "fa fa-th-list",
    Inputs = 1,
    Outputs = 1)]
public class JoinNode : SdkNodeBase
{
    private readonly ConcurrentDictionary<string, List<object>> _accumulator = new();
    private readonly ConcurrentDictionary<string, int> _expectedCounts = new();

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("mode", "Mode", new[]
            {
                ("auto", "Automatic"),
                ("custom", "Manual")
            }, defaultValue: "auto")
            .AddSelect("build", "Combine each", new[]
            {
                ("array", "Array"),
                ("string", "String"),
                ("object", "Object")
            }, defaultValue: "array", showWhen: "mode=custom")
            .AddText("property", "Using", defaultValue: "payload")
            .AddText("joiner", "Join using", showWhen: "build=string")
            .AddNumber("count", "After a number of messages", showWhen: "mode=custom")
            .AddNumber("timeout", "Or a timeout of", suffix: "seconds", showWhen: "mode=custom")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "mode", "auto" },
        { "build", "array" },
        { "property", "payload" },
        { "joiner", "" },
        { "count", 0 },
        { "timeout", 0 }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Joins a sequence of messages into a single message.")
        .AddInput("msg.payload", "various", "Data to accumulate")
        .AddInput("msg.parts", "object", "Part information from split node")
        .AddOutput("msg.payload", "array|string|object", "Combined data")
        .Details(@"
Joins a sequence of messages into a single message.

In **Automatic** mode, uses msg.parts from split node.
In **Manual** mode, accumulates until count or timeout reached.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var mode = GetConfig("mode", "auto");
        
        if (mode == "auto" && msg.Properties.TryGetValue("parts", out var partsObj))
        {
            HandlePartsMessage(msg, partsObj, send, done);
        }
        else
        {
            HandleManualMessage(msg, send, done);
        }

        return Task.CompletedTask;
    }

    private void HandlePartsMessage(NodeMessage msg, object partsObj, SendDelegate send, DoneDelegate done)
    {
        dynamic parts = partsObj;
        string id = parts.id?.ToString() ?? Guid.NewGuid().ToString();
        int count = (int)(parts.count ?? 0);
        int index = (int)(parts.index ?? 0);

        _accumulator.TryAdd(id, new List<object>());
        _expectedCounts.TryAdd(id, count);

        _accumulator[id].Add(msg.Payload);

        if (_accumulator[id].Count >= _expectedCounts[id])
        {
            var combined = new NodeMessage
            {
                Topic = msg.Topic,
                Payload = _accumulator[id].ToArray()
            };
            send(0, combined);

            _accumulator.TryRemove(id, out _);
            _expectedCounts.TryRemove(id, out _);
        }

        done();
    }

    private void HandleManualMessage(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var count = GetConfig("count", 0);
        const string key = "manual";

        _accumulator.TryAdd(key, new List<object>());
        _accumulator[key].Add(msg.Payload);

        if (count > 0 && _accumulator[key].Count >= count)
        {
            var build = GetConfig("build", "array");
            object payload = build switch
            {
                "string" => string.Join(GetConfig("joiner", ""), _accumulator[key]),
                _ => _accumulator[key].ToArray()
            };

            var combined = new NodeMessage
            {
                Topic = msg.Topic,
                Payload = payload
            };
            send(0, combined);

            _accumulator[key].Clear();
        }

        done();
    }
}
