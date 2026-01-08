// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Common;

/// <summary>
/// Link Call node - calls a Link In node and waits for a response.
/// </summary>
public class LinkCallNode : NodeBase
{
    private static readonly Dictionary<string, LinkCallNode> _instances = new();

    public override NodeDefinition Definition => new()
    {
        Type = "link call",
        Category = NodeCategory.Common,
        DisplayName = "link call",
        Color = "#ddd",
        Icon = "fa-link",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "links", new List<string>() },
            { "timeout", 30 }
        }
    };

    public override Task InitializeAsync(FlowNode config, Core.Interfaces.INodeContext context)
    {
        var result = base.InitializeAsync(config, context);
        _instances[config.Id] = this;
        return result;
    }

    public override Task CloseAsync()
    {
        _instances.Remove(Config.Id);
        return base.CloseAsync();
    }

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var links = GetConfig<List<string>>("links", new List<string>());

        if (links.Count > 0)
        {
            // Mark message with this node as the return target
            message.Properties["_linkSource"] = Config.Id;

            // Send to the first linked Link In node
            var linkIn = LinkInNode.GetInstance(links[0]);
            linkIn?.ReceiveFromLink(message);
        }
        else
        {
            // No links configured, pass through
            Send(message);
        }

        Done();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Receives the return message from a Link Out node.
    /// </summary>
    public void ReceiveReturn(NodeMessage message)
    {
        message.Properties.Remove("_linkSource");
        Send(message);
    }

    /// <summary>
    /// Gets a Link Call node instance by ID.
    /// </summary>
    public static LinkCallNode? GetInstance(string nodeId)
    {
        return _instances.TryGetValue(nodeId, out var instance) ? instance : null;
    }
}
