// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/ui/subflow.js
// ============================================================
// TRANSLATION: JavaScript subflow module to C# service
// ============================================================

namespace NodeRed.Editor.Services;

/// <summary>
/// Subflow management service for creating and editing subflows.
/// Translated from RED.subflow module.
/// </summary>
public class SubflowManager
{
    private readonly EditorState _state;
    private readonly History _history;

    public SubflowManager(EditorState state, History history)
    {
        _state = state;
        _history = history;
    }

    /// <summary>
    /// Create a subflow from selected nodes.
    /// Translated from createSubflow() in subflow.js
    /// Note: Full implementation requires EditorState integration to move nodes into subflow workspace.
    /// </summary>
    public Subflow? CreateSubflow(string name, IEnumerable<FlowNode>? nodes = null)
    {
        // TODO: Full implementation would:
        // 1. Calculate input/output ports from external connections
        // 2. Create subflow workspace
        // 3. Move nodes into subflow workspace
        var subflow = new Subflow
        {
            Id = Guid.NewGuid().ToString(),
            Type = "subflow",
            Name = name
        };

        return subflow;
    }

    /// <summary>
    /// Convert subflow to regular nodes.
    /// Translated from convertToNodes() in subflow.js
    /// Note: Full implementation requires EditorNodes access to get subflow nodes.
    /// </summary>
    public List<FlowNode> ConvertToNodes(Subflow subflow)
    {
        // TODO: Full implementation would clone nodes from subflow workspace to current flow
        var convertedNodes = new List<FlowNode>();
        return convertedNodes;
    }

    /// <summary>
    /// Delete a subflow.
    /// Translated from delete() in subflow.js
    /// Note: Full implementation requires EditorNodes integration.
    /// </summary>
    public void DeleteSubflow(string subflowId)
    {
        // TODO: Full implementation would:
        // 1. Check for instances in use
        // 2. Delete subflow nodes
        // 3. Remove subflow workspace
        // 4. Remove subflow definition
    }

    /// <summary>
    /// Update subflow properties.
    /// Translated from update() in subflow.js
    /// </summary>
    public void UpdateSubflow(Subflow subflow, string? name = null)
    {
        if (name != null) subflow.Name = name;
    }

    /// <summary>
    /// Get subflow instance count.
    /// Note: Full implementation requires EditorNodes access to count subflow instances.
    /// </summary>
    public int GetInstanceCount(string subflowId)
    {
        // TODO: Full implementation would count nodes with type "subflow:{subflowId}"
        return 0;
    }

    /// <summary>
    /// Create an instance of a subflow.
    /// </summary>
    public FlowNode CreateInstance(Subflow subflow, double x, double y, string? z = null)
    {
        var instance = new FlowNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = $"subflow:{subflow.Id}",
            Name = "",
            X = x,
            Y = y,
            Z = z ?? _state.Workspaces.Active(),
            Dirty = true
        };

        return instance;
    }
}
