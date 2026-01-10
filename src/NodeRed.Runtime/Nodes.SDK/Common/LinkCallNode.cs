// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Common;

/// <summary>
/// Link Call node - calls a Link In node and waits for response.
/// </summary>
[NodeType("link call", "link call",
    Category = NodeCategory.Common,
    Color = "#e8c28b",
    Icon = "fa fa-link",
    Inputs = 1,
    Outputs = 1)]
public class LinkCallNode : SdkNodeBase
{
    private readonly Dictionary<string, (NodeMessage msg, DateTime sent, CancellationTokenSource cts)> _pendingCalls = new();

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddNumber("timeout", "Timeout", suffix: "seconds", defaultValue: 30, min: 0)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "links", new List<string>() },
        { "timeout", 30 }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Calls a Link In node and waits for a response from a Link Out node.")
        .AddInput("msg", "object", "The message to send to the linked flow")
        .AddOutput("msg", "object", "The response message from the linked flow")
        .Details(@"
The Link Call node sends a message to a Link In node and waits
for a response from a Link Out node (in return mode).

This allows creating reusable sub-flows that can be called
like functions, with request/response semantics.

**Timeout:** If no response is received within the configured
timeout, the node will generate an error.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Check if this is a return message (from Link Out in return mode)
        if (msg.Properties.ContainsKey("_linkCallReturn"))
        {
            // This is a return from a previous call
            msg.Properties.Remove("_linkCallReturn");
            Status("", StatusFill.Grey, SdkStatusShape.Dot);
            send(0, msg);
            done();
            return Task.CompletedTask;
        }

        // This is a new outgoing call
        var timeout = GetConfig("timeout", 30);
        var links = GetConfigList("links");
        
        if (links.Count == 0)
        {
            Error("No target link configured");
            done(new InvalidOperationException("No target link configured"));
            return Task.CompletedTask;
        }

        Status("calling...", StatusFill.Blue, SdkStatusShape.Ring);
        
        // Set the _linkSource property so Link Out (return mode) knows where to send the response
        msg.Properties["_linkSource"] = Id;
        
        // Set up timeout
        var cts = new CancellationTokenSource();
        var callId = Guid.NewGuid().ToString();
        _pendingCalls[callId] = (msg, DateTime.UtcNow, cts);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(timeout), cts.Token);
                // If we reach here, timeout occurred
                if (_pendingCalls.ContainsKey(callId))
                {
                    _pendingCalls.Remove(callId);
                    Status("timeout", StatusFill.Red, SdkStatusShape.Ring);
                    Error($"Link call timeout after {timeout}s");
                }
            }
            catch (TaskCanceledException)
            {
                // Call completed before timeout - this is expected
            }
        });

        // Send to the configured Link In nodes
        // The runtime handles the actual routing
        send(0, msg);
        done();
        
        return Task.CompletedTask;
    }

    protected override Task OnCloseAsync()
    {
        // Cancel any pending calls
        foreach (var call in _pendingCalls.Values)
        {
            call.cts.Cancel();
        }
        _pendingCalls.Clear();
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
