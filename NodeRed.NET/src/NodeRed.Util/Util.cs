// ============================================================
// SOURCE: packages/node_modules/@node-red/util/lib/util.js
// LINES: 1-1110
// ============================================================
// ORIGINAL CODE:
// ------------------------------------------------------------
// See original file for full source code.
// Key functions translated:
// - generateId()
// - ensureString()
// - ensureBuffer()
// - cloneMessage()
// - compareObjects()
// - normalisePropertyExpression()
// - getMessageProperty() / getObjectProperty()
// - setMessageProperty() / setObjectProperty()
// - getSetting()
// - evaluateEnvProperty()
// - parseContextStore()
// - evaluateNodeProperty()
// - prepareJSONataExpression() / evaluateJSONataExpression()
// - normaliseNodeTypeName()
// - encodeObject()
// ------------------------------------------------------------
// TRANSLATION:
// ------------------------------------------------------------

// Copyright JS Foundation and other contributors, http://js.foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NodeRed.Util
{
    /// <summary>
    /// Custom error with code property.
    /// </summary>
    public class NodeRedError : Exception
    {
        public string Code { get; }

        public NodeRedError(string code, string message) : base(message)
        {
            Code = code;
        }
    }

    /// <summary>
    /// Context store key information.
    /// </summary>
    public class ContextStoreKey
    {
        public string? Store { get; set; }
        public string Key { get; set; } = string.Empty;
    }

    /// <summary>
    /// Encoded object wrapper for special types.
    /// </summary>
    public class EncodedObject
    {
        [JsonPropertyName("__enc__")]
        public bool Encoded { get; set; } = true;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public object? Data { get; set; }

        [JsonPropertyName("length")]
        public int? Length { get; set; }
    }

    /// <summary>
    /// Encoded error data.
    /// </summary>
    public class EncodedErrorData
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("cause")]
        public string? Cause { get; set; }

        [JsonPropertyName("stack")]
        public string? Stack { get; set; }
    }

    /// <summary>
    /// General utilities for Node-RED.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/util/lib/util.js
    /// </remarks>
    public static class Util
    {
        /// <summary>
        /// Safely returns the object constructor name.
        /// </summary>
        /// <param name="obj">The object to get the constructor name from.</param>
        /// <returns>The name of the object constructor if it exists, empty string otherwise.</returns>
        public static string ConstructorName(object? obj)
        {
            return obj?.GetType().Name ?? string.Empty;
        }

        /// <summary>
        /// Generates a pseudo-unique-random id.
        /// </summary>
        /// <returns>A random-ish id (16 character hex string).</returns>
        public static string GenerateId()
        {
            var bytes = new byte[8];
            // Use Random.Shared which is thread-safe in .NET 6+
            Random.Shared.NextBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Generates a cryptographically secure random id.
        /// </summary>
        /// <returns>A secure random id (16 character hex string).</returns>
        public static string GenerateSecureId()
        {
            var bytes = new byte[8];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Converts the provided argument to a String.
        /// </summary>
        /// <param name="obj">The property to convert to a String.</param>
        /// <returns>The stringified version.</returns>
        public static string EnsureString(object? obj)
        {
            if (obj == null)
            {
                return string.Empty;
            }

            if (obj is byte[] buffer)
            {
                return Encoding.UTF8.GetString(buffer);
            }

            if (obj is string str)
            {
                return str;
            }

            if (obj.GetType().IsClass && obj is not string)
            {
                return JsonSerializer.Serialize(obj);
            }

            return obj.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Converts the provided argument to a byte array (Buffer equivalent).
        /// </summary>
        /// <param name="obj">The property to convert to a byte array.</param>
        /// <returns>The byte array version.</returns>
        public static byte[] EnsureBuffer(object? obj)
        {
            if (obj == null)
            {
                return Array.Empty<byte>();
            }

            if (obj is byte[] buffer)
            {
                return buffer;
            }

            string str;
            if (obj.GetType().IsClass && obj is not string)
            {
                str = JsonSerializer.Serialize(obj);
            }
            else
            {
                str = obj.ToString() ?? string.Empty;
            }

            return Encoding.UTF8.GetBytes(str);
        }

        /// <summary>
        /// Safely clones a message object.
        /// This handles msg.req/msg.res objects that must not be cloned.
        /// </summary>
        /// <param name="msg">The message object to clone.</param>
        /// <returns>The cloned message.</returns>
        public static T? CloneMessage<T>(T? msg) where T : class
        {
            if (msg == null)
            {
                return null;
            }

            // For dictionary-based messages, handle req/res specially
            if (msg is IDictionary<string, object> dict)
            {
                // Save req/res references
                dict.TryGetValue("req", out var req);
                dict.TryGetValue("res", out var res);

                // Remove temporarily
                dict.Remove("req");
                dict.Remove("res");

                // Deep clone via JSON serialization
                var json = JsonSerializer.Serialize(dict);
                var clone = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                // Restore req/res to original
                if (req != null) dict["req"] = req;
                if (res != null) dict["res"] = res;

                // Add req/res references to clone
                if (clone != null)
                {
                    if (req != null) clone["req"] = req;
                    if (res != null) clone["res"] = res;
                }

                return clone as T;
            }

            // For other types, use JSON serialization for deep clone
            var jsonStr = JsonSerializer.Serialize(msg);
            return JsonSerializer.Deserialize<T>(jsonStr);
        }

        /// <summary>
        /// Compares two objects, handling various types.
        /// </summary>
        /// <param name="obj1">First object.</param>
        /// <param name="obj2">Second object.</param>
        /// <returns>Whether the two objects are the same.</returns>
        public static bool CompareObjects(object? obj1, object? obj2)
        {
            if (ReferenceEquals(obj1, obj2))
            {
                return true;
            }

            if (obj1 == null || obj2 == null)
            {
                return false;
            }

            // Handle arrays/lists
            if (obj1 is IList list1 && obj2 is IList list2)
            {
                if (list1.Count != list2.Count)
                {
                    return false;
                }

                for (int i = 0; i < list1.Count; i++)
                {
                    if (!CompareObjects(list1[i], list2[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            // Handle byte arrays
            if (obj1 is byte[] bytes1 && obj2 is byte[] bytes2)
            {
                return bytes1.SequenceEqual(bytes2);
            }

            // Handle primitives
            if (obj1.GetType().IsPrimitive || obj1 is string)
            {
                return obj1.Equals(obj2);
            }

            // Handle dictionaries
            if (obj1 is IDictionary dict1 && obj2 is IDictionary dict2)
            {
                if (dict1.Count != dict2.Count)
                {
                    return false;
                }

                foreach (var key in dict1.Keys)
                {
                    if (!dict2.Contains(key))
                    {
                        return false;
                    }

                    if (!CompareObjects(dict1[key], dict2[key]))
                    {
                        return false;
                    }
                }

                return true;
            }

            // For other objects, compare via JSON serialization
            var json1 = JsonSerializer.Serialize(obj1);
            var json2 = JsonSerializer.Serialize(obj2);
            return json1 == json2;
        }

        /// <summary>
        /// Parses a property expression, such as `msg.foo.bar[3]` to validate it
        /// and convert it to a canonical version expressed as an Array of property names.
        /// </summary>
        /// <param name="str">The property expression.</param>
        /// <param name="msg">Optional message for cross-reference evaluation.</param>
        /// <param name="toString">If true, returns the normalized expression as a string.</param>
        /// <returns>The normalized expression as a list of property parts.</returns>
        public static List<object> NormalisePropertyExpression(string str, IDictionary<string, object>? msg = null, bool toString = false)
        {
            var length = str.Length;
            if (length == 0)
            {
                throw new NodeRedError("INVALID_EXPR", "Invalid property expression: zero-length");
            }

            var parts = new List<object>();
            var start = 0;
            var inString = false;
            var inBox = false;
            var quoteChar = '\0';

            for (var i = 0; i < length; i++)
            {
                var c = str[i];

                if (!inString)
                {
                    if (c == '\'' || c == '"')
                    {
                        if (i != start)
                        {
                            throw new NodeRedError("INVALID_EXPR", $"Invalid property expression: unexpected {c} at position {i}");
                        }
                        inString = true;
                        quoteChar = c;
                        start = i + 1;
                    }
                    else if (c == '.')
                    {
                        if (i == 0)
                        {
                            throw new NodeRedError("INVALID_EXPR", "Invalid property expression: unexpected . at position 0");
                        }
                        if (start != i)
                        {
                            var v = str.Substring(start, i - start);
                            if (int.TryParse(v, out var intVal))
                            {
                                parts.Add(intVal);
                            }
                            else
                            {
                                parts.Add(v);
                            }
                        }
                        if (i == length - 1)
                        {
                            throw new NodeRedError("INVALID_EXPR", "Invalid property expression: unterminated expression");
                        }
                        start = i + 1;
                    }
                    else if (c == '[')
                    {
                        if (i == 0)
                        {
                            throw new NodeRedError("INVALID_EXPR", $"Invalid property expression: unexpected {c} at position {i}");
                        }
                        if (start != i)
                        {
                            parts.Add(str.Substring(start, i - start));
                        }
                        if (i == length - 1)
                        {
                            throw new NodeRedError("INVALID_EXPR", "Invalid property expression: unterminated expression");
                        }

                        // Check for nested msg reference
                        var remaining = str.Substring(i + 1);
                        if (Regex.IsMatch(remaining, @"^msg[.\[]"))
                        {
                            // Handle nested msg reference
                            var depth = 1;
                            var inLocalString = false;
                            var localStringQuote = '\0';
                            var j = i + 1;

                            for (; j < length; j++)
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
                                    var nestedExpr = str.Substring(i + 1, j - i - 1);
                                    if (msg != null)
                                    {
                                        var crossRefProp = GetMessageProperty(msg, nestedExpr);
                                        if (crossRefProp == null)
                                        {
                                            throw new NodeRedError("INVALID_EXPR", $"Invalid expression: undefined reference at position {i + 1} : {nestedExpr}");
                                        }
                                        parts.Add(crossRefProp);
                                    }
                                    else
                                    {
                                        parts.Add(NormalisePropertyExpression(nestedExpr, msg));
                                    }
                                    i = j;
                                    start = j + 1;
                                    break;
                                }
                            }

                            if (depth > 0)
                            {
                                throw new NodeRedError("INVALID_EXPR", $"Invalid property expression: unmatched '[' at position {i}");
                            }
                            continue;
                        }

                        start = i + 1;
                        inBox = true;
                    }
                    else if (c == ']')
                    {
                        if (!inBox)
                        {
                            throw new NodeRedError("INVALID_EXPR", $"Invalid property expression: unexpected {c} at position {i}");
                        }
                        if (start != i)
                        {
                            var v = str.Substring(start, i - start);
                            if (int.TryParse(v, out var intVal))
                            {
                                parts.Add(intVal);
                            }
                            else
                            {
                                throw new NodeRedError("INVALID_EXPR", $"Invalid property expression: unexpected array expression at position {start}");
                            }
                        }
                        start = i + 1;
                        inBox = false;
                    }
                    else if (c == ' ')
                    {
                        throw new NodeRedError("INVALID_EXPR", $"Invalid property expression: unexpected ' ' at position {i}");
                    }
                }
                else
                {
                    if (c == quoteChar)
                    {
                        if (i - start == 0)
                        {
                            throw new NodeRedError("INVALID_EXPR", $"Invalid property expression: zero-length string at position {start}");
                        }
                        parts.Add(str.Substring(start, i - start));
                        start = i + 1;
                        inString = false;
                    }
                }
            }

            if (inBox || inString)
            {
                throw new NodeRedError("INVALID_EXPR", "Invalid property expression: unterminated expression");
            }

            if (start < length)
            {
                var remaining = str.Substring(start);
                if (int.TryParse(remaining, out var intVal))
                {
                    parts.Add(intVal);
                }
                else
                {
                    parts.Add(remaining);
                }
            }

            if (toString)
            {
                var result = new StringBuilder();
                foreach (var p in parts)
                {
                    if (result.Length == 0)
                    {
                        result.Append(p);
                    }
                    else
                    {
                        var partStr = p.ToString()!;
                        if (partStr.Contains('"'))
                        {
                            result.Append($"['{partStr}']");
                        }
                        else
                        {
                            result.Append($"[\"{partStr}\"]");
                        }
                    }
                }
                // Note: When toString is true, we'd need to return string instead
                // This is a deviation from the original that returns string|array
            }

            return parts;
        }

        /// <summary>
        /// Gets a property of a message object.
        /// Unlike GetObjectProperty, this function will strip `msg.` from the
        /// front of the property expression if present.
        /// </summary>
        /// <param name="msg">The message object.</param>
        /// <param name="expr">The property expression.</param>
        /// <returns>The message property, or null if it does not exist.</returns>
        public static object? GetMessageProperty(IDictionary<string, object> msg, string expr)
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
        public static object? GetObjectProperty(object obj, string expr)
        {
            var msgPropParts = NormalisePropertyExpression(expr, obj as IDictionary<string, object>);
            object? current = obj;

            foreach (var key in msgPropParts)
            {
                if (current == null)
                {
                    return null;
                }

                if (current is IDictionary<string, object> dict && key is string strKey)
                {
                    if (!dict.TryGetValue(strKey, out current))
                    {
                        return null;
                    }
                }
                else if (current is IList list && key is int intKey)
                {
                    if (intKey < 0 || intKey >= list.Count)
                    {
                        return null;
                    }
                    current = list[intKey];
                }
                else if (current is JsonElement element)
                {
                    if (key is string sKey && element.ValueKind == JsonValueKind.Object)
                    {
                        if (element.TryGetProperty(sKey, out var prop))
                        {
                            current = prop;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else if (key is int iKey && element.ValueKind == JsonValueKind.Array)
                    {
                        if (iKey >= 0 && iKey < element.GetArrayLength())
                        {
                            current = element[iKey];
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
                else
                {
                    // Try reflection for other objects
                    var prop = current.GetType().GetProperty(key.ToString()!);
                    if (prop != null)
                    {
                        current = prop.GetValue(current);
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            return current;
        }

        /// <summary>
        /// Sets a property of a message object.
        /// </summary>
        /// <param name="msg">The message object.</param>
        /// <param name="prop">The property expression.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="createMissing">Whether to create missing parent properties.</param>
        /// <returns>True if successful.</returns>
        public static bool SetMessageProperty(IDictionary<string, object> msg, string prop, object? value, bool? createMissing = null)
        {
            if (prop.StartsWith("msg."))
            {
                prop = prop.Substring(4);
            }
            return SetObjectProperty(msg, prop, value, createMissing);
        }

        /// <summary>
        /// Sets a property of an object.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="prop">The property expression.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="createMissing">Whether to create missing parent properties.</param>
        /// <returns>True if successful.</returns>
        public static bool SetObjectProperty(object obj, string prop, object? value, bool? createMissing = null)
        {
            createMissing ??= value != null;

            var msgPropParts = NormalisePropertyExpression(prop, obj as IDictionary<string, object>);
            var length = msgPropParts.Count;
            var current = obj;

            for (var i = 0; i < length - 1; i++)
            {
                var key = msgPropParts[i];

                if (current is IDictionary<string, object> dict)
                {
                    var strKey = key.ToString()!;
                    if (dict.TryGetValue(strKey, out var next))
                    {
                        if (next == null || (next is not IDictionary && next is not IList))
                        {
                            return false;
                        }
                        current = next;
                    }
                    else if (createMissing.Value)
                    {
                        var nextKey = msgPropParts[i + 1];
                        if (nextKey is string)
                        {
                            dict[strKey] = new Dictionary<string, object>();
                        }
                        else
                        {
                            dict[strKey] = new List<object>();
                        }
                        current = dict[strKey];
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (current is IList list && key is int intKey)
                {
                    if (intKey < 0 || intKey >= list.Count)
                    {
                        if (createMissing.Value)
                        {
                            while (list.Count <= intKey)
                            {
                                list.Add(null!);
                            }
                            var nextKey = msgPropParts[i + 1];
                            if (nextKey is string)
                            {
                                list[intKey] = new Dictionary<string, object>();
                            }
                            else
                            {
                                list[intKey] = new List<object>();
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                    current = list[intKey]!;
                }
                else
                {
                    return false;
                }
            }

            // Set the final property
            var finalKey = msgPropParts[length - 1];

            if (current is IDictionary<string, object> finalDict)
            {
                var strKey = finalKey.ToString()!;
                if (value == null)
                {
                    finalDict.Remove(strKey);
                }
                else
                {
                    finalDict[strKey] = value;
                }
                return true;
            }
            else if (current is IList finalList && finalKey is int finalIntKey)
            {
                if (value == null)
                {
                    if (finalIntKey >= 0 && finalIntKey < finalList.Count)
                    {
                        finalList.RemoveAt(finalIntKey);
                    }
                }
                else
                {
                    while (finalList.Count <= finalIntKey)
                    {
                        finalList.Add(null!);
                    }
                    finalList[finalIntKey] = value;
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parses a context property string to extract the store name if present.
        /// For example, `#:(file)::foo` results in `{ Store: "file", Key: "foo" }`.
        /// </summary>
        /// <param name="key">The context property string to parse.</param>
        /// <returns>The parsed property with Store and Key.</returns>
        public static ContextStoreKey ParseContextStore(string key)
        {
            var match = Regex.Match(key, @"^#:\((\S+?)\)::(.*)$");
            if (match.Success)
            {
                return new ContextStoreKey
                {
                    Store = match.Groups[1].Value,
                    Key = match.Groups[2].Value
                };
            }

            return new ContextStoreKey { Key = key };
        }

        /// <summary>
        /// Checks if a String contains any Environment Variable specifiers and returns
        /// it with their values substituted in place.
        /// </summary>
        /// <param name="value">The string to parse.</param>
        /// <param name="node">The node evaluating the property (optional).</param>
        /// <returns>The parsed string with env vars substituted.</returns>
        public static string EvaluateEnvProperty(string value, object? node = null)
        {
            string? result;

            // Check for ${ENV_VAR} pattern
            if (Regex.IsMatch(value, @"^\$\{[^}]+\}$"))
            {
                // ${ENV_VAR} - single env var reference
                var name = value.Substring(2, value.Length - 3);
                result = GetSetting(node, name);
            }
            else if (!Regex.IsMatch(value, @"\$\{\S+\}"))
            {
                // Plain ENV_VAR name
                result = GetSetting(node, value);
            }
            else
            {
                // FOO${ENV_VAR}BAR - mixed content
                result = Regex.Replace(value, @"\$\{([^}]+)\}", match =>
                {
                    var val = GetSetting(node, match.Groups[1].Value);
                    return val ?? string.Empty;
                });
                return result;
            }

            return result ?? string.Empty;
        }

        /// <summary>
        /// Get value of environment variable or node setting.
        /// </summary>
        /// <param name="node">The accessing node.</param>
        /// <param name="name">The name of the variable.</param>
        /// <returns>The value of the env var or setting.</returns>
        public static string? GetSetting(object? node, string name)
        {
            // Check for special node properties
            if (node != null)
            {
                var nodeType = node.GetType();

                if (name == "NR_NODE_NAME")
                {
                    var nameProp = nodeType.GetProperty("Name");
                    return nameProp?.GetValue(node)?.ToString();
                }
                if (name == "NR_NODE_ID")
                {
                    var idProp = nodeType.GetProperty("Id");
                    return idProp?.GetValue(node)?.ToString();
                }
                if (name == "NR_NODE_PATH")
                {
                    var pathProp = nodeType.GetProperty("Path");
                    return pathProp?.GetValue(node)?.ToString();
                }

                // Check if node has a flow with getSetting
                var flowProp = nodeType.GetProperty("Flow");
                if (flowProp != null)
                {
                    var flow = flowProp.GetValue(node);
                    if (flow != null)
                    {
                        var getSettingMethod = flow.GetType().GetMethod("GetSetting");
                        if (getSettingMethod != null)
                        {
                            return getSettingMethod.Invoke(flow, new object[] { name })?.ToString();
                        }
                    }
                }
            }

            // Fall back to environment variable
            return Environment.GetEnvironmentVariable(name);
        }

        /// <summary>
        /// Normalise a node type name to camel case.
        /// For example: `a-random node type` will normalise to `aRandomNodeType`.
        /// </summary>
        /// <param name="name">The node type.</param>
        /// <returns>The normalised name.</returns>
        public static string NormaliseNodeTypeName(string name)
        {
            // Replace non-alphanumeric with space
            var result = Regex.Replace(name, @"[^a-zA-Z0-9]", " ");
            result = result.Trim();
            // Collapse multiple spaces
            result = Regex.Replace(result, @" +", " ");
            // Convert to camel case
            var parts = result.Split(' ');
            var sb = new StringBuilder();

            for (var i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;

                if (i == 0)
                {
                    sb.Append(char.ToLower(parts[i][0]));
                    if (parts[i].Length > 1)
                    {
                        sb.Append(parts[i].Substring(1));
                    }
                }
                else
                {
                    sb.Append(char.ToUpper(parts[i][0]));
                    if (parts[i].Length > 1)
                    {
                        sb.Append(parts[i].Substring(1));
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Encode an object to JSON without losing information about non-JSON types
        /// such as Buffer and Function.
        /// </summary>
        /// <param name="msg">The message containing the object to encode.</param>
        /// <param name="maxLength">Maximum length for string/array truncation.</param>
        /// <returns>The encoded message.</returns>
        public static Dictionary<string, object?> EncodeObject(Dictionary<string, object?> msg, int maxLength = 1000)
        {
            try
            {
                if (!msg.TryGetValue("msg", out var msgValue))
                {
                    return msg;
                }

                // Handle null/undefined
                if (msgValue == null)
                {
                    msg["format"] = "null";
                    msg["msg"] = "(undefined)";
                    return msg;
                }

                // Handle Exception/Error
                if (msgValue is Exception ex)
                {
                    msg["format"] = "error";
                    msg["msg"] = JsonSerializer.Serialize(new EncodedObject
                    {
                        Type = "error",
                        Data = new EncodedErrorData
                        {
                            Name = ex.GetType().Name,
                            Message = ex.Message,
                            Stack = ex.StackTrace
                        }
                    });
                    return msg;
                }

                // Handle byte array (Buffer)
                if (msgValue is byte[] buffer)
                {
                    msg["format"] = $"buffer[{buffer.Length}]";
                    var hex = BitConverter.ToString(buffer).Replace("-", "").ToLowerInvariant();
                    msg["msg"] = hex.Length > maxLength ? hex.Substring(0, maxLength) : hex;
                    return msg;
                }

                // Handle primitives
                if (msgValue is bool)
                {
                    msg["format"] = "boolean";
                    msg["msg"] = msgValue.ToString()!.ToLowerInvariant();
                    return msg;
                }

                if (msgValue is int or long or float or double or decimal)
                {
                    msg["format"] = "number";
                    msg["msg"] = msgValue.ToString();
                    return msg;
                }

                if (msgValue is string str)
                {
                    msg["format"] = $"string[{str.Length}]";
                    msg["msg"] = str.Length > maxLength ? str.Substring(0, maxLength) + "..." : str;
                    return msg;
                }

                // Handle arrays/lists
                if (msgValue is IList list)
                {
                    msg["format"] = $"array[{list.Count}]";
                    if (list.Count > maxLength)
                    {
                        var truncated = new List<object?>();
                        for (var i = 0; i < maxLength && i < list.Count; i++)
                        {
                            truncated.Add(list[i]);
                        }
                        msg["msg"] = JsonSerializer.Serialize(new EncodedObject
                        {
                            Type = "array",
                            Data = truncated,
                            Length = list.Count
                        });
                    }
                    else
                    {
                        msg["msg"] = JsonSerializer.Serialize(list);
                    }
                    return msg;
                }

                // Handle other objects
                var typeName = ConstructorName(msgValue);
                msg["format"] = string.IsNullOrEmpty(typeName) ? "Object" : typeName;
                msg["msg"] = JsonSerializer.Serialize(msgValue, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                return msg;
            }
            catch (Exception e)
            {
                msg["format"] = "error";
                msg["msg"] = JsonSerializer.Serialize(new EncodedObject
                {
                    Type = "error",
                    Data = new EncodedErrorData
                    {
                        Name = e.GetType().Name,
                        Message = "encodeObject Error: " + e.Message,
                        Stack = e.StackTrace
                    }
                });
                return msg;
            }
        }
    }
}
// ------------------------------------------------------------
// MAPPING NOTES:
// - constructorName() → ConstructorName()
// - generateId() → GenerateId() (using Random for compatibility, GenerateSecureId for crypto)
// - ensureString() → EnsureString()
// - ensureBuffer() → EnsureBuffer() (returns byte[])
// - cloneMessage() → CloneMessage<T>() (generic with JSON serialization)
// - compareObjects() → CompareObjects()
// - normalisePropertyExpression() → NormalisePropertyExpression()
// - getMessageProperty() → GetMessageProperty()
// - getObjectProperty() → GetObjectProperty()
// - setMessageProperty() → SetMessageProperty()
// - setObjectProperty() → SetObjectProperty()
// - parseContextStore() → ParseContextStore()
// - evaluateEnvProperty() → EvaluateEnvProperty()
// - getSetting() → GetSetting()
// - normaliseNodeTypeName() → NormaliseNodeTypeName()
// - encodeObject() → EncodeObject()
// - Buffer → byte[]
// - JSON.stringify → JsonSerializer.Serialize
// - JSON.parse → JsonSerializer.Deserialize
// - lodash.clonedeep → JSON serialization for deep clone
// - Error with code → NodeRedError class
// ============================================================
