// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using System.Net.Sockets;
using System.Text;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Network;

/// <summary>
/// TCP Out node - provides a choice of TCP outputs.
/// </summary>
[NodeType("tcp out", "tcp out",
    Category = NodeCategory.Network,
    Color = "#6baed6",
    Icon = "fa fa-plug",
    Inputs = 1,
    Outputs = 0)]
public class TcpOutNode : SdkNodeBase
{
    private TcpClient? _client;
    private NetworkStream? _stream;

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("beserver", "Type", new[]
            {
                ("reply", "Reply to TCP"),
                ("server", "Listen on"),
                ("client", "Connect to")
            }, defaultValue: "client")
            .AddText("host", "Host", defaultValue: "localhost", showWhen: "beserver=client")
            .AddNumber("port", "Port", defaultValue: 9000, showWhen: "beserver!=reply")
            .AddCheckbox("base64", "Decode Base64 message?", defaultValue: false)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "beserver", "client" },
        { "host", "localhost" },
        { "port", 9000 },
        { "base64", false }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Provides a choice of TCP outputs.")
        .AddInput("msg.payload", "string|Buffer", "Data to send")
        .Details(@"
Can either connect to a remote TCP port, or accept incoming connections.
**msg.payload** can be a Buffer, string or Base64 encoded string.")
        .Build();

    protected override async Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var mode = GetConfig<string>("beserver", "client");
        var host = GetConfig<string>("host", "localhost");
        var port = GetConfig<int>("port", 9000);
        var decodeBase64 = GetConfig<bool>("base64", false);

        try
        {
            byte[] data;
            if (msg.Payload is byte[] bytes)
            {
                data = bytes;
            }
            else if (msg.Payload is string str)
            {
                data = decodeBase64 ? Convert.FromBase64String(str) : Encoding.UTF8.GetBytes(str);
            }
            else
            {
                data = Encoding.UTF8.GetBytes(msg.Payload?.ToString() ?? "");
            }

            if (mode == "client")
            {
                if (_client == null || !_client.Connected)
                {
                    _client?.Dispose();
                    _client = new TcpClient();
                    await _client.ConnectAsync(host!, port);
                    _stream = _client.GetStream();
                    Status($"Connected to {host}:{port}", StatusFill.Green, SdkStatusShape.Dot);
                }

                if (_stream != null)
                {
                    await _stream.WriteAsync(data);
                    await _stream.FlushAsync();
                }
            }
            
            Status("Sent", StatusFill.Green, SdkStatusShape.Dot);
        }
        catch (Exception ex)
        {
            Error($"TCP send error: {ex.Message}", msg);
            Status(ex.Message, StatusFill.Red, SdkStatusShape.Ring);
        }

        done();
    }

    protected override Task OnCloseAsync()
    {
        _stream?.Dispose();
        _client?.Dispose();
        return Task.CompletedTask;
    }
}
