// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Network;

/// <summary>
/// HTTP In node - creates an HTTP endpoint.
/// </summary>
[NodeType("http in", "http in",
    Category = NodeCategory.Network,
    Color = "#6baed6",
    Icon = "fa fa-globe",
    Inputs = 0,
    Outputs = 1)]
public class HttpInNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("method", "Method", new[]
            {
                ("get", "GET"),
                ("post", "POST"),
                ("put", "PUT"),
                ("delete", "DELETE"),
                ("patch", "PATCH")
            }, defaultValue: "get")
            .AddText("url", "URL", icon: "fa fa-link", defaultValue: "/")
            .AddCheckbox("upload", "Accept file uploads", defaultValue: false)
            .AddText("swaggerDoc", "Swagger description")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "method", "get" },
        { "url", "/" },
        { "upload", false },
        { "swaggerDoc", "" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Creates an HTTP endpoint for creating web services.")
        .AddOutput("msg.req", "object", "HTTP request object")
        .AddOutput("msg.res", "object", "HTTP response object (used to send response)")
        .AddOutput("msg.payload", "string|object", "Request body/query")
        .Details(@"
Creates an HTTP endpoint that listens for incoming requests.

The node outputs:
- **msg.req** - the HTTP request object
- **msg.res** - the HTTP response object
- **msg.payload** - for GET: query string params; for POST: body")
        .Build();

    public string Method => GetConfig("method", "get").ToUpperInvariant();
    public string Url => GetConfig("url", "/");

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // HTTP In nodes are triggered by HTTP requests, not message input
        done();
        return Task.CompletedTask;
    }

    public void HandleRequest(object request, object response)
    {
        var msg = NewMessage();
        msg.Payload = request;
        msg.Topic = Url;
        msg.Properties["req"] = request;
        msg.Properties["res"] = response;
        // Note: Would need to send through proper channel
    }
}
