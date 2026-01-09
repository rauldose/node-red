// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NodeRed.Runtime.Nodes.Parser;

/// <summary>
/// YAML node - converts between YAML string and object.
/// </summary>
public class YamlNode : NodeBase
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

    public override NodeDefinition Definition => new()
    {
        Type = "yaml",
        Category = NodeCategory.Parser,
        DisplayName = "yaml",
        Color = "#DEBD5C",
        Icon = "fa-file-code",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "property", "payload" }
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var property = GetConfig<string>("property", "payload");

        object? data;
        if (property == "payload")
        {
            data = message.Payload;
        }
        else if (message.Properties.TryGetValue(property, out var propData))
        {
            data = propData;
        }
        else
        {
            Done();
            return Task.CompletedTask;
        }

        try
        {
            object? result;

            if (data is string yamlString)
            {
                // Parse YAML to object
                result = _deserializer.Deserialize<object>(yamlString);
            }
            else
            {
                // Convert object to YAML
                result = _serializer.Serialize(data);
            }

            if (property == "payload")
            {
                message.Payload = result;
            }
            else
            {
                message.Properties[property] = result!;
            }

            Send(message);
        }
        catch (Exception ex)
        {
            Log($"YAML parse error: {ex.Message}", Core.Enums.LogLevel.Error);
        }

        Done();
        return Task.CompletedTask;
    }
}
