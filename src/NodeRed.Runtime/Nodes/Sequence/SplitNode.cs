// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using System.Collections;

namespace NodeRed.Runtime.Nodes.Sequence;

/// <summary>
/// Split node - splits a message into multiple messages.
/// </summary>
public class SplitNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "split",
        Category = NodeCategory.Sequence,
        DisplayName = "split",
        Color = "#c0c0c0",
        Icon = "fa-th-list",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "splt", "\\n" },
            { "spltType", "str" },
            { "arraySplt", 1 },
            { "arraySpltType", "len" },
            { "stream", false },
            { "addname", "" }
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var payload = message.Payload;
        var parts = new List<object>();

        if (payload is string strPayload)
        {
            var splt = GetConfig<string>("splt", "\\n");
            if (splt == "\\n") splt = "\n";
            parts.AddRange(strPayload.Split(splt).Cast<object>());
        }
        else if (payload is IEnumerable<object> enumerable)
        {
            parts.AddRange(enumerable);
        }
        else if (payload is IList list)
        {
            foreach (var item in list)
            {
                parts.Add(item);
            }
        }
        else if (payload is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                parts.Add(new { key = entry.Key, payload = entry.Value });
            }
        }
        else
        {
            // Can't split, send as-is
            Send(message);
            Done();
            return Task.CompletedTask;
        }

        // Send each part as a separate message
        var msgId = Guid.NewGuid().ToString();
        for (int i = 0; i < parts.Count; i++)
        {
            var partMsg = new NodeMessage
            {
                Topic = message.Topic,
                Payload = parts[i]
            };
            partMsg.Properties["parts"] = new
            {
                id = msgId,
                type = "array",
                count = parts.Count,
                index = i
            };
            Send(partMsg);
        }

        Done();
        return Task.CompletedTask;
    }
}
