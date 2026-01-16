using System.Text.RegularExpressions;

namespace NodeRed.Editor.Services;

/// <summary>
/// Translated from: @node-red/editor-client/src/js/validators.js
/// Input validation utilities for node property editors
/// </summary>
public static class Validators
{
    /// <summary>
    /// Validate that a value is a number
    /// </summary>
    public static Func<string, bool> Number() => value =>
    {
        if (string.IsNullOrEmpty(value)) return true;
        return double.TryParse(value, out _);
    };

    /// <summary>
    /// Validate that a value is a positive number
    /// </summary>
    public static Func<string, bool> PositiveNumber() => value =>
    {
        if (string.IsNullOrEmpty(value)) return true;
        return double.TryParse(value, out var num) && num >= 0;
    };

    /// <summary>
    /// Validate that a value is an integer
    /// </summary>
    public static Func<string, bool> Integer() => value =>
    {
        if (string.IsNullOrEmpty(value)) return true;
        return int.TryParse(value, out _);
    };

    /// <summary>
    /// Validate that a value matches a regex pattern
    /// </summary>
    public static Func<string, bool> Regex(string pattern, bool allowBlank = false) => value =>
    {
        if (string.IsNullOrEmpty(value)) return allowBlank;
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(value, pattern);
        }
        catch
        {
            return false;
        }
    };

    /// <summary>
    /// Validate that a value is a valid regex pattern
    /// </summary>
    public static Func<string, bool> RegexPattern() => value =>
    {
        if (string.IsNullOrEmpty(value)) return true;
        try
        {
            _ = new Regex(value);
            return true;
        }
        catch
        {
            return false;
        }
    };

    /// <summary>
    /// Validate typed input value based on type
    /// </summary>
    public static Func<string, string, bool> TypedInput() => (value, type) =>
    {
        return type switch
        {
            "num" => string.IsNullOrEmpty(value) || double.TryParse(value, out _),
            "bool" => value == "true" || value == "false" || string.IsNullOrEmpty(value),
            "json" => IsValidJson(value),
            "jsonata" => !string.IsNullOrEmpty(value), // Basic check
            "re" => RegexPattern()(value),
            "str" => true,
            "msg" => IsValidPropertyExpression(value),
            "flow" => IsValidPropertyExpression(value),
            "global" => IsValidPropertyExpression(value),
            "env" => IsValidEnvironmentVariableName(value),
            _ => true
        };
    };

    /// <summary>
    /// Validate that a value is valid JSON
    /// </summary>
    public static bool IsValidJson(string value)
    {
        if (string.IsNullOrEmpty(value)) return true;
        try
        {
            System.Text.Json.JsonDocument.Parse(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validate a property expression (e.g., msg.payload, flow.data)
    /// </summary>
    public static bool IsValidPropertyExpression(string value)
    {
        if (string.IsNullOrEmpty(value)) return true;
        
        // Basic property path validation
        // Allows: identifier, identifier.identifier, identifier["key"], identifier[0]
        var pattern = @"^[a-zA-Z_$][a-zA-Z0-9_$]*(\.[a-zA-Z_$][a-zA-Z0-9_$]*|\[[^\]]+\])*$";
        return System.Text.RegularExpressions.Regex.IsMatch(value, pattern);
    }

    /// <summary>
    /// Validate an environment variable name
    /// </summary>
    public static bool IsValidEnvironmentVariableName(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(value, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }

    /// <summary>
    /// Validate a node name (can't be empty, can't contain certain characters)
    /// </summary>
    public static Func<string, bool> NodeName() => value =>
    {
        if (string.IsNullOrEmpty(value)) return false;
        // Node names shouldn't contain control characters
        return !System.Text.RegularExpressions.Regex.IsMatch(value, @"[\x00-\x1F\x7F]");
    };

    /// <summary>
    /// Validate a port number
    /// </summary>
    public static Func<string, bool> Port() => value =>
    {
        if (string.IsNullOrEmpty(value)) return true;
        return int.TryParse(value, out var port) && port >= 0 && port <= 65535;
    };

    /// <summary>
    /// Validate a URL
    /// </summary>
    public static Func<string, bool> Url(bool allowRelative = false) => value =>
    {
        if (string.IsNullOrEmpty(value)) return true;
        
        if (allowRelative && value.StartsWith("/"))
            return true;

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    };

    /// <summary>
    /// Validate an email address
    /// </summary>
    public static Func<string, bool> Email() => value =>
    {
        if (string.IsNullOrEmpty(value)) return true;
        return System.Text.RegularExpressions.Regex.IsMatch(value, 
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    };

    /// <summary>
    /// Combine multiple validators with AND logic
    /// </summary>
    public static Func<string, bool> All(params Func<string, bool>[] validators) => value =>
    {
        return validators.All(v => v(value));
    };

    /// <summary>
    /// Combine multiple validators with OR logic
    /// </summary>
    public static Func<string, bool> Any(params Func<string, bool>[] validators) => value =>
    {
        return validators.Any(v => v(value));
    };
}
