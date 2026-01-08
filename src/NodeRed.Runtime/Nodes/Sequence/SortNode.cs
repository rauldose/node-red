// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using System.Collections;

namespace NodeRed.Runtime.Nodes.Sequence;

/// <summary>
/// Sort node - sorts an array or sequence of messages.
/// </summary>
public class SortNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "sort",
        Category = NodeCategory.Sequence,
        DisplayName = "sort",
        Color = "#c0c0c0",
        Icon = "fa-sort-alpha-down",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "target", "payload" },
            { "targetType", "msg" },
            { "msgKey", "payload" },
            { "msgKeyType", "elem" },
            { "seqKey", "" },
            { "seqKeyType", "msg" },
            { "as_num", false },
            { "order", "ascending" }
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var target = GetConfig<string>("target", "payload");
        var order = GetConfig<string>("order", "ascending");
        var asNum = GetConfig<bool>("as_num", false);

        object? data;
        if (target == "payload")
        {
            data = message.Payload;
        }
        else if (message.Properties.TryGetValue(target, out var propData))
        {
            data = propData;
        }
        else
        {
            Done();
            return Task.CompletedTask;
        }

        if (data is IEnumerable enumerable and not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(item);
            }

            IOrderedEnumerable<object?> sorted;
            if (asNum)
            {
                sorted = order == "ascending"
                    ? list.OrderBy(x => ToDouble(x))
                    : list.OrderByDescending(x => ToDouble(x));
            }
            else
            {
                sorted = order == "ascending"
                    ? list.OrderBy(x => x?.ToString() ?? "")
                    : list.OrderByDescending(x => x?.ToString() ?? "");
            }

            var result = sorted.ToList();

            if (target == "payload")
            {
                message.Payload = result;
            }
            else
            {
                message.Properties[target] = result;
            }

            Send(message);
        }
        else
        {
            // Not an array, pass through
            Send(message);
        }

        Done();
        return Task.CompletedTask;
    }

    private static double ToDouble(object? value)
    {
        if (value == null) return 0;
        if (double.TryParse(value.ToString(), out var result)) return result;
        return 0;
    }
}
