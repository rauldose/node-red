// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using System.Text.RegularExpressions;

namespace NodeRed.Runtime.Nodes.Parser;

/// <summary>
/// HTML node - extracts data from HTML documents using CSS selectors.
/// </summary>
public class HtmlNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "html",
        Category = NodeCategory.Parser,
        DisplayName = "html",
        Color = "#DEBD5C",
        Icon = "fa-file-alt",
        Inputs = 1,
        Outputs = 1,
        Defaults = new Dictionary<string, object?>
        {
            { "tag", "" }, // CSS selector
            { "ret", "html" }, // html, text, attr
            { "as", "single" } // single, multi
        }
    };

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var tag = GetConfig<string>("tag", "");
        var ret = GetConfig<string>("ret", "html");
        var asMode = GetConfig<string>("as", "single");

        if (message.Payload is not string htmlString || string.IsNullOrEmpty(tag))
        {
            Send(message);
            Done();
            return Task.CompletedTask;
        }

        try
        {
            // Simple tag extraction using regex (for basic cases)
            // In a production system, you'd use HtmlAgilityPack or AngleSharp
            var results = new List<string>();

            // Convert simple CSS selectors to regex patterns
            var tagName = tag.TrimStart('.', '#');
            string pattern;

            if (tag.StartsWith("."))
            {
                // Class selector
                pattern = $@"<(\w+)[^>]*class\s*=\s*[""'][^""']*{tagName}[^""']*[""'][^>]*>(.*?)</\1>";
            }
            else if (tag.StartsWith("#"))
            {
                // ID selector
                pattern = $@"<(\w+)[^>]*id\s*=\s*[""']{tagName}[""'][^>]*>(.*?)</\1>";
            }
            else
            {
                // Tag selector
                pattern = $@"<{tagName}[^>]*>(.*?)</{tagName}>";
            }

            var matches = Regex.Matches(htmlString, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                if (ret == "text")
                {
                    // Strip HTML tags
                    var text = Regex.Replace(match.Groups.Count > 2 ? match.Groups[2].Value : match.Groups[1].Value, "<[^>]+>", "");
                    results.Add(text.Trim());
                }
                else
                {
                    results.Add(match.Value);
                }
            }

            if (asMode == "single")
            {
                message.Payload = results;
                Send(message);
            }
            else
            {
                foreach (var result in results)
                {
                    var itemMsg = new NodeMessage
                    {
                        Topic = message.Topic,
                        Payload = result
                    };
                    Send(itemMsg);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"HTML parse error: {ex.Message}", Core.Enums.LogLevel.Error);
        }

        Done();
        return Task.CompletedTask;
    }
}
