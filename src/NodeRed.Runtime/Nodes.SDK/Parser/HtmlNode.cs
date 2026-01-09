// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using System.Text.RegularExpressions;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Parser;

/// <summary>
/// HTML node - extracts elements from an HTML document.
/// </summary>
/// <remarks>
/// Note: This implementation uses regex for simple tag extraction.
/// For production use, consider HtmlAgilityPack or AngleSharp for proper HTML parsing.
/// </remarks>
[NodeType("html", "html",
    Category = NodeCategory.Parser,
    Color = "#c0c0c0",
    Icon = "fa fa-code",
    Inputs = 1,
    Outputs = 1)]
public class HtmlNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("tag", "Selector", icon: "fa fa-code", placeholder: "div.classname or #id")
            .AddSelect("ret", "Output", new[]
            {
                ("html", "The HTML content of the elements"),
                ("text", "Only the text content"),
                ("attr", "An object of the attributes")
            }, defaultValue: "html")
            .AddText("as", "Property", defaultValue: "payload")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "tag", "" },
        { "ret", "html" },
        { "as", "payload" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Extracts elements from an HTML document.")
        .AddInput("msg.payload", "string", "HTML string to parse")
        .AddOutput("msg.payload", "array", "Array of extracted elements")
        .Details(@"
Uses a simplified CSS selector to extract elements from an HTML document.

**Note:** This is a basic implementation using regex. For complex HTML parsing,
consider using a proper HTML parser library.

**Supported selectors:**
- Tag name: `div`, `p`, `span`
- Class: `.classname`
- ID: `#idname`")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var html = msg.Payload?.ToString() ?? "";
            var tag = GetConfig<string>("tag", "");
            var ret = GetConfig("ret", "html");

            if (string.IsNullOrEmpty(tag))
            {
                msg.Payload = html;
                send(0, msg);
                done();
                return Task.CompletedTask;
            }

            var results = new List<string>();
            string pattern;

            if (tag.StartsWith("#"))
            {
                // ID selector
                var id = tag.Substring(1);
                pattern = $@"<\w+[^>]*id\s*=\s*[""']{Regex.Escape(id)}[""'][^>]*>(.*?)</\w+>";
            }
            else if (tag.StartsWith("."))
            {
                // Class selector
                var className = tag.Substring(1);
                pattern = $@"<\w+[^>]*class\s*=\s*[""'][^""']*{Regex.Escape(className)}[^""']*[""'][^>]*>(.*?)</\w+>";
            }
            else
            {
                // Tag selector
                pattern = $@"<{Regex.Escape(tag)}[^>]*>(.*?)</{Regex.Escape(tag)}>";
            }

            var matches = Regex.Matches(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                if (ret == "text")
                {
                    // Strip HTML tags to get text only
                    var text = Regex.Replace(match.Groups[1].Value, "<[^>]+>", "");
                    results.Add(text.Trim());
                }
                else
                {
                    results.Add(match.Value);
                }
            }

            msg.Payload = results;
            send(0, msg);
            done();
        }
        catch (Exception ex)
        {
            Error(ex.Message);
            done(ex);
        }

        return Task.CompletedTask;
    }
}
