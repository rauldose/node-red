// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Common;

/// <summary>
/// Link Out node - sends messages to Link In nodes.
/// </summary>
public class LinkOutNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "link out",
        Category = NodeCategory.Common,
        DisplayName = "link out",
        Color = "#ddd",
        Icon = "fa-arrow-left",
        Inputs = 1,
        Outputs = 0,
        Defaults = new Dictionary<string, object?>
        {
            { "mode", "link" }, // "link" or "return"
            { "links", new List<string>() }
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var mode = GetConfig<string>("mode", "link");
        var links = GetConfig<List<string>>("links", new List<string>());

        if (mode == "link")
        {
            // Send to all linked Link In nodes
            foreach (var linkId in links)
            {
                var linkIn = LinkInNode.GetInstance(linkId);
                linkIn?.ReceiveFromLink(message);
            }
        }
        else if (mode == "return")
        {
            // Return mode - send back to the calling Link Call node
            if (message.Properties.TryGetValue("_linkSource", out var source) && source is string sourceId)
            {
                var linkCall = LinkCallNode.GetInstance(sourceId);
                linkCall?.ReceiveReturn(message);
            }
        }

        Done();
        return Task.CompletedTask;
    }
}
