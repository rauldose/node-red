// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/ui/group.js
// ============================================================
// TRANSLATION: JavaScript group module to C# service
// ============================================================

namespace NodeRed.Editor.Services;

/// <summary>
/// Group management service for creating and editing node groups.
/// Translated from RED.group module.
/// </summary>
public class GroupManager
{
    private readonly EditorState _state;
    private readonly History _history;

    public GroupManager(EditorState state, History history)
    {
        _state = state;
        _history = history;
    }

    /// <summary>
    /// Create a group from selected nodes.
    /// Translated from createGroup() in group.js
    /// </summary>
    public NodeGroup? CreateGroup(IEnumerable<FlowNode>? nodes = null, string? workspaceId = null)
    {
        var nodesToGroup = nodes?.ToList();
        
        if (nodesToGroup == null || nodesToGroup.Count == 0)
        {
            return null;
        }

        // Calculate bounding box using actual node dimensions
        var minX = nodesToGroup.Min(n => n.X - n.Width / 2.0);
        var minY = nodesToGroup.Min(n => n.Y - n.Height / 2.0);
        var maxX = nodesToGroup.Max(n => n.X + n.Width / 2.0);
        var maxY = nodesToGroup.Max(n => n.Y + n.Height / 2.0);

        var padding = 20;
        var group = new NodeGroup
        {
            Id = Guid.NewGuid().ToString(),
            Type = "group",
            Name = "",
            Z = workspaceId ?? nodesToGroup.First().Z,
            X = minX - padding,
            Y = minY - padding,
            Width = (maxX - minX) + (padding * 2),
            Height = (maxY - minY) + (padding * 2),
            Style = new GroupStyle
            {
                Stroke = "#a4a4a4",
                StrokeOpacity = 1.0,
                Fill = "none",
                FillOpacity = 0.2,
                Label = true,
                LabelPosition = "nw",
                Color = "#a4a4a4"
            },
            Nodes = nodesToGroup.Select(n => n.Id).ToList()
        };
        
        // Add nodes to the group
        foreach (var node in nodesToGroup)
        {
            node.GroupId = group.Id;
        }
        
        // Add group to state
        _state.Nodes.AddGroup(group);

        // Record history
        _history.Push(new HistoryEvent
        {
            Type = HistoryEventType.CreateGroup,
            Group = group
        });

        return group;
    }

    /// <summary>
    /// Ungroup selected nodes.
    /// Translated from ungroup() in group.js
    /// </summary>
    public void UngroupSelection(List<FlowNode> nodes)
    {
        // TODO: Full implementation would remove group membership from selected nodes
        // For now, just record that an ungroup happened
        if (nodes.Count > 0)
        {
            _history.Push(new HistoryEvent
            {
                Type = HistoryEventType.DeleteGroup,
                NodeIds = nodes.Select(n => n.Id).ToList()
            });
        }
    }

    /// <summary>
    /// Ungroup nodes.
    /// Translated from ungroup() in group.js
    /// Note: Full implementation requires EditorNodes access to get nodes in group.
    /// </summary>
    public List<FlowNode> Ungroup(NodeGroup group)
    {
        // TODO: Full implementation would:
        // 1. Get all nodes in the group
        // 2. Remove group from state
        // 3. Return the ungrouped nodes
        var nodes = new List<FlowNode>();

        // Record history
        _history.Push(new HistoryEvent
        {
            Type = HistoryEventType.DeleteGroup,
            Group = group
        });

        return nodes;
    }

    /// <summary>
    /// Add nodes to existing group.
    /// Translated from addToGroup() in group.js
    /// </summary>
    public void AddToGroup(NodeGroup group, IEnumerable<FlowNode> nodes)
    {
        var nodeIds = nodes.Select(n => n.Id).ToList();

        // Record history
        _history.Push(new HistoryEvent
        {
            Type = HistoryEventType.AddToGroup,
            GroupId = group.Id,
            NodeIds = nodeIds
        });
    }

    /// <summary>
    /// Remove nodes from group.
    /// Translated from removeFromGroup() in group.js
    /// </summary>
    public void RemoveFromGroup(NodeGroup group, IEnumerable<FlowNode> nodes)
    {
        var nodeIds = nodes.Select(n => n.Id).ToList();

        // Record history
        _history.Push(new HistoryEvent
        {
            Type = HistoryEventType.RemoveFromGroup,
            GroupId = group.Id,
            NodeIds = nodeIds
        });
    }

    /// <summary>
    /// Edit group properties.
    /// </summary>
    public void EditGroup(NodeGroup group, string? name = null)
    {
        if (name != null)
        {
            group.Name = name;
        }
    }
}
