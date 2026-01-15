// ============================================================
// INSPIRED BY: @node-red/runtime/lib/flows/Group.js
// REFERENCE: NODE-RED-ARCHITECTURE-ANALYSIS.md - Groups section
// ============================================================
// Group - container for organizing nodes visually and logically
// ============================================================

/*!
 * Original work Copyright JS Foundation and other contributors, http://js.foundation
 * Modified work Copyright 2026 NodeRed.NET Contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 */

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace NodeRed.Runtime;

/// <summary>
/// Group configuration from JSON
/// Maps to: Group definition in Node-RED
/// </summary>
public class GroupConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Style { get; set; }
    public List<string> Nodes { get; set; } = new(); // Node IDs in this group
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string? ParentGroupId { get; set; } // For nested groups
    public Dictionary<string, object>? Env { get; set; } // Environment variables
}

/// <summary>
/// Group - represents a visual and logical container for nodes
/// Maps to: Group class in @node-red/runtime/lib/flows/Group.js
/// Reference: NODE-RED-ARCHITECTURE-ANALYSIS.md - Advanced Features
/// 
/// Groups provide:
/// - Visual organization of nodes on the canvas
/// - Logical grouping with shared environment variables
/// - Nested group support
/// - Move/resize multiple nodes together
/// </summary>
public class Group
{
    private readonly GroupConfiguration _config;
    private readonly ILogger _logger;
    private readonly Dictionary<string, object?> _env = new();
    private readonly object? _parent;

    public string Id => _config.Id;
    public string Name => _config.Name;
    public string Type => "group";
    public IReadOnlyList<string> Nodes => _config.Nodes.AsReadOnly();
    public string? ParentGroupId => _config.ParentGroupId;

    public Group(GroupConfiguration config, ILogger logger, object? parent = null)
    {
        _config = config;
        _logger = logger;
        _parent = parent;
    }

    /// <summary>
    /// Initialize the group and evaluate environment variables
    /// Maps to: Group.start() in Node-RED
    /// </summary>
    public async Task StartAsync()
    {
        if (_config.Env != null)
        {
            foreach (var kvp in _config.Env)
            {
                // In full implementation, would evaluate env property values
                _env[kvp.Key] = kvp.Value;
            }
        }

        _logger.LogDebug($"Started group: {Name} ({Id})");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Get a group setting value
    /// Maps to: Group.getSetting() in Node-RED
    /// </summary>
    public object? GetSetting(string key)
    {
        if (key == "NR_GROUP_NAME")
        {
            return Name;
        }

        if (key == "NR_GROUP_ID")
        {
            return Id;
        }

        // Check local env variables
        if (_env.ContainsKey(key))
        {
            return _env[key];
        }

        // Check parent group/flow if key starts with $parent.
        if (key.StartsWith("$parent."))
        {
            var parentKey = key.Substring(8);
            // In full implementation, would delegate to parent
            return null;
        }

        return null;
    }

    /// <summary>
    /// Add a node to this group
    /// </summary>
    public void AddNode(string nodeId)
    {
        if (!_config.Nodes.Contains(nodeId))
        {
            _config.Nodes.Add(nodeId);
            _logger.LogDebug($"Added node {nodeId} to group {Name}");
        }
    }

    /// <summary>
    /// Remove a node from this group
    /// </summary>
    public void RemoveNode(string nodeId)
    {
        if (_config.Nodes.Remove(nodeId))
        {
            _logger.LogDebug($"Removed node {nodeId} from group {Name}");
        }
    }

    /// <summary>
    /// Check if a node is in this group
    /// </summary>
    public bool ContainsNode(string nodeId)
    {
        return _config.Nodes.Contains(nodeId);
    }

    /// <summary>
    /// Update group bounds (position and size)
    /// </summary>
    public void UpdateBounds(double x, double y, double width, double height)
    {
        _config.X = x;
        _config.Y = y;
        _config.Width = width;
        _config.Height = height;
    }

    /// <summary>
    /// Get group configuration
    /// </summary>
    public GroupConfiguration GetConfiguration()
    {
        return _config;
    }
}
