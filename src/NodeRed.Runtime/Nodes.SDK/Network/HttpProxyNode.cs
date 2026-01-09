// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Network;

/// <summary>
/// HTTP Proxy Config node - provides HTTP proxy configuration.
/// This is a configuration node that other nodes can reference.
/// </summary>
[NodeType("http proxy", "HTTP proxy",
    Category = NodeCategory.Config,
    Color = "#d8bfd8",
    Icon = "fa fa-globe",
    Inputs = 0,
    Outputs = 0)]
public class HttpProxyNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("url", "Proxy URL", icon: "fa fa-globe", required: true,
                placeholder: "http://proxy.example.com:8080")
            .AddText("username", "Username", icon: "fa fa-user")
            .AddPassword("password", "Password")
            .AddText("noproxy", "No Proxy For", 
                placeholder: "localhost, 127.0.0.1, *.local")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "url", "" },
        { "noproxy", "" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Provides HTTP proxy configuration for HTTP request nodes.")
        .Details(@"
The HTTP Proxy node provides proxy configuration that can be used
by HTTP Request nodes to route traffic through a proxy server.

**Configuration:**
- **Proxy URL** - The full URL of the proxy server (e.g., http://proxy.example.com:8080)
- **Username/Password** - Optional credentials for authenticated proxies
- **No Proxy For** - Comma-separated list of hosts that should bypass the proxy

**No Proxy Patterns:**
You can use wildcards in the no-proxy list:
- `localhost` - Exact match
- `*.local` - All hosts ending in .local
- `192.168.*` - All IPs starting with 192.168

**Usage:**
1. Create this configuration node
2. Reference it from HTTP Request nodes
3. Those nodes will route requests through this proxy")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Config nodes don't process messages
        done();
        return Task.CompletedTask;
    }
}
