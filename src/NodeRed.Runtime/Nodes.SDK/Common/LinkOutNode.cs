// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Common;

/// <summary>
/// Link Out node - sends messages to Link In nodes.
/// </summary>
[NodeType("link out", "link out",
    Category = NodeCategory.Common,
    Color = "#e8c28b",
    Icon = "fa fa-link",
    Inputs = 1,
    Outputs = 0)]
public class LinkOutNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("mode", "Mode", new[]
            {
                ("link", "Send to all connected link nodes"),
                ("return", "Return to calling link node")
            }, defaultValue: "link")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "mode", "link" },
        { "links", new List<string>() }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Sends messages to Link In nodes, creating virtual wires.")
        .AddInput("msg", "object", "The message to send to connected Link In nodes")
        .Details(@"
The Link Out node sends messages to one or more Link In nodes,
creating virtual wires between them.

**Modes:**
- **Send to all** - Messages are sent to all connected Link In nodes
- **Return** - Messages are returned to the Link Call node that invoked this flow

This allows creating virtual wires that:
- Connect flows across different tabs
- Reduce visual clutter in complex flows
- Create reusable sub-flows")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var mode = GetConfig("mode", "link");
        
        if (mode == "return")
        {
            // Return mode - the message should have _linkSource property set by Link Call
            // The runtime handles routing back to the caller
            if (!msg.Properties.ContainsKey("_linkSource"))
            {
                Warn("Link Out in return mode received message without _linkSource");
            }
            // Send through normal routing - FlowExecutor.HandleLinkOut will handle return mode
            Send(0, msg);
        }
        else
        {
            // Normal link mode - get configured links and send to each
            var links = GetConfigList("links");
            if (links.Count == 0)
            {
                // No links configured - message is dropped
                Status("no links", StatusFill.Yellow, SdkStatusShape.Ring);
            }
            else
            {
                Status($"→ {links.Count} link(s)", StatusFill.Green, SdkStatusShape.Dot);
                // The runtime will handle cross-flow routing via the wires/links configuration
                Send(0, msg);
            }
        }
        
        done();
        return Task.CompletedTask;
    }

    private List<string> GetConfigList(string name)
    {
        var result = new List<string>();
        if (Config.Config.TryGetValue(name, out var value))
        {
            if (value is IEnumerable<object> list)
            {
                foreach (var item in list)
                {
                    if (item?.ToString() is string s && !string.IsNullOrEmpty(s))
                    {
                        result.Add(s);
                    }
                }
            }
        }
        return result;
    }
}
