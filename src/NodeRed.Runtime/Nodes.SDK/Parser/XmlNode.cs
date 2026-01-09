// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Xml.Linq;
using System.Text.Json;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Parser;

/// <summary>
/// XML node - converts between XML string and object.
/// </summary>
[NodeType("xml", "xml",
    Category = NodeCategory.Parser,
    Color = "#8bbce8",
    Icon = "fa fa-code",
    Inputs = 1,
    Outputs = 1)]
public class XmlNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("action", "Action", new[]
            {
                ("auto", "Convert to/from XML"),
                ("str", "Always convert to XML string"),
                ("obj", "Always convert to Object")
            }, defaultValue: "auto")
            .AddText("property", "Property", defaultValue: "payload")
            .AddCheckbox("indent", "Format XML (pretty print)", defaultValue: false)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "action", "auto" },
        { "property", "payload" },
        { "indent", false }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Converts between an XML string and its object representation.")
        .AddInput("msg.payload", "string|object", "The value to convert")
        .AddOutput("msg.payload", "object|string", "The converted value")
        .Details(@"
The XML node converts between XML strings and objects.

**Auto mode:**
- If input is a string, parse it to an object
- If input is an object, serialize it to XML

The object format uses a simple structure where
element names become property keys.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var action = GetConfig("action", "auto");
        var property = GetConfig("property", "payload");
        var indent = GetConfig("indent", false);

        try
        {
            var value = property == "payload" 
                ? msg.Payload 
                : msg.Properties.GetValueOrDefault(property);

            object? result = action switch
            {
                "str" => ToXmlString(value, indent),
                "obj" => value is string s ? XmlToObject(s) : value,
                _ => value is string str ? XmlToObject(str) : ToXmlString(value, indent)
            };

            if (property == "payload")
                msg.Payload = result;
            else
                msg.Properties[property] = result;

            send(0, msg);
        }
        catch (Exception ex)
        {
            Error($"XML error: {ex.Message}", msg);
        }

        done();
        return Task.CompletedTask;
    }

    private static object? XmlToObject(string xml)
    {
        var doc = XDocument.Parse(xml);
        return ElementToObject(doc.Root!);
    }

    private static object? ElementToObject(XElement element)
    {
        if (!element.HasElements)
        {
            return element.Value;
        }

        var dict = new Dictionary<string, object?>();
        foreach (var child in element.Elements())
        {
            dict[child.Name.LocalName] = ElementToObject(child);
        }
        return dict;
    }

    private static string ToXmlString(object? value, bool indent)
    {
        var root = ObjectToElement("root", value);
        var options = indent ? SaveOptions.None : SaveOptions.DisableFormatting;
        return root.ToString(options);
    }

    private static XElement ObjectToElement(string name, object? value)
    {
        if (value is Dictionary<string, object?> dict)
        {
            var element = new XElement(name);
            foreach (var kvp in dict)
            {
                element.Add(ObjectToElement(kvp.Key, kvp.Value));
            }
            return element;
        }

        if (value is JsonElement jsonElement)
        {
            return JsonElementToXml(name, jsonElement);
        }

        return new XElement(name, value?.ToString() ?? "");
    }

    private static XElement JsonElementToXml(string name, JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ObjectToElement(name, 
                element.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value)),
            JsonValueKind.Array => new XElement(name, 
                element.EnumerateArray().Select((e, i) => JsonElementToXml($"item{i}", e))),
            _ => new XElement(name, element.ToString())
        };
    }
}
