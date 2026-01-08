// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using System.Collections;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Sequence;

/// <summary>
/// Sort node - sorts message properties or sequence of messages.
/// </summary>
[NodeType("sort", "sort",
    Category = NodeCategory.Sequence,
    Color = "#c0c0c0",
    Icon = "fa fa-sort",
    Inputs = 1,
    Outputs = 1)]
public class SortNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("target", "Sort", new[]
            {
                ("payload", "msg.payload"),
                ("sequence", "Message sequence")
            }, defaultValue: "payload")
            .AddCheckbox("asNumber", "As numbers", defaultValue: false)
            .AddSelect("order", "Order", new[]
            {
                ("ascending", "Ascending"),
                ("descending", "Descending")
            }, defaultValue: "ascending")
            .AddText("key", "Key", placeholder: "property name for object sorting")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "target", "payload" },
        { "asNumber", false },
        { "order", "ascending" },
        { "key", "" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Sorts message payload or a sequence of messages.")
        .AddInput("msg.payload", "array", "Array to sort")
        .AddOutput("msg.payload", "array", "Sorted array")
        .Details("Sorts the contents of an array or a sequence of messages by their property values.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var target = GetConfig("target", "payload");
        var order = GetConfig("order", "ascending");
        var asNumber = GetConfig("asNumber", false);
        var key = GetConfig<string>("key", "");

        if (target == "payload" && msg.Payload is IEnumerable enumerable && msg.Payload is not string)
        {
            var list = enumerable.Cast<object>().ToList();

            if (string.IsNullOrEmpty(key))
            {
                if (asNumber)
                    list = list.OrderBy(x => Convert.ToDouble(x)).ToList();
                else
                    list = list.OrderBy(x => x?.ToString() ?? "").ToList();
            }
            else
            {
                list = list.OrderBy(x =>
                {
                    var propValue = GetPropertyValue(x, key);
                    return asNumber ? Convert.ToDouble(propValue) : (object)(propValue?.ToString() ?? "");
                }).ToList();
            }

            if (order == "descending")
                list.Reverse();

            msg.Payload = list;
        }

        send(0, msg);
        done();
        return Task.CompletedTask;
    }

    private static object? GetPropertyValue(object obj, string propertyName)
    {
        if (obj is IDictionary dict)
            return dict[propertyName];

        var prop = obj.GetType().GetProperty(propertyName);
        return prop?.GetValue(obj);
    }
}
