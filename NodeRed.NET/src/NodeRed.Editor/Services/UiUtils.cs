using System.Text.RegularExpressions;

namespace NodeRed.Editor.Services;

/// <summary>
/// Translated from: @node-red/editor-client/src/js/ui/utils.js
/// UI utility functions
/// </summary>
public static class UiUtils
{
    /// <summary>
    /// Escape HTML special characters
    /// </summary>
    public static string EscapeHtml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    /// <summary>
    /// Format a value for display (handles objects, arrays, etc.)
    /// </summary>
    public static string FormatValue(object? value, int maxLength = 100)
    {
        if (value == null) return "null";
        
        var str = value.ToString() ?? "";
        if (str.Length > maxLength)
        {
            str = str.Substring(0, maxLength) + "...";
        }
        return str;
    }

    /// <summary>
    /// Format bytes to human-readable size
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Format a duration in milliseconds to human-readable format
    /// </summary>
    public static string FormatDuration(long milliseconds)
    {
        if (milliseconds < 1000)
            return $"{milliseconds}ms";
        if (milliseconds < 60000)
            return $"{milliseconds / 1000.0:0.#}s";
        if (milliseconds < 3600000)
            return $"{milliseconds / 60000.0:0.#}m";
        return $"{milliseconds / 3600000.0:0.#}h";
    }

    /// <summary>
    /// Format a timestamp to relative time (e.g., "5 minutes ago")
    /// </summary>
    public static string FormatRelativeTime(DateTime timestamp)
    {
        var diff = DateTime.UtcNow - timestamp;

        if (diff.TotalSeconds < 60)
            return "just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} minute{((int)diff.TotalMinutes != 1 ? "s" : "")} ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} hour{((int)diff.TotalHours != 1 ? "s" : "")} ago";
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays} day{((int)diff.TotalDays != 1 ? "s" : "")} ago";
        
        return timestamp.ToString("yyyy-MM-dd HH:mm");
    }

    /// <summary>
    /// Truncate text with ellipsis
    /// </summary>
    public static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? "";
        return text.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Highlight search matches in text
    /// </summary>
    public static string HighlightMatches(string text, string search)
    {
        if (string.IsNullOrEmpty(search))
            return EscapeHtml(text);

        var escapedSearch = Regex.Escape(search);
        var regex = new Regex($"({escapedSearch})", RegexOptions.IgnoreCase);
        var escaped = EscapeHtml(text);
        
        return regex.Replace(escaped, "<mark>$1</mark>");
    }

    /// <summary>
    /// Generate a unique ID
    /// </summary>
    public static string GenerateId()
    {
        return Convert.ToHexString(Guid.NewGuid().ToByteArray())
            .Substring(0, 16)
            .ToLowerInvariant();
    }

    /// <summary>
    /// Parse a color string to RGB components
    /// </summary>
    public static (int R, int G, int B)? ParseColor(string color)
    {
        if (string.IsNullOrEmpty(color)) return null;

        // Handle hex colors
        if (color.StartsWith("#"))
        {
            var hex = color.Substring(1);
            if (hex.Length == 3)
            {
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            }
            if (hex.Length == 6)
            {
                return (
                    Convert.ToInt32(hex.Substring(0, 2), 16),
                    Convert.ToInt32(hex.Substring(2, 2), 16),
                    Convert.ToInt32(hex.Substring(4, 2), 16)
                );
            }
        }

        // Handle rgb() colors
        var rgbMatch = Regex.Match(color, @"rgb\((\d+),\s*(\d+),\s*(\d+)\)");
        if (rgbMatch.Success)
        {
            return (
                int.Parse(rgbMatch.Groups[1].Value),
                int.Parse(rgbMatch.Groups[2].Value),
                int.Parse(rgbMatch.Groups[3].Value)
            );
        }

        return null;
    }

    /// <summary>
    /// Get contrasting text color (black or white) for a background color
    /// </summary>
    public static string GetContrastColor(string backgroundColor)
    {
        var rgb = ParseColor(backgroundColor);
        if (rgb == null) return "#000000";

        // Calculate relative luminance
        var luminance = (0.299 * rgb.Value.R + 0.587 * rgb.Value.G + 0.114 * rgb.Value.B) / 255;
        return luminance > 0.5 ? "#000000" : "#FFFFFF";
    }

    /// <summary>
    /// Debounce a function call
    /// </summary>
    public static Func<Task> Debounce(Func<Task> action, int delayMs)
    {
        var cts = new CancellationTokenSource();
        
        return async () =>
        {
            cts.Cancel();
            cts = new CancellationTokenSource();
            
            try
            {
                await Task.Delay(delayMs, cts.Token);
                await action();
            }
            catch (TaskCanceledException)
            {
                // Debounced
            }
        };
    }

    /// <summary>
    /// Get node label for display
    /// </summary>
    public static string GetNodeLabel(Dictionary<string, object?> node)
    {
        if (node.TryGetValue("_def", out var def) && def is Dictionary<string, object?> defDict)
        {
            // Check for label function
            if (node.TryGetValue("name", out var name) && !string.IsNullOrEmpty(name?.ToString()))
            {
                return name.ToString()!;
            }

            if (defDict.TryGetValue("label", out var label))
            {
                return label?.ToString() ?? node["type"]?.ToString() ?? "node";
            }
        }

        return node.TryGetValue("name", out var n) && !string.IsNullOrEmpty(n?.ToString()) 
            ? n.ToString()! 
            : node["type"]?.ToString() ?? "node";
    }
}
