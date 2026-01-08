// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Network;

/// <summary>
/// HTTP Response node - sends responses back to requests received from an HTTP In node.
/// </summary>
[NodeType("http response", "http response",
    Category = NodeCategory.Network,
    Color = "#6baed6",
    Icon = "fa fa-reply",
    Inputs = 1,
    Outputs = 0)]
public class HttpResponseNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddNumber("statusCode", "Status code", defaultValue: 200)
            .AddText("headers", "Headers", placeholder: "JSON object of headers")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "statusCode", 200 },
        { "headers", "" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Sends responses back to requests received from an HTTP In node.")
        .AddInput("msg.payload", "various", "Body of response")
        .AddInput("msg.statusCode", "number", "HTTP status code (overrides node setting)")
        .AddInput("msg.headers", "object", "HTTP headers (overrides node setting)")
        .AddInput("msg.cookies", "object", "Cookies to set")
        .Details(@"
This node sends a response to an HTTP request that was received by an HTTP In node.

The response can include:
- **msg.payload** - the response body
- **msg.statusCode** - the HTTP status code
- **msg.headers** - any HTTP headers to include")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var statusCode = msg.Properties.TryGetValue("statusCode", out var sc) 
            ? Convert.ToInt32(sc) 
            : GetConfig("statusCode", 200);

        if (msg.Properties.TryGetValue("res", out var resObj))
        {
            // In a real implementation, we would send the HTTP response here
            Log($"HTTP Response: {statusCode}");
        }

        Status($"{statusCode}", StatusFill.Green, SdkStatusShape.Dot);
        done();
        return Task.CompletedTask;
    }
}
