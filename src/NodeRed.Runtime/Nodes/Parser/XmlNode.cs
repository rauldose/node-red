// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using System.Xml;
using System.Xml.Linq;
using System.Text.Json;

namespace NodeRed.Runtime.Nodes.Parser;

/// <summary>
/// XML node - converts between XML and JavaScript object.
/// </summary>
public class XmlNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "xml",
        Category = NodeCategory.Parser,
        DisplayName = "xml",
        Color = "#DEBD5C",
        Icon = "fa-file-code",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "property", "payload" },
            { "attr", "-" },
            { "chr", "_" }
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

            if (data is string xmlString)
            {
                // Parse XML to object
                var doc = XDocument.Parse(xmlString);
                result = XmlToDict(doc.Root!);
            }
            else
            {
                // Convert object to XML
                var json = JsonSerializer.Serialize(data);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                var doc = new XDocument(DictToXml("root", dict!));
                result = doc.ToString();
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
            Log($"XML parse error: {ex.Message}", Core.Enums.LogLevel.Error);
        }

        Done();
        return Task.CompletedTask;
    }

    private static Dictionary<string, object> XmlToDict(XElement element)
    {
        var dict = new Dictionary<string, object>();

        // Add attributes
        foreach (var attr in element.Attributes())
        {
            dict[$"-{attr.Name.LocalName}"] = attr.Value;
        }

        // Add child elements
        var groups = element.Elements().GroupBy(e => e.Name.LocalName);
        foreach (var group in groups)
        {
            var elements = group.ToList();
            if (elements.Count == 1)
            {
                var child = elements[0];
                if (child.HasElements || child.HasAttributes)
                {
                    dict[group.Key] = XmlToDict(child);
                }
                else
                {
                    dict[group.Key] = child.Value;
                }
            }
            else
            {
                dict[group.Key] = elements.Select(e => 
                    e.HasElements || e.HasAttributes ? (object)XmlToDict(e) : e.Value
                ).ToList();
            }
        }

        // If only text content
        if (!dict.Any() && !element.HasElements)
        {
            dict["_"] = element.Value;
        }

        return dict;
    }

    private static XElement DictToXml(string name, Dictionary<string, object> dict)
    {
        var element = new XElement(name);

        foreach (var kvp in dict)
        {
            if (kvp.Key.StartsWith("-"))
            {
                // Attribute
                element.Add(new XAttribute(kvp.Key.Substring(1), kvp.Value?.ToString() ?? ""));
            }
            else if (kvp.Value is Dictionary<string, object> childDict)
            {
                element.Add(DictToXml(kvp.Key, childDict));
            }
            else if (kvp.Value is IEnumerable<object> list)
            {
                foreach (var item in list)
                {
                    if (item is Dictionary<string, object> itemDict)
                    {
                        element.Add(DictToXml(kvp.Key, itemDict));
                    }
                    else
                    {
                        element.Add(new XElement(kvp.Key, item?.ToString()));
                    }
                }
            }
            else
            {
                element.Add(new XElement(kvp.Key, kvp.Value?.ToString()));
            }
        }

        return element;
    }
}
