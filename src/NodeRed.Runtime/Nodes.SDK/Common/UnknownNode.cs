// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Common;

/// <summary>
/// Unknown node - a placeholder for nodes that are not installed or recognized.
/// This node is used when a flow references a node type that doesn't exist.
/// </summary>
[NodeType("unknown", "unknown",
    Category = NodeCategory.Common,
    Color = "#c0c0c0",
    Icon = "fa fa-exclamation-triangle",
    Inputs = 1,
    Outputs = 1)]
public class UnknownNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddInfo("This node represents an unknown or missing node type.")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Placeholder for unknown or missing node types.")
        .Details(@"
This node is automatically inserted when a flow references a node type
that is not installed or recognized.

This can happen when:
- A required node package is not installed
- A node type has been renamed or removed
- Importing a flow from another system with different nodes

To resolve this:
1. Install the missing node package
2. Replace this node with the correct node type
3. Update the flow to use alternative nodes")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Unknown nodes don't process messages but log a warning
        Warn("Unknown node type - message dropped");
        done();
        return Task.CompletedTask;
    }
}
