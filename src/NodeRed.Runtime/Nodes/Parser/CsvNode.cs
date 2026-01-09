// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using System.Text;

namespace NodeRed.Runtime.Nodes.Parser;

/// <summary>
/// CSV node - converts between CSV and arrays/objects.
/// </summary>
public class CsvNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "csv",
        Category = NodeCategory.Parser,
        DisplayName = "csv",
        Color = "#DEBD5C",
        Icon = "fa-file-csv",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "sep", "," },
            { "hdrin", "" },
            { "hdrout", "none" }, // none, all, once
            { "multi", "one" }, // one, mult
            { "ret", "\\n" },
            { "temp", "" },
            { "skip", 0 }
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var sep = GetConfig<string>("sep", ",");
        var hdrin = GetConfig<string>("hdrin", "");
        var multi = GetConfig<string>("multi", "one");
        var ret = GetConfig<string>("ret", "\\n");
        if (ret == "\\n") ret = "\n";

        var payload = message.Payload;

        try
        {
            if (payload is string csvString)
            {
                // Parse CSV to array/objects
                var lines = csvString.Split(new[] { ret }, StringSplitOptions.RemoveEmptyEntries);
                var headers = string.IsNullOrEmpty(hdrin) 
                    ? (lines.Length > 0 ? lines[0].Split(sep) : Array.Empty<string>())
                    : hdrin.Split(',');

                var startIndex = string.IsNullOrEmpty(hdrin) ? 1 : 0;
                var result = new List<Dictionary<string, object>>();

                for (int i = startIndex; i < lines.Length; i++)
                {
                    var values = ParseCsvLine(lines[i], sep[0]);
                    var row = new Dictionary<string, object>();

                    for (int j = 0; j < headers.Length && j < values.Count; j++)
                    {
                        row[headers[j].Trim()] = values[j];
                    }

                    result.Add(row);
                }

                if (multi == "one")
                {
                    // Send all as one message with array payload
                    message.Payload = result;
                    Send(message);
                }
                else
                {
                    // Send each row as separate message
                    foreach (var row in result)
                    {
                        var rowMsg = new NodeMessage
                        {
                            Topic = message.Topic,
                            Payload = row
                        };
                        Send(rowMsg);
                    }
                }
            }
            else if (payload is IEnumerable<object> list)
            {
                // Convert array to CSV
                var sb = new StringBuilder();
                var first = true;

                foreach (var item in list)
                {
                    if (item is IDictionary<string, object> dict)
                    {
                        if (first)
                        {
                            // Write headers
                            sb.AppendLine(string.Join(sep, dict.Keys));
                            first = false;
                        }
                        sb.AppendLine(string.Join(sep, dict.Values.Select(v => EscapeCsvField(v?.ToString() ?? "", sep[0]))));
                    }
                    else if (item is IEnumerable<object> row)
                    {
                        sb.AppendLine(string.Join(sep, row.Select(v => EscapeCsvField(v?.ToString() ?? "", sep[0]))));
                    }
                }

                message.Payload = sb.ToString();
                Send(message);
            }
            else
            {
                Send(message);
            }
        }
        catch (Exception ex)
        {
            Log($"CSV parse error: {ex.Message}", Core.Enums.LogLevel.Error);
        }

        Done();
        return Task.CompletedTask;
    }

    private static List<string> ParseCsvLine(string line, char separator)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

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
            else if (c == separator && !inQuotes)
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
        return result;
    }

    private static string EscapeCsvField(string field, char separator)
    {
        if (field.Contains(separator) || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
