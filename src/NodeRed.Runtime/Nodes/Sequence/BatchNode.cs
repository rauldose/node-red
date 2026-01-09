// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using System.Collections;

namespace NodeRed.Runtime.Nodes.Sequence;

/// <summary>
/// Batch node - batches messages into groups.
/// </summary>
public class BatchNode : NodeBase
{
    private readonly Dictionary<string, List<NodeMessage>> _batches = new();
    private readonly Dictionary<string, DateTime> _lastSent = new();

    public override NodeDefinition Definition => new()
    {
        Type = "batch",
        Category = NodeCategory.Sequence,
        DisplayName = "batch",
        Color = "#c0c0c0",
        Icon = "fa-object-group",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "mode", "count" }, // count, interval, concat
            { "count", 10 },
            { "overlap", 0 },
            { "interval", 10.0 },
            { "allowEmptySequence", false },
            { "topics", new List<string>() }
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var mode = GetConfig<string>("mode", "count");
        var count = GetConfig<int>("count", 10);
        var overlap = GetConfig<int>("overlap", 0);

        var key = message.Topic ?? "_default";

        if (!_batches.ContainsKey(key))
        {
            _batches[key] = new List<NodeMessage>();
        }

        _batches[key].Add(message);

        if (mode == "count" && _batches[key].Count >= count)
        {
            SendBatch(key, overlap);
        }

        Done();
        return Task.CompletedTask;
    }

    private void SendBatch(string key, int overlap)
    {
        if (!_batches.TryGetValue(key, out var batch) || batch.Count == 0)
            return;

        // Create a new message with the batch as an array
        var payloads = batch.Select(m => m.Payload).ToList();
        var batchMsg = new NodeMessage
        {
            Topic = key == "_default" ? "" : key,
            Payload = payloads
        };
        batchMsg.Properties["parts"] = new
        {
            id = Guid.NewGuid().ToString(),
            type = "array",
            count = payloads.Count,
            index = 0
        };

        Send(batchMsg);

        // Handle overlap
        if (overlap > 0 && batch.Count > overlap)
        {
            _batches[key] = batch.Skip(batch.Count - overlap).ToList();
        }
        else
        {
            _batches[key].Clear();
        }

        _lastSent[key] = DateTime.UtcNow;
    }
}
