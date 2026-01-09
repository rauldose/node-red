// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;
using System.Net;

namespace NodeRed.Runtime.Nodes.Network;

/// <summary>
/// HTTP Proxy Config node - provides HTTP proxy configuration.
/// This is a configuration node that other nodes can reference.
/// </summary>
public class HttpProxyNode : NodeBase
{
    /// <summary>
    /// Gets the proxy URL.
    /// </summary>
    public string Url => GetConfig<string?>("url", null) ?? "";

    /// <summary>
    /// Gets the no-proxy list (comma-separated hosts that bypass the proxy).
    /// </summary>
    public string NoProxy => GetConfig<string?>("noproxy", null) ?? "";

    /// <summary>
    /// Gets the proxy credentials if configured.
    /// </summary>
    private string? Username => GetConfig<string?>("username", null);

    /// <summary>
    /// Gets whether the proxy configuration is valid.
    /// </summary>
    public bool IsValid => !string.IsNullOrEmpty(Url);

    public override NodeDefinition Definition => new()
    {
        Type = "http proxy",
        DisplayName = "http proxy",
        Category = NodeCategory.Config,
        Color = "#d8bfd8",
        Icon = "globe",
        Inputs = 0,
        Outputs = 0,
        Defaults = new Dictionary<string, object?>
        {
            { "name", "" },
            { "url", "" },
            { "noproxy", "" }
        },
        Credentials = new List<NodeCredentialDefinition>
        {
            new() { Name = "username", Type = CredentialType.Text },
            new() { Name = "password", Type = CredentialType.Password }
        },
        HelpText = "Provides HTTP proxy configuration for HTTP request nodes."
    };

    /// <summary>
    /// Creates a WebProxy configured with the node's settings.
    /// </summary>
    public IWebProxy CreateProxy()
    {
        if (!IsValid)
        {
            return WebRequest.DefaultWebProxy ?? new WebProxy();
        }

        var proxy = new WebProxy(Url);

        // Set credentials if provided
        var username = Username;
        var password = GetConfig<string?>("password", null);
        if (!string.IsNullOrEmpty(username))
        {
            proxy.Credentials = new NetworkCredential(username, password ?? "");
        }

        // Set bypass list
        if (!string.IsNullOrEmpty(NoProxy))
        {
            var bypassList = NoProxy.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(h => h.Trim())
                .Where(h => !string.IsNullOrEmpty(h))
                .Select(h => WildcardToRegex(h))
                .ToArray();
            proxy.BypassList = bypassList;
        }

        return proxy;
    }

    private static string WildcardToRegex(string pattern)
    {
        // Convert simple wildcard patterns to regex
        return "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
    }

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // Config nodes don't process messages
        Done();
        return Task.CompletedTask;
    }
}
