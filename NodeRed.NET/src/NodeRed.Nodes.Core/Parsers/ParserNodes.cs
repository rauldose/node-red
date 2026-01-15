// ============================================================
// SOURCE: packages/node_modules/@node-red/nodes/core/parsers/*.js
// TRANSLATION: Parser nodes - JSON, CSV, XML, HTML, YAML
// ============================================================

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NodeRed.Util;

namespace NodeRed.Nodes.Core.Parsers;

#region JSON Node
/// <summary>
/// JSON node configuration
/// SOURCE: 70-JSON.js
/// </summary>
public class JsonNodeConfig : NodeConfig
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = ""; // "", "str", "obj"
    
    [JsonPropertyName("property")]
    public string Property { get; set; } = "payload";
    
    [JsonPropertyName("pretty")]
    public bool Pretty { get; set; }
}

/// <summary>
/// JSON node - Converts between JSON string and object
/// SOURCE: packages/node_modules/@node-red/nodes/core/parsers/70-JSON.js
/// 
/// MAPPING NOTES:
/// - JSON.parse() → JsonSerializer.Deserialize()
/// - JSON.stringify() → JsonSerializer.Serialize()
/// </summary>
public class JsonNode : BaseNode
{
    private readonly string _action;
    private readonly string _property;
    private readonly bool _pretty;
    
    public JsonNode(NodeConfig config) : base(config)
    {
        var jsonConfig = config as JsonNodeConfig ?? new JsonNodeConfig();
        
        _action = jsonConfig.Action ?? "";
        _property = jsonConfig.Property ?? "payload";
        _pretty = jsonConfig.Pretty;
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                var value = GetProperty(msg, _property);
                object? result;
                
                if (_action == "str")
                {
                    // Force to string
                    result = SerializeToJson(value);
                }
                else if (_action == "obj")
                {
                    // Force to object
                    result = ParseJson(value);
                }
                else
                {
                    // Auto-detect: string->object, object->string
                    if (value is string str)
                    {
                        result = ParseJson(str);
                    }
                    else
                    {
                        result = SerializeToJson(value);
                    }
                }
                
                SetProperty(msg, _property, result);
                send(msg);
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private object? ParseJson(object? value)
    {
        if (value == null) return null;
        var str = value.ToString();
        if (string.IsNullOrEmpty(str)) return null;
        
        return JsonSerializer.Deserialize<JsonElement>(str);
    }
    
    private string SerializeToJson(object? value)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = _pretty
        };
        return JsonSerializer.Serialize(value, options);
    }
    
    private object? GetProperty(FlowMessage msg, string property)
    {
        return property switch
        {
            "payload" => msg.Payload,
            "topic" => msg.Topic,
            _ => msg.AdditionalProperties?.TryGetValue(property, out var val) == true ? val : null
        };
    }
    
    private void SetProperty(FlowMessage msg, string property, object? value)
    {
        switch (property)
        {
            case "payload":
                msg.Payload = value;
                break;
            case "topic":
                msg.Topic = value?.ToString();
                break;
            default:
                msg.AdditionalProperties ??= new Dictionary<string, JsonElement>();
                msg.AdditionalProperties[property] = JsonSerializer.Deserialize<JsonElement>(
                    JsonSerializer.Serialize(value));
                break;
        }
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("json", config => new JsonNode(config));
    }
}
#endregion

#region CSV Node
/// <summary>
/// CSV node configuration
/// SOURCE: 70-CSV.js
/// </summary>
public class CsvNodeConfig : NodeConfig
{
    [JsonPropertyName("sep")]
    public string Sep { get; set; } = ",";
    
    [JsonPropertyName("hdrin")]
    public string HdrIn { get; set; } = "";
    
    [JsonPropertyName("hdrout")]
    public string HdrOut { get; set; } = "none"; // none, once, all
    
    [JsonPropertyName("multi")]
    public string Multi { get; set; } = "one"; // one, mult
    
    [JsonPropertyName("ret")]
    public string Ret { get; set; } = "\\n";
    
    [JsonPropertyName("temp")]
    public string? Temp { get; set; }
    
    [JsonPropertyName("skip")]
    public int Skip { get; set; } = 0;
    
    [JsonPropertyName("strings")]
    public bool Strings { get; set; } = true;
    
    [JsonPropertyName("include_empty_strings")]
    public bool IncludeEmptyStrings { get; set; }
    
    [JsonPropertyName("include_null_values")]
    public bool IncludeNullValues { get; set; }
}

/// <summary>
/// CSV node - Parses and generates CSV data
/// SOURCE: packages/node_modules/@node-red/nodes/core/parsers/70-CSV.js
/// </summary>
public class CsvNode : BaseNode
{
    private readonly string _sep;
    private readonly string _hdrOut;
    private readonly string _multi;
    private readonly string _ret;
    private readonly string[] _template;
    private readonly int _skip;
    private readonly bool _strings;
    private bool _headerSent;
    
    public CsvNode(NodeConfig config) : base(config)
    {
        var csvConfig = config as CsvNodeConfig ?? new CsvNodeConfig();
        
        _sep = string.IsNullOrEmpty(csvConfig.Sep) ? "," : csvConfig.Sep;
        _hdrOut = csvConfig.HdrOut ?? "none";
        _multi = csvConfig.Multi ?? "one";
        _ret = csvConfig.Ret ?? "\\n";
        _skip = csvConfig.Skip;
        _strings = csvConfig.Strings;
        _headerSent = false;
        
        // Parse template columns
        _template = !string.IsNullOrEmpty(csvConfig.Temp) 
            ? csvConfig.Temp.Split(',').Select(t => t.Trim()).ToArray()
            : Array.Empty<string>();
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                if (msg.Payload is string str)
                {
                    // Parse CSV string to objects
                    var result = ParseCsv(str);
                    
                    if (_multi == "mult")
                    {
                        // Send array of objects
                        msg.Payload = result;
                        send(msg);
                    }
                    else
                    {
                        // Send one message per row
                        var count = result.Count;
                        for (int i = 0; i < count; i++)
                        {
                            var rowMsg = msg.Clone();
                            rowMsg.Payload = result[i];
                            
                            // Add parts for sequence handling
                            rowMsg.Parts = new MessageParts
                            {
                                Id = msg.MsgId,
                                Index = i,
                                Count = count,
                                Type = "array"
                            };
                            
                            send(rowMsg);
                        }
                    }
                }
                else
                {
                    // Convert object(s) to CSV string
                    var csv = GenerateCsv(msg.Payload);
                    msg.Payload = csv;
                    send(msg);
                }
                
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private List<Dictionary<string, object?>> ParseCsv(string csv)
    {
        var result = new List<Dictionary<string, object?>>();
        var lines = csv.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        
        string[]? headers = null;
        var lineIndex = 0;
        
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            // Skip initial lines
            if (lineIndex < _skip)
            {
                lineIndex++;
                continue;
            }
            
            var values = ParseCsvLine(line);
            
            if (headers == null)
            {
                // First row is headers (or use template)
                headers = _template.Length > 0 ? _template : values;
                if (_template.Length > 0)
                {
                    // First row is data, not headers
                    result.Add(CreateRow(headers, values));
                }
            }
            else
            {
                result.Add(CreateRow(headers, values));
            }
            
            lineIndex++;
        }
        
        return result;
    }
    
    private string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c.ToString() == _sep && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        
        result.Add(current.ToString());
        return result.ToArray();
    }
    
    private Dictionary<string, object?> CreateRow(string[] headers, string[] values)
    {
        var row = new Dictionary<string, object?>();
        
        for (int i = 0; i < headers.Length; i++)
        {
            var header = headers[i];
            var value = i < values.Length ? values[i] : "";
            
            if (_strings)
            {
                row[header] = value;
            }
            else
            {
                // Try to parse as number
                if (double.TryParse(value, out var num))
                    row[header] = num;
                else if (bool.TryParse(value, out var b))
                    row[header] = b;
                else
                    row[header] = value;
            }
        }
        
        return row;
    }
    
    private string GenerateCsv(object? payload)
    {
        var sb = new StringBuilder();
        var newline = _ret == "\\n" ? "\n" : _ret == "\\r\\n" ? "\r\n" : _ret;
        
        var items = new List<object?>();
        if (payload is IEnumerable<object> enumerable)
            items.AddRange(enumerable);
        else
            items.Add(payload);
        
        bool firstRow = true;
        foreach (var item in items)
        {
            if (item is not IDictionary<string, object?> dict)
            {
                if (item is JsonElement je && je.ValueKind == JsonValueKind.Object)
                {
                    dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(je.GetRawText()) 
                        ?? new Dictionary<string, object?>();
                }
                else continue;
            }
            
            var keys = dict.Keys.ToArray();
            
            // Output header if needed
            if (firstRow && (_hdrOut == "all" || (_hdrOut == "once" && !_headerSent)))
            {
                sb.AppendLine(string.Join(_sep, keys.Select(k => EscapeCsvValue(k))));
                _headerSent = true;
            }
            
            // Output values
            var values = keys.Select(k => EscapeCsvValue(dict[k]?.ToString() ?? ""));
            sb.Append(string.Join(_sep, values));
            sb.Append(newline);
            
            firstRow = false;
        }
        
        return sb.ToString();
    }
    
    private string EscapeCsvValue(string value)
    {
        if (value.Contains(_sep) || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("csv", config => new CsvNode(config));
    }
}
#endregion

#region XML Node
/// <summary>
/// XML node configuration
/// SOURCE: 70-XML.js
/// </summary>
public class XmlNodeConfig : NodeConfig
{
    [JsonPropertyName("attr")]
    public string Attr { get; set; } = "";
    
    [JsonPropertyName("chr")]
    public string Chr { get; set; } = "_";
    
    [JsonPropertyName("property")]
    public string Property { get; set; } = "payload";
}

/// <summary>
/// XML node - Converts between XML string and object
/// SOURCE: packages/node_modules/@node-red/nodes/core/parsers/70-XML.js
/// </summary>
public class XmlNode : BaseNode
{
    private readonly string _attrPrefix;
    private readonly string _charKey;
    private readonly string _property;
    
    public XmlNode(NodeConfig config) : base(config)
    {
        var xmlConfig = config as XmlNodeConfig ?? new XmlNodeConfig();
        
        _attrPrefix = xmlConfig.Attr ?? "$";
        _charKey = xmlConfig.Chr ?? "_";
        _property = xmlConfig.Property ?? "payload";
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                var value = GetProperty(msg, _property);
                object? result;
                
                if (value is string str)
                {
                    // Parse XML to object
                    result = ParseXml(str);
                }
                else
                {
                    // Convert object to XML
                    result = GenerateXml(value);
                }
                
                SetProperty(msg, _property, result);
                send(msg);
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private Dictionary<string, object?> ParseXml(string xml)
    {
        var doc = XDocument.Parse(xml);
        return ElementToDict(doc.Root!);
    }
    
    private Dictionary<string, object?> ElementToDict(XElement element)
    {
        var dict = new Dictionary<string, object?>();
        
        // Add attributes
        foreach (var attr in element.Attributes())
        {
            dict[_attrPrefix + attr.Name.LocalName] = attr.Value;
        }
        
        // Add child elements
        var childGroups = element.Elements().GroupBy(e => e.Name.LocalName);
        foreach (var group in childGroups)
        {
            var items = group.ToList();
            if (items.Count == 1)
            {
                var child = items[0];
                if (child.HasElements || child.HasAttributes)
                {
                    dict[group.Key] = ElementToDict(child);
                }
                else
                {
                    dict[group.Key] = child.Value;
                }
            }
            else
            {
                dict[group.Key] = items.Select(e => 
                    e.HasElements || e.HasAttributes ? (object)ElementToDict(e) : e.Value
                ).ToList();
            }
        }
        
        // Add text content if present and no child elements
        if (!element.HasElements && !string.IsNullOrEmpty(element.Value))
        {
            if (dict.Count > 0)
            {
                dict[_charKey] = element.Value;
            }
            else
            {
                return new Dictionary<string, object?> { [element.Name.LocalName] = element.Value };
            }
        }
        
        return new Dictionary<string, object?> { [element.Name.LocalName] = dict };
    }
    
    private string GenerateXml(object? value)
    {
        if (value == null) return "";
        
        if (value is IDictionary<string, object?> dict)
        {
            var rootName = dict.Keys.FirstOrDefault() ?? "root";
            var rootValue = dict[rootName];
            
            var element = DictToElement(rootName, rootValue);
            return element.ToString();
        }
        
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            var objDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(je.GetRawText());
            return GenerateXml(objDict);
        }
        
        return $"<value>{value}</value>";
    }
    
    private XElement DictToElement(string name, object? value)
    {
        var element = new XElement(name);
        
        if (value is IDictionary<string, object?> dict)
        {
            foreach (var kv in dict)
            {
                if (kv.Key.StartsWith(_attrPrefix))
                {
                    element.Add(new XAttribute(kv.Key.Substring(_attrPrefix.Length), kv.Value?.ToString() ?? ""));
                }
                else if (kv.Key == _charKey)
                {
                    element.Value = kv.Value?.ToString() ?? "";
                }
                else if (kv.Value is IEnumerable<object> list)
                {
                    foreach (var item in list)
                    {
                        element.Add(DictToElement(kv.Key, item));
                    }
                }
                else
                {
                    element.Add(DictToElement(kv.Key, kv.Value));
                }
            }
        }
        else
        {
            element.Value = value?.ToString() ?? "";
        }
        
        return element;
    }
    
    private object? GetProperty(FlowMessage msg, string property)
    {
        return property switch
        {
            "payload" => msg.Payload,
            _ => msg.AdditionalProperties?.TryGetValue(property, out var val) == true ? val : null
        };
    }
    
    private void SetProperty(FlowMessage msg, string property, object? value)
    {
        if (property == "payload")
            msg.Payload = value;
        else
        {
            msg.AdditionalProperties ??= new Dictionary<string, JsonElement>();
            msg.AdditionalProperties[property] = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(value));
        }
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("xml", config => new XmlNode(config));
    }
}
#endregion

#region HTML Node
/// <summary>
/// HTML node configuration
/// SOURCE: 70-HTML.js
/// </summary>
public class HtmlNodeConfig : NodeConfig
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "";
    
    [JsonPropertyName("ret")]
    public string Ret { get; set; } = "html"; // html, text, attr
    
    [JsonPropertyName("as")]
    public string As { get; set; } = "single"; // single, multi
}

/// <summary>
/// HTML node - Extracts elements from HTML
/// SOURCE: packages/node_modules/@node-red/nodes/core/parsers/70-HTML.js
/// 
/// NOTE: This is a simplified implementation using regex.
/// For production use, consider using HtmlAgilityPack or AngleSharp.
/// </summary>
public class HtmlNode : BaseNode
{
    private readonly string _selector;
    private readonly string _ret;
    private readonly string _as;
    
    public HtmlNode(NodeConfig config) : base(config)
    {
        var htmlConfig = config as HtmlNodeConfig ?? new HtmlNodeConfig();
        
        _selector = htmlConfig.Tag ?? "";
        _ret = htmlConfig.Ret ?? "html";
        _as = htmlConfig.As ?? "single";
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                if (msg.Payload is string html)
                {
                    var results = ExtractElements(html, _selector);
                    
                    if (_as == "multi")
                    {
                        // Send one message per match
                        for (int i = 0; i < results.Count; i++)
                        {
                            var matchMsg = msg.Clone();
                            matchMsg.Payload = results[i];
                            matchMsg.Parts = new MessageParts
                            {
                                Id = msg.MsgId,
                                Index = i,
                                Count = results.Count
                            };
                            send(matchMsg);
                        }
                    }
                    else
                    {
                        // Send array
                        msg.Payload = results;
                        send(msg);
                    }
                }
                
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private List<string> ExtractElements(string html, string selector)
    {
        var results = new List<string>();
        
        // Simple tag extraction using regex
        // Note: This is a simplified implementation
        var tagName = Regex.Match(selector, @"^(\w+)").Groups[1].Value;
        if (string.IsNullOrEmpty(tagName)) tagName = "div";
        
        var pattern = $@"<{tagName}[^>]*>(.*?)</{tagName}>";
        var matches = Regex.Matches(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            var content = _ret switch
            {
                "text" => Regex.Replace(match.Groups[1].Value, "<[^>]+>", ""),
                "html" => match.Groups[1].Value,
                _ => match.Value
            };
            results.Add(content.Trim());
        }
        
        return results;
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("html", config => new HtmlNode(config));
    }
}
#endregion

#region YAML Node
/// <summary>
/// YAML node configuration
/// SOURCE: 70-YAML.js
/// </summary>
public class YamlNodeConfig : NodeConfig
{
    [JsonPropertyName("property")]
    public string Property { get; set; } = "payload";
}

/// <summary>
/// YAML node - Converts between YAML string and object
/// SOURCE: packages/node_modules/@node-red/nodes/core/parsers/70-YAML.js
/// 
/// NOTE: This is a simplified implementation.
/// For production use, consider using YamlDotNet library.
/// </summary>
public class YamlNode : BaseNode
{
    private readonly string _property;
    
    public YamlNode(NodeConfig config) : base(config)
    {
        var yamlConfig = config as YamlNodeConfig ?? new YamlNodeConfig();
        _property = yamlConfig.Property ?? "payload";
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                var value = GetProperty(msg, _property);
                object? result;
                
                if (value is string str)
                {
                    // Parse YAML to object (simplified - only handles basic cases)
                    result = ParseSimpleYaml(str);
                }
                else
                {
                    // Convert object to YAML (simplified)
                    result = GenerateSimpleYaml(value);
                }
                
                SetProperty(msg, _property, result);
                send(msg);
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private Dictionary<string, object?> ParseSimpleYaml(string yaml)
    {
        var result = new Dictionary<string, object?>();
        var lines = yaml.Split('\n');
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                continue;
            
            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex > 0)
            {
                var key = trimmed.Substring(0, colonIndex).Trim();
                var value = trimmed.Substring(colonIndex + 1).Trim();
                
                // Remove quotes
                if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                    (value.StartsWith("'") && value.EndsWith("'")))
                {
                    value = value.Substring(1, value.Length - 2);
                }
                
                // Try to parse as number or boolean
                if (double.TryParse(value, out var num))
                    result[key] = num;
                else if (bool.TryParse(value, out var b))
                    result[key] = b;
                else if (value == "null" || value == "~")
                    result[key] = null;
                else
                    result[key] = value;
            }
        }
        
        return result;
    }
    
    private string GenerateSimpleYaml(object? value, int indent = 0)
    {
        var sb = new StringBuilder();
        var prefix = new string(' ', indent * 2);
        
        if (value is IDictionary<string, object?> dict)
        {
            foreach (var kv in dict)
            {
                if (kv.Value is IDictionary<string, object?> || kv.Value is IEnumerable<object>)
                {
                    sb.AppendLine($"{prefix}{kv.Key}:");
                    sb.Append(GenerateSimpleYaml(kv.Value, indent + 1));
                }
                else
                {
                    var val = kv.Value?.ToString() ?? "null";
                    if (val.Contains(':') || val.Contains('#'))
                        val = $"\"{val}\"";
                    sb.AppendLine($"{prefix}{kv.Key}: {val}");
                }
            }
        }
        else if (value is IEnumerable<object> list)
        {
            foreach (var item in list)
            {
                sb.AppendLine($"{prefix}- {item}");
            }
        }
        else
        {
            sb.AppendLine(value?.ToString() ?? "null");
        }
        
        return sb.ToString();
    }
    
    private object? GetProperty(FlowMessage msg, string property)
    {
        return property switch
        {
            "payload" => msg.Payload,
            _ => msg.AdditionalProperties?.TryGetValue(property, out var val) == true ? val : null
        };
    }
    
    private void SetProperty(FlowMessage msg, string property, object? value)
    {
        if (property == "payload")
            msg.Payload = value;
        else
        {
            msg.AdditionalProperties ??= new Dictionary<string, JsonElement>();
            msg.AdditionalProperties[property] = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(value));
        }
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("yaml", config => new YamlNode(config));
    }
}
#endregion
