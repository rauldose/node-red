// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Sequence;

/// <summary>
/// Join node - joins a sequence of messages into a single message.
/// </summary>
public class JoinNode : NodeBase
{
    private readonly Dictionary<string, List<(int index, object payload)>> _pendingParts = new();
    private readonly Dictionary<string, int> _expectedCounts = new();

    public override NodeDefinition Definition => new()
    {
        Type = "join",
        Category = NodeCategory.Sequence,
        DisplayName = "join",
        Color = "#c0c0c0",
        Icon = "fa-list",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "mode", "auto" }, // auto, custom
            { "build", "array" }, // array, string, object, buffer
            { "property", "" },
            { "propertyType", "full" },
            { "key", "topic" },
            { "joiner", "\\n" },
            { "joinerType", "str" },
            { "accumulate", false },
            { "timeout", 0.0 },
            { "count", 0 },
            { "reduceRight", false },
            { "reduceExp", "" },
            { "reduceInit", "" },
            { "reduceInitType", "str" },
            { "reduceFixup", "" }
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var mode = GetConfig<string>("mode", "auto");
        var build = GetConfig<string>("build", "array");
        var joiner = GetConfig<string>("joiner", "\\n");
        if (joiner == "\\n") joiner = "\n";

        if (mode == "auto" && message.Properties.TryGetValue("parts", out var partsObj))
        {
            // Auto mode - use parts info from split node
            dynamic parts = partsObj;
            string id = parts.id?.ToString() ?? "";
            int count = parts.count;
            int index = parts.index;

            if (!_pendingParts.ContainsKey(id))
            {
                _pendingParts[id] = new List<(int, object)>();
                _expectedCounts[id] = count;
            }

            _pendingParts[id].Add((index, message.Payload!));

            if (_pendingParts[id].Count >= _expectedCounts[id])
            {
                // All parts received, join them
                var orderedParts = _pendingParts[id]
                    .OrderBy(p => p.index)
                    .Select(p => p.payload)
                    .ToList();

                object joined = build switch
                {
                    "string" => string.Join(joiner, orderedParts.Select(p => p?.ToString() ?? "")),
                    "object" => orderedParts.ToDictionary(
                        p => (p as dynamic)?.key?.ToString() ?? "",
                        p => (object?)((p as dynamic)?.payload ?? p)),
                    _ => orderedParts // array
                };

                var joinedMsg = new NodeMessage
                {
                    Topic = message.Topic,
                    Payload = joined
                };
                Send(joinedMsg);

                _pendingParts.Remove(id);
                _expectedCounts.Remove(id);
            }
        }
        else
        {
            // Manual mode - accumulate based on count
            var count = GetConfig<int>("count", 0);
            if (count > 0)
            {
                var key = "manual";
                if (!_pendingParts.ContainsKey(key))
                {
                    _pendingParts[key] = new List<(int, object)>();
                }

                _pendingParts[key].Add((_pendingParts[key].Count, message.Payload!));

                if (_pendingParts[key].Count >= count)
                {
                    var parts = _pendingParts[key].Select(p => p.payload).ToList();
                    
                    object joined = build switch
                    {
                        "string" => string.Join(joiner, parts.Select(p => p?.ToString() ?? "")),
                        _ => parts
                    };

                    var joinedMsg = new NodeMessage
                    {
                        Topic = message.Topic,
                        Payload = joined
                    };
                    Send(joinedMsg);

                    _pendingParts.Remove(key);
                }
            }
            else
            {
                // No count configured, pass through
                Send(message);
            }
        }

        Done();
        return Task.CompletedTask;
    }
}
