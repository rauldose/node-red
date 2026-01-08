// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using System.Collections;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Sequence;

/// <summary>
/// Split node - splits a message into multiple messages.
/// </summary>
[NodeType("split", "split",
    Category = NodeCategory.Sequence,
    Color = "#c0c0c0",
    Icon = "fa fa-th-list",
    Inputs = 1,
    Outputs = 1)]
public class SplitNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("spltType", "Split using", new[]
            {
                ("str", "String"),
                ("bin", "Buffer"),
                ("len", "Fixed length")
            }, defaultValue: "str")
            .AddText("splt", "Split by", defaultValue: "\\n", showWhen: "spltType=str")
            .AddNumber("arraySplt", "Array size", defaultValue: 1, showWhen: "spltType=len")
            .AddCheckbox("stream", "Handle as a stream of messages", defaultValue: false)
            .AddText("addname", "Copy key to", placeholder: "msg.property")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "splt", "\\n" },
        { "spltType", "str" },
        { "arraySplt", 1 },
        { "stream", false },
        { "addname", "" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Splits a message into a sequence of messages.")
        .AddInput("msg.payload", "string|array|object", "The data to split")
        .AddOutput("msg.payload", "various", "Individual parts of the split data")
        .AddOutput("msg.parts", "object", "Information about the message part")
        .Details(@"
Splits a message into a sequence of messages.

**For strings:** Splits based on the configured separator (default newline).
**For arrays:** Creates a message for each array element.
**For objects:** Creates a message for each key/value pair.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var payload = msg.Payload;
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
            send(0, msg);
            done();
            return Task.CompletedTask;
        }

        var msgId = Guid.NewGuid().ToString();
        for (int i = 0; i < parts.Count; i++)
        {
            var partMsg = CloneMessage(msg);
            partMsg.Payload = parts[i];
            partMsg.Properties["parts"] = new
            {
                id = msgId,
                type = "array",
                count = parts.Count,
                index = i
            };
            send(0, partMsg);
        }

        done();
        return Task.CompletedTask;
    }
}
