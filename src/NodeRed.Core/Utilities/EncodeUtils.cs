// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NodeRed.Core.Utilities;

/// <summary>
/// Utility functions for encoding objects for debugging and display.
/// Equivalent to @node-red/util/lib/util.js encodeObject() function.
/// </summary>
public static class EncodeUtils
{
    private const int DefaultMaxLength = 1000;

    /// <summary>
    /// Encoded object wrapper with type information.
    /// </summary>
    public class EncodedValue
    {
        [JsonPropertyName("__enc__")]
        public bool IsEncoded { get; set; } = true;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("data")]
        public object? Data { get; set; }

        [JsonPropertyName("length")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Length { get; set; }
    }

    /// <summary>
    /// Error data for encoded errors.
    /// </summary>
    public class ErrorData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("code")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Code { get; set; }

        [JsonPropertyName("cause")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Cause { get; set; }

        [JsonPropertyName("stack")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Stack { get; set; }
    }

    /// <summary>
    /// Encoded message result.
    /// </summary>
    public class EncodedMessage
    {
        /// <summary>
        /// The format/type of the message (e.g., "Object", "array[5]", "string[100]", "error")
        /// </summary>
        public string Format { get; set; } = "";

        /// <summary>
        /// The encoded message content (may be JSON string or simple string)
        /// </summary>
        public string Msg { get; set; } = "";
    }

    /// <summary>
    /// Encode an object to JSON without losing information about non-JSON types
    /// such as byte arrays (Buffer equivalent), functions, and special values.
    /// This function is closely tied to its reverse within the editor.
    /// </summary>
    /// <param name="msg">The object containing the message to encode (with a "msg" property)</param>
    /// <param name="maxLength">Maximum length for strings and arrays</param>
    /// <returns>The encoded object with format and msg properties</returns>
    public static EncodedMessage EncodeObject(object? msg, int? maxLength = null)
    {
        var debugLength = maxLength ?? DefaultMaxLength;
        var result = new EncodedMessage();

        try
        {
            if (msg == null)
            {
                result.Format = "null";
                result.Msg = "(undefined)";
                return result;
            }

            var msgType = msg.GetType();

            // Handle Exception/Error
            if (msg is Exception ex)
            {
                result.Format = "error";
                var errorValue = new EncodedValue
                {
                    Type = "error",
                    Data = new ErrorData
                    {
                        Name = ex.GetType().Name,
                        Message = ex.Message,
                        Stack = ex.StackTrace,
                        Cause = ex.InnerException?.Message
                    }
                };
                result.Msg = JsonSerializer.Serialize(errorValue);
                return result;
            }

            // Handle byte[] (Buffer equivalent)
            if (msg is byte[] bytes)
            {
                result.Format = $"buffer[{bytes.Length}]";
                var hexString = Convert.ToHexString(bytes).ToLowerInvariant();
                result.Msg = hexString.Length > debugLength 
                    ? hexString.Substring(0, debugLength) 
                    : hexString;
                return result;
            }

            // Handle arrays
            if (msg is Array array)
            {
                result.Format = $"array[{array.Length}]";
                if (array.Length > debugLength)
                {
                    var truncated = new object?[debugLength];
                    Array.Copy(array, truncated, debugLength);
                    var encodedArray = new EncodedValue
                    {
                        Type = "array",
                        Data = EncodeArrayElements(truncated, debugLength),
                        Length = array.Length
                    };
                    result.Msg = SafeJsonSerialize(encodedArray);
                }
                else
                {
                    result.Msg = SafeJsonSerialize(EncodeArrayElements(array, debugLength));
                }
                return result;
            }

            // Handle IList<object>
            if (msg is IList<object?> list)
            {
                result.Format = $"array[{list.Count}]";
                if (list.Count > debugLength)
                {
                    var truncated = list.Take(debugLength).ToList();
                    var encodedArray = new EncodedValue
                    {
                        Type = "array",
                        Data = EncodeListElements(truncated, debugLength),
                        Length = list.Count
                    };
                    result.Msg = SafeJsonSerialize(encodedArray);
                }
                else
                {
                    result.Msg = SafeJsonSerialize(EncodeListElements(list, debugLength));
                }
                return result;
            }

            // Handle string
            if (msg is string str)
            {
                result.Format = $"string[{str.Length}]";
                result.Msg = str.Length > debugLength 
                    ? str.Substring(0, debugLength) + "..." 
                    : str;
                return result;
            }

            // Handle boolean
            if (msg is bool boolVal)
            {
                result.Format = "boolean";
                result.Msg = boolVal.ToString().ToLowerInvariant();
                return result;
            }

            // Handle numbers
            if (IsNumericType(msgType))
            {
                result.Format = "number";
                if (msg is double d)
                {
                    if (double.IsNaN(d) || double.IsInfinity(d))
                    {
                        var encodedNum = new EncodedValue
                        {
                            Type = "number",
                            Data = d.ToString()
                        };
                        result.Msg = SafeJsonSerialize(encodedNum);
                        return result;
                    }
                }
                result.Msg = msg.ToString() ?? "0";
                return result;
            }

            // Handle Delegate/Func (function equivalent)
            if (msg is Delegate)
            {
                result.Format = "function";
                result.Msg = "[function]";
                return result;
            }

            // Handle Regex
            if (msg is System.Text.RegularExpressions.Regex regex)
            {
                result.Format = "regexp";
                result.Msg = regex.ToString();
                return result;
            }

            // Handle HashSet<T> (Set equivalent)
            if (msgType.IsGenericType && msgType.GetGenericTypeDefinition() == typeof(HashSet<>))
            {
                var setItems = ((System.Collections.IEnumerable)msg).Cast<object?>().ToList();
                result.Format = $"set[{setItems.Count}]";
                var encodedSet = new EncodedValue
                {
                    Type = "set",
                    Data = setItems.Take(debugLength).ToList(),
                    Length = setItems.Count
                };
                result.Msg = SafeJsonSerialize(encodedSet);
                return result;
            }

            // Handle Dictionary<K,V> (Map equivalent)
            if (msgType.IsGenericType && msgType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var dictItems = ((System.Collections.IDictionary)msg);
                result.Format = "map";
                var dataDict = new Dictionary<string, object?>();
                var count = 0;
                foreach (System.Collections.DictionaryEntry entry in dictItems)
                {
                    if (count >= debugLength) break;
                    dataDict[entry.Key?.ToString() ?? ""] = entry.Value;
                    count++;
                }
                var encodedMap = new EncodedValue
                {
                    Type = "map",
                    Data = dataDict,
                    Length = dictItems.Count
                };
                result.Msg = SafeJsonSerialize(encodedMap);
                return result;
            }

            // Handle other objects
            result.Format = msgType.Name;
            if (result.Format == "Object" || result.Format == "ExpandoObject" || 
                result.Format == "Dictionary`2" || result.Format == "JsonElement")
            {
                result.Format = "Object";
            }

            result.Msg = SafeJsonSerialize(msg, debugLength);
            return result;
        }
        catch (Exception e)
        {
            result.Format = "error";
            var errorValue = new EncodedValue
            {
                Type = "error",
                Data = new ErrorData
                {
                    Name = e.GetType().Name,
                    Message = "encodeObject Error: " + e.Message,
                    Stack = e.StackTrace
                }
            };
            result.Msg = JsonSerializer.Serialize(errorValue);
            return result;
        }
    }

    /// <summary>
    /// Safely serializes an object to JSON, handling special cases.
    /// </summary>
    private static string SafeJsonSerialize(object? obj, int maxStringLength = DefaultMaxLength)
    {
        if (obj == null) return "null";

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                MaxDepth = 32,
                Converters = { new SpecialValueConverter(maxStringLength) }
            };
            return JsonSerializer.Serialize(obj, options);
        }
        catch
        {
            return obj.ToString() ?? "";
        }
    }

    /// <summary>
    /// Encodes array elements, handling special types.
    /// </summary>
    private static object?[] EncodeArrayElements(Array array, int maxLength)
    {
        var result = new object?[array.Length];
        for (int i = 0; i < array.Length; i++)
        {
            result[i] = EncodeValue(array.GetValue(i), maxLength);
        }
        return result;
    }

    /// <summary>
    /// Encodes list elements, handling special types.
    /// </summary>
    private static List<object?> EncodeListElements(IList<object?> list, int maxLength)
    {
        var result = new List<object?>();
        foreach (var item in list)
        {
            result.Add(EncodeValue(item, maxLength));
        }
        return result;
    }

    /// <summary>
    /// Encodes a single value, handling special types.
    /// </summary>
    private static object? EncodeValue(object? value, int maxLength)
    {
        if (value == null) return new EncodedValue { Type = "undefined" };

        if (value is Exception ex)
        {
            return new EncodedValue
            {
                Type = "error",
                Data = new ErrorData
                {
                    Name = ex.GetType().Name,
                    Message = ex.Message,
                    Stack = ex.StackTrace
                }
            };
        }

        if (value is byte[] bytes)
        {
            return new EncodedValue
            {
                Type = "Buffer",
                Data = bytes.Take(maxLength).ToArray(),
                Length = bytes.Length
            };
        }

        if (value is Delegate)
        {
            return new EncodedValue { Type = "function" };
        }

        if (value is double d && (double.IsNaN(d) || double.IsInfinity(d)))
        {
            return new EncodedValue
            {
                Type = "number",
                Data = d.ToString()
            };
        }

        if (value is string str && str.Length > maxLength)
        {
            return str.Substring(0, maxLength) + "...";
        }

        return value;
    }

    /// <summary>
    /// Checks if a type is a numeric type.
    /// </summary>
    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) ||
               type == typeof(decimal);
    }

    /// <summary>
    /// Custom JSON converter for handling special values.
    /// </summary>
    private class SpecialValueConverter : JsonConverter<object>
    {
        private readonly int _maxStringLength;

        public SpecialValueConverter(int maxStringLength)
        {
            _maxStringLength = maxStringLength;
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return true;
        }

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<object>(ref reader);
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            var type = value.GetType();

            // Handle special double values
            if (value is double d)
            {
                if (double.IsNaN(d))
                {
                    writer.WriteStartObject();
                    writer.WriteBoolean("__enc__", true);
                    writer.WriteString("type", "number");
                    writer.WriteString("data", "NaN");
                    writer.WriteEndObject();
                    return;
                }
                if (double.IsPositiveInfinity(d))
                {
                    writer.WriteStartObject();
                    writer.WriteBoolean("__enc__", true);
                    writer.WriteString("type", "number");
                    writer.WriteString("data", "Infinity");
                    writer.WriteEndObject();
                    return;
                }
                if (double.IsNegativeInfinity(d))
                {
                    writer.WriteStartObject();
                    writer.WriteBoolean("__enc__", true);
                    writer.WriteString("type", "number");
                    writer.WriteString("data", "-Infinity");
                    writer.WriteEndObject();
                    return;
                }
            }

            // Handle functions
            if (value is Delegate)
            {
                writer.WriteStartObject();
                writer.WriteBoolean("__enc__", true);
                writer.WriteString("type", "function");
                writer.WriteEndObject();
                return;
            }

            // Handle byte arrays
            if (value is byte[] bytes)
            {
                writer.WriteStartObject();
                writer.WriteBoolean("__enc__", true);
                writer.WriteString("type", "Buffer");
                writer.WriteStartArray("data");
                foreach (var b in bytes.Take(_maxStringLength))
                {
                    writer.WriteNumberValue(b);
                }
                writer.WriteEndArray();
                writer.WriteNumber("length", bytes.Length);
                writer.WriteEndObject();
                return;
            }

            // Handle long strings
            if (value is string str && str.Length > _maxStringLength)
            {
                writer.WriteStringValue(str.Substring(0, _maxStringLength) + "...");
                return;
            }

            // Default serialization
            try
            {
                JsonSerializer.Serialize(writer, value, type, new JsonSerializerOptions { MaxDepth = 32 });
            }
            catch
            {
                writer.WriteStringValue(value.ToString());
            }
        }
    }
}
