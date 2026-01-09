// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Common;

/// <summary>
/// Comment node - adds a comment/annotation to the flow.
/// Does not process messages.
/// </summary>
public class CommentNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "comment",
        DisplayName = "comment",
        Category = NodeCategory.Common,
        Color = "#ffffff",
        Icon = "comment",
        Inputs = 0,
        Outputs = 0,
        Defaults = new Dictionary<string, object?>
        {
            { "name", "Comment" },
            { "info", "" }
        },
        HelpText = "A node used to add comments to flows."
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // Comment nodes don't process messages
        Done();
        return Task.CompletedTask;
    }
}
