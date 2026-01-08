// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Parser;

/// <summary>
/// CSV node - converts between CSV string and object array.
/// </summary>
[NodeType("csv", "csv",
    Category = NodeCategory.Parser,
    Color = "#8bbce8",
    Icon = "fa fa-table",
    Inputs = 1,
    Outputs = 1)]
public class CsvNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddSelect("action", "Action", new[]
            {
                ("auto", "Convert to/from CSV"),
                ("str", "Always convert to CSV string"),
                ("obj", "Always convert to Object array")
            }, defaultValue: "auto")
            .AddText("sep", "Separator", defaultValue: ",", isSmall: true)
            .AddCheckbox("hdrin", "First row contains headers", defaultValue: true)
            .AddCheckbox("hdrout", "Include header row", defaultValue: true)
            .AddCheckbox("skip", "Skip empty strings", defaultValue: false)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "action", "auto" },
        { "sep", "," },
        { "hdrin", true },
        { "hdrout", true },
        { "skip", false }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Converts between a CSV string and an array of objects.")
        .AddInput("msg.payload", "string|array", "The value to convert")
        .AddOutput("msg.payload", "array|string", "The converted value")
        .Details(@"
The CSV node parses and generates CSV (Comma Separated Values) data.

**Options:**
- **Separator** - Character to use between values (default: comma)
- **First row headers** - Use first row as property names
- **Include headers** - Add header row when generating CSV
- **Skip empty** - Ignore empty string values

When parsing, each row becomes an object with properties
matching the column headers.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var action = GetConfig("action", "auto");
        var sep = GetConfig("sep", ",");
        var hdrin = GetConfig("hdrin", true);
        var hdrout = GetConfig("hdrout", true);
        var skip = GetConfig("skip", false);

        try
        {
            var value = msg.Payload;

            object? result = action switch
            {
                "str" => ToCsvString(value, sep, hdrout),
                "obj" => value is string s ? ParseCsv(s, sep, hdrin, skip) : value,
                _ => value is string str 
                    ? ParseCsv(str, sep, hdrin, skip) 
                    : ToCsvString(value, sep, hdrout)
            };

            msg.Payload = result;
            send(0, msg);
        }
        catch (Exception ex)
        {
            Error($"CSV error: {ex.Message}", msg);
        }

        done();
        return Task.CompletedTask;
    }

    private static List<Dictionary<string, object?>> ParseCsv(string csv, string sep, bool hasHeaders, bool skipEmpty)
    {
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return new List<Dictionary<string, object?>>();

        var result = new List<Dictionary<string, object?>>();
        string[] headers;
        int startLine;

        if (hasHeaders)
        {
            headers = ParseLine(lines[0], sep);
            startLine = 1;
        }
        else
        {
            var firstLine = ParseLine(lines[0], sep);
            headers = Enumerable.Range(0, firstLine.Length).Select(i => $"col{i + 1}").ToArray();
            startLine = 0;
        }

        for (int i = startLine; i < lines.Length; i++)
        {
            var values = ParseLine(lines[i], sep);
            var row = new Dictionary<string, object?>();

            for (int j = 0; j < headers.Length && j < values.Length; j++)
            {
                var value = values[j];
                if (skipEmpty && string.IsNullOrEmpty(value)) continue;
                row[headers[j]] = value;
            }

            result.Add(row);
        }

        return result;
    }

    private static string[] ParseLine(string line, string sep)
    {
        return line.Trim().Split(sep[0]);
    }

    private static string ToCsvString(object? value, string sep, bool includeHeaders)
    {
        if (value is not IEnumerable<object> enumerable) 
        {
            return value?.ToString() ?? "";
        }

        var items = enumerable.ToList();
        if (items.Count == 0) return "";

        var lines = new List<string>();
        string[]? headers = null;

        foreach (var item in items)
        {
            if (item is Dictionary<string, object?> dict)
            {
                if (headers == null)
                {
                    headers = dict.Keys.ToArray();
                    if (includeHeaders)
                    {
                        lines.Add(string.Join(sep, headers));
                    }
                }
                lines.Add(string.Join(sep, headers.Select(h => dict.GetValueOrDefault(h)?.ToString() ?? "")));
            }
        }

        return string.Join("\n", lines);
    }
}
