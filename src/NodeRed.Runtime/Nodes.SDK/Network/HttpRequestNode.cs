// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Network;

/// <summary>
/// HTTP Request node - sends HTTP requests and returns the response.
/// </summary>
[NodeType("http request", "http request",
    Category = NodeCategory.Network,
    Color = "#6baed6",
    Icon = "fa fa-globe",
    Inputs = 1,
    Outputs = 1)]
public class HttpRequestNode : SdkNodeBase
{
    private readonly HttpClient _httpClient = new();

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("method", "Method", new[]
            {
                ("GET", "GET"),
                ("POST", "POST"),
                ("PUT", "PUT"),
                ("DELETE", "DELETE"),
                ("PATCH", "PATCH"),
                ("HEAD", "HEAD"),
                ("OPTIONS", "OPTIONS"),
                ("use", "- set by msg.method -")
            }, defaultValue: "GET")
            .AddText("url", "URL", icon: "fa fa-link", placeholder: "https://example.com")
            .AddCheckbox("tls", "Enable secure (SSL/TLS) connection", defaultValue: true)
            .AddSelect("ret", "Return", new[]
            {
                ("txt", "A UTF-8 string"),
                ("bin", "A binary buffer"),
                ("obj", "A parsed JSON object")
            }, defaultValue: "txt")
            .AddText("headers", "Headers", placeholder: "JSON object")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "method", "GET" },
        { "url", "" },
        { "tls", true },
        { "ret", "txt" },
        { "headers", "" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Sends HTTP requests and returns the response.")
        .AddInput("msg.url", "string", "URL override")
        .AddInput("msg.method", "string", "Method override")
        .AddInput("msg.payload", "various", "Request body for POST/PUT/PATCH")
        .AddInput("msg.headers", "object", "Request headers")
        .AddOutput("msg.payload", "string|Buffer|object", "Response body")
        .AddOutput("msg.statusCode", "number", "HTTP status code")
        .AddOutput("msg.headers", "object", "Response headers")
        .Details(@"
Sends an HTTP request and returns the response.

The **msg.payload** for POST/PUT/PATCH requests will be used as the request body.
The response is returned in **msg.payload**.")
        .Build();

    protected override async Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var url = msg.Properties.TryGetValue("url", out var u) 
                ? u?.ToString() 
                : GetConfig<string>("url", "");

            if (string.IsNullOrEmpty(url))
            {
                Error("No URL specified");
                done(new Exception("No URL specified"));
                return;
            }

            var method = GetConfig("method", "GET");
            if (method == "use" && msg.Properties.TryGetValue("method", out var m))
                method = m?.ToString()?.ToUpperInvariant() ?? "GET";

            var httpMethod = method switch
            {
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                "PATCH" => HttpMethod.Patch,
                "HEAD" => HttpMethod.Head,
                "OPTIONS" => HttpMethod.Options,
                _ => HttpMethod.Get
            };

            using var request = new HttpRequestMessage(httpMethod, url);

            if (httpMethod != HttpMethod.Get && httpMethod != HttpMethod.Head)
            {
                var body = msg.Payload?.ToString() ?? "";
                request.Content = new StringContent(body);
            }

            Status("Requesting...", StatusFill.Blue, SdkStatusShape.Ring);
            var response = await _httpClient.SendAsync(request);

            var ret = GetConfig("ret", "txt");
            object payload = ret switch
            {
                "bin" => await response.Content.ReadAsByteArrayAsync(),
                "obj" => System.Text.Json.JsonSerializer.Deserialize<object>(
                    await response.Content.ReadAsStringAsync()) ?? new object(),
                _ => await response.Content.ReadAsStringAsync()
            };

            msg.Payload = payload;
            msg.Properties["statusCode"] = (int)response.StatusCode;
            msg.Properties["headers"] = response.Headers.ToDictionary(
                h => h.Key, 
                h => string.Join(", ", h.Value));

            Status($"{(int)response.StatusCode}", StatusFill.Green, SdkStatusShape.Dot);
            send(0, msg);
            done();
        }
        catch (Exception ex)
        {
            Error(ex.Message);
            Status("Error", StatusFill.Red, SdkStatusShape.Ring);
            done(ex);
        }
    }
}
