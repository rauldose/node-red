// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Common;

/// <summary>
/// Link In node - receives messages from Link Out nodes.
/// </summary>
public class LinkInNode : NodeBase
{
    private static readonly Dictionary<string, LinkInNode> _instances = new();

    public override NodeDefinition Definition => new()
    {
        Type = "link in",
        Category = NodeCategory.Common,
        DisplayName = "link in",
        Color = "#ddd",
        Icon = "fa-arrow-right",
        Inputs = 0,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "links", new List<string>() }
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
        // Link In nodes receive messages from Link Out nodes via ReceiveFromLink
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by Link Out nodes to send messages to this Link In.
    /// </summary>
    public void ReceiveFromLink(NodeMessage message)
    {
        Send(message);
    }

    /// <summary>
    /// Gets a Link In node instance by ID.
    /// </summary>
    public static LinkInNode? GetInstance(string nodeId)
    {
        return _instances.TryGetValue(nodeId, out var instance) ? instance : null;
    }
}
