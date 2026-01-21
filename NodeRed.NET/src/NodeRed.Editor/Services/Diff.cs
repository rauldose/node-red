// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/ui/diff.js
// ============================================================
// TRANSLATION: JavaScript diff module to C# service
// ============================================================

using System.Text.Json;

namespace NodeRed.Editor.Services;

/// <summary>
/// Flow diff/comparison service.
/// Translated from RED.diff module.
/// </summary>
public class Diff
{
    private readonly EditorState _state;
    private List<Dictionary<string, object>> _lastDeployedState = new();
    private DateTime _lastDeployTime = DateTime.MinValue;

    public Diff(EditorState state)
    {
        _state = state;
    }

    /// <summary>
    /// Track last deployed state for comparison.
    /// </summary>
    public DateTime LastDeployTime => _lastDeployTime;

    /// <summary>
    /// Mark current state as deployed.
    /// </summary>
    public void MarkAsDeployed(List<Dictionary<string, object>> flows)
    {
        _lastDeployedState = flows.Select(f => new Dictionary<string, object>(f)).ToList();
        _lastDeployTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Check if there are changes since last deploy.
    /// </summary>
    public bool HasChanges()
    {
        if (_lastDeployedState.Count == 0) return true;
        var result = CompareCurrentWithDeployed();
        return result.HasChanges;
    }

    /// <summary>
    /// Compare two flow configurations.
    /// Translated from compareFlows() in diff.js
    /// </summary>
    public DiffResult Compare(List<Dictionary<string, object>> flowA, List<Dictionary<string, object>> flowB)
    {
        var result = new DiffResult();

        var nodesA = flowA.ToDictionary(n => n.TryGetValue("id", out var id) ? id?.ToString() ?? "" : "", n => n);
        var nodesB = flowB.ToDictionary(n => n.TryGetValue("id", out var id) ? id?.ToString() ?? "" : "", n => n);

        // Find added nodes
        foreach (var id in nodesB.Keys.Except(nodesA.Keys))
        {
            result.Added.Add(id);
            result.Changes.Add(new DiffChange
            {
                Id = id,
                Type = DiffChangeType.Added,
                NodeType = nodesB[id].TryGetValue("type", out var t) ? t?.ToString() ?? "" : "",
                Name = nodesB[id].TryGetValue("name", out var n) ? n?.ToString() : null
            });
        }

        // Find removed nodes
        foreach (var id in nodesA.Keys.Except(nodesB.Keys))
        {
            result.Removed.Add(id);
            result.Changes.Add(new DiffChange
            {
                Id = id,
                Type = DiffChangeType.Removed,
                NodeType = nodesA[id].TryGetValue("type", out var t) ? t?.ToString() ?? "" : "",
                Name = nodesA[id].TryGetValue("name", out var n) ? n?.ToString() : null
            });
        }

        // Find changed nodes
        foreach (var id in nodesA.Keys.Intersect(nodesB.Keys))
        {
            var nodeA = nodesA[id];
            var nodeB = nodesB[id];

            var propertyChanges = CompareNodes(nodeA, nodeB);
            if (propertyChanges.Count > 0)
            {
                result.Changed.Add(id);
                result.Changes.Add(new DiffChange
                {
                    Id = id,
                    Type = DiffChangeType.Changed,
                    NodeType = nodeA.TryGetValue("type", out var t) ? t?.ToString() ?? "" : "",
                    Name = nodeA.TryGetValue("name", out var n) ? n?.ToString() : null,
                    PropertyChanges = propertyChanges
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Compare current flow with deployed flow.
    /// Translated from compareCurrentWithDeployed() in diff.js
    /// </summary>
    public DiffResult CompareCurrentWithDeployed()
    {
        if (_lastDeployedState.Count == 0)
        {
            // No previous deploy - all nodes are "new"
            var result = new DiffResult();
            var currentNodes = _state.Nodes.GetNodes();
            foreach (var node in currentNodes)
            {
                result.Added.Add(node.Id);
                result.Changes.Add(new DiffChange
                {
                    Id = node.Id,
                    Type = DiffChangeType.Added,
                    NodeType = node.Type,
                    Name = node.Name
                });
            }
            return result;
        }
        
        // Get current state as dictionaries
        var currentState = GetCurrentStateAsDictionaries();
        return Compare(_lastDeployedState, currentState);
    }

    /// <summary>
    /// Get current editor state as list of dictionaries for comparison.
    /// </summary>
    private List<Dictionary<string, object>> GetCurrentStateAsDictionaries()
    {
        var result = new List<Dictionary<string, object>>();
        
        // Add workspaces/tabs
        foreach (var workspace in _state.Workspaces.GetAll())
        {
            result.Add(new Dictionary<string, object>
            {
                { "id", workspace.Id },
                { "type", workspace.Type },
                { "label", workspace.Label ?? "" },
                { "disabled", workspace.Disabled },
                { "info", workspace.Info ?? "" }
            });
        }
        
        // Add nodes
        foreach (var node in _state.Nodes.GetNodes())
        {
            var dict = new Dictionary<string, object>
            {
                { "id", node.Id },
                { "type", node.Type },
                { "name", node.Name ?? "" },
                { "x", node.X },
                { "y", node.Y },
                { "z", node.Z },
                { "wires", node.Wires ?? new List<List<string>>() }
            };
            
            if (!string.IsNullOrEmpty(node.GroupId))
            {
                dict["g"] = node.GroupId;
            }
            
            result.Add(dict);
        }
        
        // Add groups
        foreach (var group in _state.Nodes.GetGroups())
        {
            result.Add(new Dictionary<string, object>
            {
                { "id", group.Id },
                { "type", "group" },
                { "name", group.Name ?? "" },
                { "x", group.X },
                { "y", group.Y },
                { "w", group.Width },
                { "h", group.Height },
                { "z", group.Z },
                { "nodes", group.Nodes ?? new List<string>() }
            });
        }
        
        // Add subflows
        foreach (var subflow in _state.Nodes.GetAllSubflows())
        {
            result.Add(new Dictionary<string, object>
            {
                { "id", subflow.Id },
                { "type", "subflow" },
                { "name", subflow.Name ?? "" },
                { "info", subflow.Info ?? "" },
                { "color", subflow.Color ?? "" }
            });
        }
        
        return result;
    }

    /// <summary>
    /// Get only modified nodes since last deploy.
    /// </summary>
    public List<string> GetModifiedNodeIds()
    {
        var diff = CompareCurrentWithDeployed();
        return diff.Changed.Concat(diff.Added).ToList();
    }

    /// <summary>
    /// Get only modified flows (workspaces) since last deploy.
    /// </summary>
    public List<string> GetModifiedFlowIds()
    {
        var diff = CompareCurrentWithDeployed();
        var modifiedNodeIds = diff.Changed.Concat(diff.Added).Concat(diff.Removed).ToHashSet();
        var modifiedFlowIds = new HashSet<string>();
        
        // Find flows that contain modified nodes
        foreach (var node in _state.Nodes.GetNodes())
        {
            if (modifiedNodeIds.Contains(node.Id))
            {
                modifiedFlowIds.Add(node.Z);
            }
        }
        
        return modifiedFlowIds.ToList();
    }

    /// <summary>
    /// Compare two nodes.
    /// Translated from compareNodes() in diff.js
    /// </summary>
    private List<PropertyChange> CompareNodes(Dictionary<string, object> nodeA, Dictionary<string, object> nodeB)
    {
        var changes = new List<PropertyChange>();
        var allKeys = nodeA.Keys.Union(nodeB.Keys).Except(new[] { "changed", "moved", "dirty" });

        foreach (var key in allKeys)
        {
            var hasA = nodeA.TryGetValue(key, out var valueA);
            var hasB = nodeB.TryGetValue(key, out var valueB);

            if (!hasA && hasB)
            {
                changes.Add(new PropertyChange
                {
                    Property = key,
                    Type = DiffChangeType.Added,
                    OldValue = null,
                    NewValue = valueB
                });
            }
            else if (hasA && !hasB)
            {
                changes.Add(new PropertyChange
                {
                    Property = key,
                    Type = DiffChangeType.Removed,
                    OldValue = valueA,
                    NewValue = null
                });
            }
            else if (hasA && hasB && !ValuesEqual(valueA, valueB))
            {
                changes.Add(new PropertyChange
                {
                    Property = key,
                    Type = DiffChangeType.Changed,
                    OldValue = valueA,
                    NewValue = valueB
                });
            }
        }

        return changes;
    }

    /// <summary>
    /// Check if two values are equal.
    /// </summary>
    private bool ValuesEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;

        // For JsonElement, serialize and compare
        if (a is JsonElement jsonA && b is JsonElement jsonB)
        {
            return jsonA.GetRawText() == jsonB.GetRawText();
        }

        return a.Equals(b);
    }

    /// <summary>
    /// Find conflicts between two diffs.
    /// Translated from findConflicts() in diff.js
    /// </summary>
    public List<DiffConflict> FindConflicts(DiffResult diffA, DiffResult diffB)
    {
        var conflicts = new List<DiffConflict>();

        // Find nodes changed in both
        var bothChanged = diffA.Changed.Intersect(diffB.Changed).ToList();

        foreach (var id in bothChanged)
        {
            var changeA = diffA.Changes.First(c => c.Id == id);
            var changeB = diffB.Changes.First(c => c.Id == id);

            // Check for conflicting property changes
            var propsA = changeA.PropertyChanges.Select(p => p.Property).ToHashSet();
            var propsB = changeB.PropertyChanges.Select(p => p.Property).ToHashSet();
            var conflictingProps = propsA.Intersect(propsB).ToList();

            if (conflictingProps.Count > 0)
            {
                conflicts.Add(new DiffConflict
                {
                    Id = id,
                    NodeType = changeA.NodeType,
                    ConflictingProperties = conflictingProps,
                    ChangeA = changeA,
                    ChangeB = changeB
                });
            }
        }

        return conflicts;
    }
}

/// <summary>
/// Result of a diff operation.
/// </summary>
public class DiffResult
{
    public List<string> Added { get; } = new();
    public List<string> Removed { get; } = new();
    public List<string> Changed { get; } = new();
    public List<DiffChange> Changes { get; } = new();

    public bool HasChanges => Added.Count > 0 || Removed.Count > 0 || Changed.Count > 0;
}

/// <summary>
/// A single change in a diff.
/// </summary>
public class DiffChange
{
    public string Id { get; set; } = "";
    public DiffChangeType Type { get; set; }
    public string NodeType { get; set; } = "";
    public string? Name { get; set; }
    public List<PropertyChange> PropertyChanges { get; set; } = new();
}

/// <summary>
/// A property change within a node.
/// </summary>
public class PropertyChange
{
    public string Property { get; set; } = "";
    public DiffChangeType Type { get; set; }
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
}

/// <summary>
/// A conflict between two diffs.
/// </summary>
public class DiffConflict
{
    public string Id { get; set; } = "";
    public string NodeType { get; set; } = "";
    public string ConflictType { get; set; } = "property";
    public List<string> ConflictingProperties { get; set; } = new();
    public DiffChange? ChangeA { get; set; }
    public DiffChange? ChangeB { get; set; }
}

/// <summary>
/// Type of change in a diff.
/// </summary>
public enum DiffChangeType
{
    Added,
    Removed,
    Changed
}
