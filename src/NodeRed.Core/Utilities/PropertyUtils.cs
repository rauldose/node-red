// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using System.Text.RegularExpressions;

namespace NodeRed.Core.Utilities;

/// <summary>
/// Utility functions for property expression parsing and evaluation.
/// Equivalent to @node-red/util/lib/util.js property-related functions.
/// </summary>
public static class PropertyUtils
{
    /// <summary>
    /// Parses a property expression, such as `msg.foo.bar[3]` to validate it
    /// and convert it to a canonical version expressed as an array of property names.
    /// For example, `a["b"].c` returns `['a','b','c']`
    /// </summary>
    /// <param name="str">The property expression.</param>
    /// <param name="msg">Optional message object for cross-reference evaluation.</param>
    /// <returns>The normalised expression as a list of parts.</returns>
    public static List<object> NormalisePropertyExpression(string str, Dictionary<string, object?>? msg = null)
    {
        if (string.IsNullOrEmpty(str))
        {
            throw MessageUtils.CreateError("INVALID_EXPR", "Invalid property expression: zero-length");
        }

        var parts = new List<object>();
        var length = str.Length;
        var start = 0;
        var inString = false;
        var inBox = false;
        char quoteChar = '\0';

        for (var i = 0; i < length; i++)
        {
            var c = str[i];

            if (!inString)
            {
                if (c == '\'' || c == '"')
                {
                    if (i != start)
                    {
                        throw MessageUtils.CreateError("INVALID_EXPR", $"Invalid property expression: unexpected {c} at position {i}");
                    }
                    inString = true;
                    quoteChar = c;
                    start = i + 1;
                }
                else if (c == '.')
                {
                    if (i == 0)
                    {
                        throw MessageUtils.CreateError("INVALID_EXPR", "Invalid property expression: unexpected . at position 0");
                    }
                    if (start != i)
                    {
                        var v = str.Substring(start, i - start);
                        if (int.TryParse(v, out var num))
                        {
                            parts.Add(num);
                        }
                        else
                        {
                            parts.Add(v);
                        }
                    }
                    if (i == length - 1)
                    {
                        throw MessageUtils.CreateError("INVALID_EXPR", "Invalid property expression: unterminated expression");
                    }
                    // Next char is first char of an identifier
                    if (!Regex.IsMatch(str[i + 1].ToString(), @"[a-zA-Z0-9$_]"))
                    {
                        throw MessageUtils.CreateError("INVALID_EXPR", $"Invalid property expression: unexpected {str[i + 1]} at position {i + 1}");
                    }
                    start = i + 1;
                }
                else if (c == '[')
                {
                    if (i == 0)
                    {
                        throw MessageUtils.CreateError("INVALID_EXPR", $"Invalid property expression: unexpected {c} at position {i}");
                    }
                    if (start != i)
                    {
                        parts.Add(str.Substring(start, i - start));
                    }
                    if (i == length - 1)
                    {
                        throw MessageUtils.CreateError("INVALID_EXPR", "Invalid property expression: unterminated expression");
                    }

                    // Check for nested msg reference
                    var remaining = str.Substring(i + 1);
                    if (Regex.IsMatch(remaining, @"^msg[.\[]"))
                    {
                        var depth = 1;
                        var inLocalString = false;
                        var localStringQuote = '\0';

                        for (var j = i + 1; j < length; j++)
                        {
                            if (str[j] == '"' || str[j] == '\'')
                            {
                                if (inLocalString)
                                {
                                    if (str[j] == localStringQuote)
                                    {
                                        inLocalString = false;
                                    }
                                }
                                else
                                {
                                    inLocalString = true;
                                    localStringQuote = str[j];
                                }
                            }

                            if (str[j] == '[') depth++;
                            else if (str[j] == ']') depth--;

                            if (depth == 0)
                            {
                                try
                                {
                                    var nestedExpr = str.Substring(i + 1, j - i - 1);
                                    if (msg != null)
                                    {
                                        var crossRefProp = GetMessageProperty(msg, nestedExpr);
                                        if (crossRefProp == null)
                                        {
                                            throw MessageUtils.CreateError("INVALID_EXPR", $"Invalid expression: undefined reference at position {i + 1} : {nestedExpr}");
                                        }
                                        parts.Add(crossRefProp);
                                    }
                                    else
                                    {
                                        parts.Add(NormalisePropertyExpression(nestedExpr, msg));
                                    }
                                    inBox = false;
                                    i = j;
                                    start = j + 1;
                                    break;
                                }
                                catch
                                {
                                    throw MessageUtils.CreateError("INVALID_EXPR", $"Invalid expression started at position {i + 1}");
                                }
                            }
                        }

                        if (depth > 0)
                        {
                            throw MessageUtils.CreateError("INVALID_EXPR", $"Invalid property expression: unmatched '[' at position {i}");
                        }
                        continue;
                    }
                    else if (!Regex.IsMatch(str[i + 1].ToString(), @"[""'\d]"))
                    {
                        throw MessageUtils.CreateError("INVALID_EXPR", $"Invalid property expression: unexpected {str[i + 1]} at position {i + 1}");
                    }
                    start = i + 1;
                    inBox = true;
                }
                else if (c == ']')
                {
                    if (!inBox)
                    {
                        throw MessageUtils.CreateError("INVALID_EXPR", $"Invalid property expression: unexpected {c} at position {i}");
                    }
                    if (start != i)
                    {
                        var v = str.Substring(start, i - start);
                        if (int.TryParse(v, out var num))
                        {
                            parts.Add(num);
                        }
                        else
                        {
                            throw MessageUtils.CreateError("INVALID_EXPR", $"Invalid property expression: unexpected array expression at position {start}");
                        }
                    }
                    start = i + 1;
                    inBox = false;
                }
                else if (c == ' ')
                {
                    throw MessageUtils.CreateError("INVALID_EXPR", $"Invalid property expression: unexpected ' ' at position {i}");
                }
            }
            else
            {
                if (c == quoteChar)
                {
                    if (i - start == 0)
                    {
                        throw MessageUtils.CreateError("INVALID_EXPR", $"Invalid property expression: zero-length string at position {start}");
                    }
                    parts.Add(str.Substring(start, i - start));

                    if (inBox && i + 1 < length && str[i + 1] != ']')
                    {
                        throw MessageUtils.CreateError("INVALID_EXPR", $"Invalid property expression: unexpected array expression at position {start}");
                    }
                    else if (!inBox && i + 1 != length && !Regex.IsMatch(str[i + 1].ToString(), @"[.\[]"))
                    {
                        throw MessageUtils.CreateError("INVALID_EXPR", $"Invalid property expression: unexpected {str[i + 1]} expression at position {i + 1}");
                    }
                    start = i + 1;
                    inString = false;
                }
            }
        }

        if (inBox || inString)
        {
            throw MessageUtils.CreateError("INVALID_EXPR", "Invalid property expression: unterminated expression");
        }

        if (start < length)
        {
            parts.Add(str.Substring(start));
        }

        return parts;
    }

    /// <summary>
    /// Gets a property of a message object.
    /// Strips `msg.` from the front of the property expression if present.
    /// </summary>
    /// <param name="msg">The message object.</param>
    /// <param name="expr">The property expression.</param>
    /// <returns>The message property, or null if it does not exist.</returns>
    public static object? GetMessageProperty(Dictionary<string, object?> msg, string expr)
    {
        if (expr.StartsWith("msg."))
        {
            expr = expr.Substring(4);
        }
        return GetObjectProperty(msg, expr);
    }

    /// <summary>
    /// Gets a property of an object.
    /// </summary>
    /// <param name="obj">The object.</param>
    /// <param name="expr">The property expression.</param>
    /// <returns>The object property, or null if it does not exist.</returns>
    public static object? GetObjectProperty(object? obj, string expr)
    {
        if (obj == null) return null;

        var parts = NormalisePropertyExpression(expr);
        object? result = obj;

        foreach (var key in parts)
        {
            if (result == null) return null;

            if (key is int index)
            {
                if (result is IList<object?> list)
                {
                    result = index >= 0 && index < list.Count ? list[index] : null;
                }
                else if (result is Array arr)
                {
                    result = index >= 0 && index < arr.Length ? arr.GetValue(index) : null;
                }
                else if (result is JsonElement jsonArr && jsonArr.ValueKind == JsonValueKind.Array)
                {
                    result = index >= 0 && index < jsonArr.GetArrayLength() ? jsonArr[index] : null;
                }
                else
                {
                    return null;
                }
            }
            else if (key is string strKey)
            {
                if (result is IDictionary<string, object?> dict)
                {
                    result = dict.TryGetValue(strKey, out var value) ? value : null;
                }
                else if (result is JsonElement jsonObj && jsonObj.ValueKind == JsonValueKind.Object)
                {
                    result = jsonObj.TryGetProperty(strKey, out var prop) ? prop : null;
                }
                else
                {
                    // Try reflection for other objects
                    var type = result.GetType();
                    var property = type.GetProperty(strKey);
                    if (property != null)
                    {
                        result = property.GetValue(result);
                    }
                    else
                    {
                        var field = type.GetField(strKey);
                        result = field?.GetValue(result);
                    }
                }
            }
            else
            {
                return null;
            }
        }

        return result;
    }

    /// <summary>
    /// Sets a property of a message object.
    /// Strips `msg.` from the front of the property expression if present.
    /// </summary>
    /// <param name="msg">The message object.</param>
    /// <param name="prop">The property expression.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="createMissing">Whether to create missing parent properties.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool SetMessageProperty(Dictionary<string, object?> msg, string prop, object? value, bool? createMissing = null)
    {
        if (prop.StartsWith("msg."))
        {
            prop = prop.Substring(4);
        }
        return SetObjectProperty(msg, prop, value, createMissing ?? (value != null));
    }

    /// <summary>
    /// Sets a property of an object.
    /// </summary>
    /// <param name="obj">The object.</param>
    /// <param name="prop">The property expression.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="createMissing">Whether to create missing parent properties.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool SetObjectProperty(object obj, string prop, object? value, bool createMissing = true)
    {
        var parts = NormalisePropertyExpression(prop);
        var length = parts.Count;
        object? current = obj;

        for (var i = 0; i < length - 1; i++)
        {
            var key = parts[i];

            if (key is string strKey)
            {
                if (current is IDictionary<string, object?> dict)
                {
                    if (dict.TryGetValue(strKey, out var existing))
                    {
                        if (existing == null || (existing is not IDictionary<string, object?> && existing is not IList<object?>))
                        {
                            return false;
                        }
                        current = existing;
                    }
                    else if (createMissing)
                    {
                        var nextKey = parts[i + 1];
                        dict[strKey] = nextKey is int ? new List<object?>() : new Dictionary<string, object?>();
                        current = dict[strKey];
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            else if (key is int index)
            {
                if (current is IList<object?> list)
                {
                    while (list.Count <= index)
                    {
                        if (!createMissing) return false;
                        list.Add(null);
                    }

                    if (list[index] == null && createMissing)
                    {
                        var nextKey = parts[i + 1];
                        list[index] = nextKey is int ? new List<object?>() : new Dictionary<string, object?>();
                    }
                    current = list[index];
                }
                else
                {
                    return false;
                }
            }
        }

        // Set the final value
        var finalKey = parts[length - 1];
        if (finalKey is string finalStrKey)
        {
            if (current is IDictionary<string, object?> dict)
            {
                if (value == null)
                {
                    dict.Remove(finalStrKey);
                }
                else
                {
                    dict[finalStrKey] = value;
                }
                return true;
            }
        }
        else if (finalKey is int finalIndex)
        {
            if (current is IList<object?> list)
            {
                if (value == null)
                {
                    if (finalIndex >= 0 && finalIndex < list.Count)
                    {
                        list.RemoveAt(finalIndex);
                    }
                }
                else
                {
                    while (list.Count <= finalIndex)
                    {
                        list.Add(null);
                    }
                    list[finalIndex] = value;
                }
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Parses a context property string to extract the store name if present.
    /// For example, `#:(file)::foo` results in `{ store: "file", key: "foo" }`.
    /// </summary>
    /// <param name="key">The context property string to parse.</param>
    /// <returns>A tuple with store name and key.</returns>
    public static (string? Store, string Key) ParseContextStore(string key)
    {
        var match = Regex.Match(key, @"^#:\((\S+?)\)::(.*)$");
        if (match.Success)
        {
            return (match.Groups[1].Value, match.Groups[2].Value);
        }
        return (null, key);
    }

    /// <summary>
    /// Normalise a node type name to camel case.
    /// For example: `a-random node type` will normalise to `aRandomNodeType`
    /// </summary>
    /// <param name="name">The node type.</param>
    /// <returns>The normalised name.</returns>
    public static string NormaliseNodeTypeName(string name)
    {
        var result = Regex.Replace(name, @"[^a-zA-Z0-9]", " ");
        result = result.Trim();
        result = Regex.Replace(result, @" +", " ");
        result = Regex.Replace(result, @" .", m => m.Value[1].ToString().ToUpperInvariant());

        if (!string.IsNullOrEmpty(result))
        {
            result = char.ToLowerInvariant(result[0]) + result.Substring(1);
        }

        return result;
    }

    /// <summary>
    /// Checks if a String contains any Environment Variable specifiers and returns
    /// it with their values substituted in place.
    /// For example, if the env var `WHO` is set to `Joe`, the string `Hello ${WHO}!`
    /// will return `Hello Joe!`.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="getEnvFunc">A function to get environment variable values.</param>
    /// <returns>The parsed string.</returns>
    public static string EvaluateEnvProperty(string value, Func<string, string?> getEnvFunc)
    {
        // Check for ${ENV_VAR} pattern
        if (Regex.IsMatch(value, @"^\${[^}]+}$"))
        {
            var name = value.Substring(2, value.Length - 3);
            return getEnvFunc(name) ?? string.Empty;
        }

        // Check for mixed pattern like FOO${ENV_VAR}BAR
        if (Regex.IsMatch(value, @"\${\S+}"))
        {
            return Regex.Replace(value, @"\${([^}]+)}", m =>
            {
                var name = m.Groups[1].Value;
                return getEnvFunc(name) ?? string.Empty;
            });
        }

        // Plain env var name
        return getEnvFunc(value) ?? string.Empty;
    }

    /// <summary>
    /// Evaluates a property value according to its type.
    /// </summary>
    /// <param name="value">The raw value.</param>
    /// <param name="type">The type of the value.</param>
    /// <param name="msg">The message object to evaluate against.</param>
    /// <param name="getEnvFunc">A function to get environment variable values.</param>
    /// <param name="getContextFunc">A function to get context values.</param>
    /// <returns>The evaluated property.</returns>
    public static object? EvaluateNodeProperty(
        string value,
        string type,
        Dictionary<string, object?>? msg = null,
        Func<string, string?>? getEnvFunc = null,
        Func<string, string, object?>? getContextFunc = null)
    {
        switch (type)
        {
            case "str":
                return value;

            case "num":
                if (double.TryParse(value, out var num))
                    return num;
                return 0;

            case "json":
                try
                {
                    return JsonSerializer.Deserialize<object>(value);
                }
                catch
                {
                    return null;
                }

            case "re":
                try
                {
                    return new Regex(value);
                }
                catch
                {
                    return null;
                }

            case "date":
                if (string.IsNullOrEmpty(value))
                    return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (value == "object")
                    return DateTimeOffset.UtcNow;
                if (value == "iso")
                    return DateTimeOffset.UtcNow.ToString("o");
                // Custom format support could be added here
                return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            case "bin":
                try
                {
                    var data = JsonSerializer.Deserialize<object>(value);
                    if (data is JsonElement element)
                    {
                        if (element.ValueKind == JsonValueKind.Array)
                        {
                            var bytes = new List<byte>();
                            foreach (var item in element.EnumerateArray())
                            {
                                if (item.TryGetByte(out var b))
                                    bytes.Add(b);
                            }
                            return bytes.ToArray();
                        }
                        else if (element.ValueKind == JsonValueKind.String)
                        {
                            return System.Text.Encoding.UTF8.GetBytes(element.GetString() ?? "");
                        }
                    }
                    return Array.Empty<byte>();
                }
                catch
                {
                    return Array.Empty<byte>();
                }

            case "msg":
                if (msg != null)
                {
                    return GetMessageProperty(msg, value);
                }
                return null;

            case "flow":
            case "global":
                if (getContextFunc != null)
                {
                    var (store, key) = ParseContextStore(value);
                    return getContextFunc(type, key);
                }
                return null;

            case "bool":
                return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

            case "env":
                if (getEnvFunc != null)
                {
                    return EvaluateEnvProperty(value, getEnvFunc);
                }
                return Environment.GetEnvironmentVariable(value);

            default:
                return value;
        }
    }
}
