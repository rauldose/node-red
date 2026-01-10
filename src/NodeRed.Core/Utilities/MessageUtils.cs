// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NodeRed.Core.Entities;

namespace NodeRed.Core.Utilities;

/// <summary>
/// Utility functions for message handling.
/// Equivalent to @node-red/util/lib/util.js message-related functions.
/// </summary>
public static class MessageUtils
{
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

    /// <summary>
    /// Generates a pseudo-unique-random id.
    /// Equivalent to JavaScript generateId() function.
    /// </summary>
    /// <returns>A random-ish 16 character hex string ID.</returns>
    public static string GenerateId()
    {
        var bytes = new byte[8];
        Rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Converts the provided argument to a String, using type-dependent methods.
    /// Equivalent to JavaScript ensureString() function.
    /// </summary>
    /// <param name="obj">The object to convert to a string.</param>
    /// <returns>The stringified version of the object.</returns>
    public static string EnsureString(object? obj)
    {
        if (obj == null)
            return string.Empty;

        if (obj is byte[] buffer)
            return Encoding.UTF8.GetString(buffer);

        if (obj is string str)
            return str;

        if (obj.GetType().IsPrimitive)
            return obj.ToString() ?? string.Empty;

        // For complex objects, serialize to JSON
        try
        {
            return JsonSerializer.Serialize(obj);
        }
        catch
        {
            return obj.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Converts the provided argument to a byte array (Buffer equivalent).
    /// Equivalent to JavaScript ensureBuffer() function.
    /// </summary>
    /// <param name="obj">The object to convert to a buffer.</param>
    /// <returns>The byte array version of the object.</returns>
    public static byte[] EnsureBuffer(object? obj)
    {
        if (obj == null)
            return Array.Empty<byte>();

        if (obj is byte[] buffer)
            return buffer;

        if (obj is string str)
            return Encoding.UTF8.GetBytes(str);

        // For complex objects, serialize to JSON first
        var jsonStr = EnsureString(obj);
        return Encoding.UTF8.GetBytes(jsonStr);
    }

    /// <summary>
    /// Safely clones a message object.
    /// Equivalent to JavaScript cloneMessage() function.
    /// </summary>
    /// <param name="msg">The message object to clone.</param>
    /// <returns>The cloned message.</returns>
    public static NodeMessage CloneMessage(NodeMessage? msg)
    {
        if (msg == null)
            return new NodeMessage();

        var cloned = new NodeMessage
        {
            Id = GenerateId(),
            Topic = msg.Topic,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Deep clone the payload
        cloned.Payload = DeepClone(msg.Payload);

        // Deep clone the properties
        foreach (var kvp in msg.Properties)
        {
            cloned.Properties[kvp.Key] = DeepClone(kvp.Value);
        }

        return cloned;
    }

    /// <summary>
    /// Compares two objects for deep equality.
    /// Equivalent to JavaScript compareObjects() function.
    /// </summary>
    /// <param name="obj1">First object to compare.</param>
    /// <param name="obj2">Second object to compare.</param>
    /// <returns>True if the objects are deeply equal, false otherwise.</returns>
    public static bool CompareObjects(object? obj1, object? obj2)
    {
        // Reference equality check
        if (ReferenceEquals(obj1, obj2))
            return true;

        // Null checks
        if (obj1 == null || obj2 == null)
            return false;

        // Type check
        var type1 = obj1.GetType();
        var type2 = obj2.GetType();

        // Handle byte arrays (Buffer equivalent)
        if (obj1 is byte[] bytes1 && obj2 is byte[] bytes2)
            return bytes1.SequenceEqual(bytes2);

        // Handle arrays
        if (obj1 is Array arr1 && obj2 is Array arr2)
        {
            if (arr1.Length != arr2.Length)
                return false;

            for (int i = 0; i < arr1.Length; i++)
            {
                if (!CompareObjects(arr1.GetValue(i), arr2.GetValue(i)))
                    return false;
            }
            return true;
        }

        // Handle lists
        if (obj1 is IList<object?> list1 && obj2 is IList<object?> list2)
        {
            if (list1.Count != list2.Count)
                return false;

            for (int i = 0; i < list1.Count; i++)
            {
                if (!CompareObjects(list1[i], list2[i]))
                    return false;
            }
            return true;
        }

        // Handle dictionaries
        if (obj1 is IDictionary<string, object?> dict1 && obj2 is IDictionary<string, object?> dict2)
        {
            if (dict1.Count != dict2.Count)
                return false;

            foreach (var key in dict1.Keys)
            {
                if (!dict2.TryGetValue(key, out var value2))
                    return false;
                if (!CompareObjects(dict1[key], value2))
                    return false;
            }
            return true;
        }

        // Primitive and value type comparison
        if (type1.IsPrimitive || type1 == typeof(string) || type1 == typeof(decimal) ||
            type1 == typeof(DateTime) || type1 == typeof(DateTimeOffset))
        {
            return obj1.Equals(obj2);
        }

        // For other types, try JSON serialization comparison
        try
        {
            var json1 = JsonSerializer.Serialize(obj1);
            var json2 = JsonSerializer.Serialize(obj2);
            return json1 == json2;
        }
        catch
        {
            return obj1.Equals(obj2);
        }
    }

    /// <summary>
    /// Deep clones an object using JSON serialization.
    /// </summary>
    /// <param name="obj">The object to clone.</param>
    /// <returns>A deep clone of the object.</returns>
    public static object? DeepClone(object? obj)
    {
        if (obj == null)
            return null;

        var type = obj.GetType();

        // Primitives and strings don't need cloning
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
            return obj;

        // Special handling for byte arrays
        if (obj is byte[] bytes)
        {
            var clone = new byte[bytes.Length];
            Array.Copy(bytes, clone, bytes.Length);
            return clone;
        }

        // Deep clone using JSON serialization
        try
        {
            var json = JsonSerializer.Serialize(obj);
            return JsonSerializer.Deserialize<object>(json);
        }
        catch
        {
            return obj;
        }
    }

    /// <summary>
    /// Creates a new error message with proper structure.
    /// </summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Error message.</param>
    /// <returns>A new Exception with the specified code.</returns>
    public static Exception CreateError(string code, string message)
    {
        var ex = new InvalidOperationException(message);
        ex.Data["code"] = code;
        return ex;
    }
}
