// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using System.Net;
using System.Net.Sockets;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Network;

/// <summary>
/// TCP In node - provides a choice of TCP inputs.
/// </summary>
[NodeType("tcp in", "tcp in",
    Category = NodeCategory.Network,
    Color = "#6baed6",
    Icon = "fa fa-plug",
    Inputs = 0,
    Outputs = 1)]
public class TcpInNode : SdkNodeBase
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("server", "Type", new[]
            {
                ("server", "Listen on"),
                ("client", "Connect to")
            }, defaultValue: "server")
            .AddText("host", "Host", defaultValue: "localhost", showWhen: "server=client")
            .AddNumber("port", "Port", defaultValue: 9000)
            .AddSelect("datamode", "Output", new[]
            {
                ("stream", "Stream of data"),
                ("single", "Single message")
            }, defaultValue: "stream")
            .AddSelect("datatype", "as", new[]
            {
                ("buffer", "A Buffer object"),
                ("utf8", "A UTF-8 string"),
                ("base64", "A Base64 encoded string")
            }, defaultValue: "buffer")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "server", "server" },
        { "host", "localhost" },
        { "port", 9000 },
        { "datamode", "stream" },
        { "datatype", "buffer" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Provides a choice of TCP inputs.")
        .AddOutput("msg.payload", "string|Buffer", "Received data")
        .AddOutput("msg._session", "object", "Session information")
        .Details(@"
Can be configured to:
- **Listen** on a port for incoming TCP connections
- **Connect** to a remote TCP server")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        done();
        return Task.CompletedTask;
    }

    protected override Task OnCloseAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        return Task.CompletedTask;
    }
}
