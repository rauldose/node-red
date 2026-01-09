// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Network;

/// <summary>
/// TLS Config node - provides TLS/SSL configuration for secure connections.
/// This is a configuration node that other nodes can reference.
/// </summary>
[NodeType("tls-config", "TLS config",
    Category = NodeCategory.Config,
    Color = "#d8bfd8",
    Icon = "fa fa-lock",
    Inputs = 0,
    Outputs = 0)]
public class TlsConfigNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddCheckbox("verifyservercert", "Verify server certificate", defaultValue: true)
            .AddText("cert", "Certificate", icon: "fa fa-file", placeholder: "path to cert file")
            .AddText("key", "Private Key", icon: "fa fa-key", placeholder: "path to key file")
            .AddText("ca", "CA Certificate", icon: "fa fa-certificate", placeholder: "path to CA file")
            .AddText("servername", "Server Name", placeholder: "SNI hostname")
            .AddText("alpnprotocol", "ALPN Protocol", placeholder: "e.g., h2")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "cert", "" },
        { "key", "" },
        { "ca", "" },
        { "certname", "" },
        { "keyname", "" },
        { "caname", "" },
        { "servername", "" },
        { "verifyservercert", true },
        { "alpnprotocol", "" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Provides TLS/SSL configuration for secure connections.")
        .Details(@"
The TLS Config node provides SSL/TLS configuration that can be
used by other nodes to establish secure connections.

**Certificate Files:**
You can provide paths to PEM-encoded certificate files:
- **Certificate** - The client certificate
- **Private Key** - The private key for the certificate
- **CA Certificate** - Custom Certificate Authority certificate

**Options:**
- **Verify server certificate** - Whether to validate the server's certificate
- **Server Name** - The hostname to use for SNI (Server Name Indication)
- **ALPN Protocol** - Application-Layer Protocol Negotiation (e.g., 'h2' for HTTP/2)

**Usage:**
1. Create this configuration node
2. Reference it from HTTP Request, MQTT, WebSocket, or other secure nodes
3. The referenced nodes will use these TLS settings")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Config nodes don't process messages
        done();
        return Task.CompletedTask;
    }
}
