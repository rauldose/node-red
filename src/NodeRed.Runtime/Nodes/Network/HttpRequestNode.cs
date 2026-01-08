// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace NodeRed.Runtime.Nodes.Network;

/// <summary>
/// HTTP Request node - makes an HTTP request.
/// </summary>
public class HttpRequestNode : NodeBase
{
    private static readonly HttpClient _httpClient = new();

    public override NodeDefinition Definition => new()
    {
        Type = "http request",
        Category = NodeCategory.Network,
        DisplayName = "http request",
        Color = "#6baed6",
        Icon = "fa-globe",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "method", "GET" },
            { "url", "" },
            { "ret", "txt" }, // txt, bin, obj, utf8
            { "paytoqs", "ignore" }, // ignore, query, body
            { "persist", false },
            { "authType", "" },
            { "senderr", false },
            { "headers", new Dictionary<string, string>() }
        }
    };

    public override async Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var method = GetConfig<string>("method", "GET");
        var url = GetConfig<string>("url", "");
        var ret = GetConfig<string>("ret", "txt");
        var paytoqs = GetConfig<string>("paytoqs", "ignore");
        var senderr = GetConfig<bool>("senderr", false);

        // Use URL from message if not configured
        if (string.IsNullOrEmpty(url) && message.Properties.TryGetValue("url", out var msgUrl))
        {
            url = msgUrl?.ToString() ?? "";
        }

        // Use method from message if provided
        if (message.Properties.TryGetValue("method", out var msgMethod))
        {
            method = msgMethod?.ToString() ?? method;
        }

        if (string.IsNullOrEmpty(url))
        {
            Log("No URL specified", LogLevel.Warning);
            Done();
            return;
        }

        try
        {
            SetStatus(NodeStatus.Processing("requesting"));

            var request = new HttpRequestMessage(new HttpMethod(method), url);

            // Add payload to body for POST/PUT/PATCH
            if (paytoqs == "body" && message.Payload != null && 
                (method == "POST" || method == "PUT" || method == "PATCH"))
            {
                var content = message.Payload is string strPayload
                    ? strPayload
                    : JsonSerializer.Serialize(message.Payload);
                request.Content = new StringContent(content, Encoding.UTF8, "application/json");
            }

            // Add headers from config
            var headers = GetConfig<Dictionary<string, string>>("headers", new Dictionary<string, string>());
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Add headers from message
            if (message.Properties.TryGetValue("headers", out var msgHeaders) && msgHeaders is Dictionary<string, string> msgHeaderDict)
            {
                foreach (var header in msgHeaderDict)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var response = await _httpClient.SendAsync(request);
            var responseMsg = new NodeMessage
            {
                Topic = message.Topic
            };

            // Get response content
            object payload = ret switch
            {
                "bin" => await response.Content.ReadAsByteArrayAsync(),
                "obj" => JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync()),
                _ => await response.Content.ReadAsStringAsync()
            };

            responseMsg.Payload = payload;
            responseMsg.Properties["statusCode"] = (int)response.StatusCode;
            responseMsg.Properties["headers"] = response.Headers.ToDictionary(
                h => h.Key,
                h => string.Join(", ", h.Value));
            responseMsg.Properties["responseUrl"] = response.RequestMessage?.RequestUri?.ToString() ?? url;

            SetStatus(NodeStatus.Success($"{(int)response.StatusCode}"));
            Send(responseMsg);
        }
        catch (Exception ex)
        {
            Log($"HTTP request error: {ex.Message}", LogLevel.Error);
            SetStatus(NodeStatus.Error(ex.Message));

            if (senderr)
            {
                message.Payload = null;
                message.Properties["error"] = ex.Message;
                Send(message);
            }
        }

        Done();
    }
}
