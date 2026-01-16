// Translated from: packages/node_modules/@node-red/editor-client/src/js/text/bidi.js
// Bidirectional text support

namespace NodeRed.Editor.Services;

/// <summary>
/// Bidirectional text handling - translated from text/bidi.js
/// Supports right-to-left languages like Arabic, Hebrew
/// </summary>
public static class Bidi
{
    // Unicode directional characters
    private const char LRM = '\u200E'; // Left-to-Right Mark
    private const char RLM = '\u200F'; // Right-to-Left Mark
    private const char LRE = '\u202A'; // Left-to-Right Embedding
    private const char RLE = '\u202B'; // Right-to-Left Embedding
    private const char PDF = '\u202C'; // Pop Directional Formatting
    private const char LRO = '\u202D'; // Left-to-Right Override
    private const char RLO = '\u202E'; // Right-to-Left Override

    /// <summary>
    /// Determine if text is predominantly RTL
    /// </summary>
    public static bool IsRtl(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        int rtlCount = 0;
        int ltrCount = 0;

        foreach (char c in text)
        {
            if (IsRtlChar(c)) rtlCount++;
            else if (IsLtrChar(c)) ltrCount++;
        }

        return rtlCount > ltrCount;
    }

    /// <summary>
    /// Check if character is RTL
    /// </summary>
    public static bool IsRtlChar(char c)
    {
        // Arabic range
        if (c >= '\u0600' && c <= '\u06FF') return true;
        // Hebrew range
        if (c >= '\u0590' && c <= '\u05FF') return true;
        // Arabic Supplement
        if (c >= '\u0750' && c <= '\u077F') return true;
        return false;
    }

    /// <summary>
    /// Check if character is LTR
    /// </summary>
    public static bool IsLtrChar(char c)
    {
        // Latin
        if (c >= 'A' && c <= 'Z') return true;
        if (c >= 'a' && c <= 'z') return true;
        // Latin Extended
        if (c >= '\u00C0' && c <= '\u024F') return true;
        return false;
    }

    /// <summary>
    /// Add directional markers for proper display
    /// </summary>
    public static string EnforceDirection(string text, bool rtl)
    {
        if (string.IsNullOrEmpty(text)) return text;

        if (rtl)
        {
            return $"{RLE}{text}{PDF}";
        }
        else
        {
            return $"{LRE}{text}{PDF}";
        }
    }

    /// <summary>
    /// Strip directional markers from text
    /// </summary>
    public static string StripMarkers(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return text
            .Replace(LRM.ToString(), "")
            .Replace(RLM.ToString(), "")
            .Replace(LRE.ToString(), "")
            .Replace(RLE.ToString(), "")
            .Replace(PDF.ToString(), "")
            .Replace(LRO.ToString(), "")
            .Replace(RLO.ToString(), "");
    }

    /// <summary>
    /// Get the text direction for CSS
    /// </summary>
    public static string GetDirection(string text)
    {
        return IsRtl(text) ? "rtl" : "ltr";
    }
}
