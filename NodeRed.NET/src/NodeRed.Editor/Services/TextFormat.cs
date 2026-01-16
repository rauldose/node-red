// Translated from: packages/node_modules/@node-red/editor-client/src/js/text/format.js
// Text formatting utilities

using System.Text.RegularExpressions;

namespace NodeRed.Editor.Services;

/// <summary>
/// Text formatting utilities - translated from text/format.js
/// Provides text transformation and formatting functions
/// </summary>
public static class TextFormat
{
    /// <summary>
    /// Format bytes as human-readable string
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";

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
    /// Format duration in milliseconds as human-readable string
    /// </summary>
    public static string FormatDuration(long ms)
    {
        if (ms < 1000) return $"{ms}ms";
        if (ms < 60000) return $"{ms / 1000.0:0.#}s";
        if (ms < 3600000) return $"{ms / 60000.0:0.#}m";
        return $"{ms / 3600000.0:0.#}h";
    }

    /// <summary>
    /// Format a timestamp for display
    /// </summary>
    public static string FormatTimestamp(DateTime dt, bool includeDate = false)
    {
        if (includeDate)
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }
        return dt.ToString("HH:mm:ss.fff");
    }

    /// <summary>
    /// Truncate text with ellipsis
    /// </summary>
    public static string Truncate(string text, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength - suffix.Length) + suffix;
    }

    /// <summary>
    /// Escape HTML special characters
    /// </summary>
    public static string EscapeHtml(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    /// <summary>
    /// Unescape HTML entities
    /// </summary>
    public static string UnescapeHtml(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return text
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'");
    }

    /// <summary>
    /// Convert camelCase to Title Case
    /// </summary>
    public static string CamelToTitle(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return Regex.Replace(text, "([A-Z])", " $1").Trim();
    }

    /// <summary>
    /// Convert snake_case to Title Case
    /// </summary>
    public static string SnakeToTitle(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return string.Join(" ", text.Split('_')
            .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower()));
    }

    /// <summary>
    /// Format JSON for display with syntax highlighting classes
    /// </summary>
    public static string FormatJsonForDisplay(string json, bool indent = true)
    {
        try
        {
            var element = System.Text.Json.JsonDocument.Parse(json);
            if (indent)
            {
                return System.Text.Json.JsonSerializer.Serialize(
                    element, 
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            return json;
        }
        catch
        {
            return json;
        }
    }

    /// <summary>
    /// Get plural form of a word based on count
    /// </summary>
    public static string Pluralize(string singular, int count, string? plural = null)
    {
        if (count == 1) return singular;
        return plural ?? (singular + "s");
    }

    /// <summary>
    /// Format a count with proper pluralization
    /// </summary>
    public static string FormatCount(int count, string singular, string? plural = null)
    {
        return $"{count} {Pluralize(singular, count, plural)}";
    }
}
