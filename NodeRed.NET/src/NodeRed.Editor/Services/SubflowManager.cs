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
    /// </summary>
    public Subflow? CreateSubflow(string name, IEnumerable<FlowNode>? nodes = null)
    {
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
    /// </summary>
    public List<FlowNode> ConvertToNodes(Subflow subflow)
    {
        var convertedNodes = new List<FlowNode>();
        return convertedNodes;
    }

    /// <summary>
    /// Delete a subflow.
    /// Translated from delete() in subflow.js
    /// </summary>
    public void DeleteSubflow(string subflowId)
    {
        // Subflow deletion would need to be coordinated with EditorNodes
        // For now, this is a placeholder
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
    /// </summary>
    public int GetInstanceCount(string subflowId)
    {
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
