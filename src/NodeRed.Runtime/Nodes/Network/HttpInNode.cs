// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;

namespace NodeRed.Runtime.Nodes.Network;

/// <summary>
/// HTTP In node - creates an HTTP endpoint.
/// </summary>
public class HttpInNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "http in",
        Category = NodeCategory.Network,
        DisplayName = "http in",
        Color = "#6baed6",
        Icon = "fa-globe",
        Inputs = 0,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "method", "get" },
            { "url", "/" },
            { "upload", false },
            { "swaggerDoc", "" }
        }
    };

    /// <summary>
    /// Gets the HTTP method for this endpoint.
    /// </summary>
    public string Method => GetConfig<string>("method", "get").ToUpperInvariant();

    /// <summary>
    /// Gets the URL path for this endpoint.
    /// </summary>
    public string Url => GetConfig<string>("url", "/");

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // HTTP In nodes don't receive input from other nodes
        // They are triggered by incoming HTTP requests
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the HTTP handler when a request is received.
    /// </summary>
    public void HandleRequest(object request, object response)
    {
        var msg = new NodeMessage
        {
            Payload = request,
            Topic = Url
        };
        msg.Properties["req"] = request;
        msg.Properties["res"] = response;

        Send(msg);
    }
}
