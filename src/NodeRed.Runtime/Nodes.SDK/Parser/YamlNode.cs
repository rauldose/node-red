// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NodeRed.Runtime.Nodes.SDK.Parser;

/// <summary>
/// YAML node - converts between YAML string and object.
/// </summary>
[NodeType("yaml", "yaml",
    Category = NodeCategory.Parser,
    Color = "#8bbce8",
    Icon = "fa fa-file-text",
    Inputs = 1,
    Outputs = 1)]
public class YamlNode : SdkNodeBase
{
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public YamlNode()
    {
        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("action", "Action", new[]
            {
                ("auto", "Convert to/from YAML"),
                ("str", "Always convert to YAML string"),
                ("obj", "Always convert to Object")
            }, defaultValue: "auto")
            .AddText("property", "Property", defaultValue: "payload")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "action", "auto" },
        { "property", "payload" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Converts between a YAML string and its object representation.")
        .AddInput("msg.payload", "string|object", "The value to convert")
        .AddOutput("msg.payload", "object|string", "The converted value")
        .Details(@"
The YAML node converts between YAML strings and objects.

**Auto mode:**
- If input is a string, parse it to an object
- If input is an object, serialize it to YAML

YAML is often preferred over JSON for configuration files
due to its readability and support for comments.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var action = GetConfig("action", "auto");
        var property = GetConfig("property", "payload");

        try
        {
            var value = property == "payload" 
                ? msg.Payload 
                : msg.Properties.GetValueOrDefault(property);

            object? result = action switch
            {
                "str" => _serializer.Serialize(value ?? new object()),
                "obj" => value is string s ? _deserializer.Deserialize<object>(s) : value,
                _ => value is string str 
                    ? _deserializer.Deserialize<object>(str) 
                    : _serializer.Serialize(value ?? new object())
            };

            if (property == "payload")
                msg.Payload = result;
            else
                msg.Properties[property] = result;

            send(0, msg);
        }
        catch (Exception ex)
        {
            Error($"YAML error: {ex.Message}", msg);
        }

        done();
        return Task.CompletedTask;
    }
}
