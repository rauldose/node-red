// ============================================================
// INSPIRED BY: @node-red/runtime message structure
// REFERENCE: NODE-RED-ARCHITECTURE-ANALYSIS.md - Message Structure section
// ============================================================
// Message format compatible with Node-RED's message structure
// ============================================================

/*!
 * Original work Copyright JS Foundation and other contributors, http://js.foundation
 * Modified work Copyright 2026 NodeRed.NET Contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 */

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NodeRed.Runtime;

/// <summary>
/// Base message object structure matching Node-RED's message format
/// Reference: NODE-RED-ARCHITECTURE-ANALYSIS.md - Message Structure
/// </summary>
public class FlowMessage
{
    /// <summary>
    /// Unique message identifier
    /// Maps to: msg._msgid in Node-RED
    /// </summary>
    [JsonPropertyName("_msgid")]
    public string MsgId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Message payload (can be any type)
    /// Maps to: msg.payload in Node-RED
    /// </summary>
    [JsonPropertyName("payload")]
    public object? Payload { get; set; }

    /// <summary>
    /// Optional message topic
    /// Maps to: msg.topic in Node-RED
    /// </summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    /// <summary>
    /// Internal event name (optional)
    /// Maps to: msg._event in Node-RED
    /// </summary>
    [JsonPropertyName("_event")]
    public string? Event { get; set; }

    /// <summary>
    /// Additional properties stored dynamically
    /// Allows for arbitrary properties like Node-RED
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }

    /// <summary>
    /// Create a deep clone of this message
    /// Node-RED clones messages when sending to multiple nodes
    /// Uses rfdc (really fast deep clone) pattern
    /// </summary>
    public FlowMessage Clone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<FlowMessage>(json) ?? new FlowMessage();
    }

    /// <summary>
    /// Get an additional property value
    /// </summary>
    public T? GetProperty<T>(string key)
    {
        if (AdditionalProperties != null && AdditionalProperties.TryGetValue(key, out var element))
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        return default;
    }

    /// <summary>
    /// Set an additional property value
    /// </summary>
    public void SetProperty<T>(string key, T value)
    {
        AdditionalProperties ??= new Dictionary<string, JsonElement>();
        var json = JsonSerializer.Serialize(value);
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        AdditionalProperties[key] = element;
    }
}
