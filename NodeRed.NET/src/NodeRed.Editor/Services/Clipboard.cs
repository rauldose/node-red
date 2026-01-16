// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/ui/clipboard.js
// ============================================================
// TRANSLATION: JavaScript clipboard module to C# service
// ============================================================

using System.Text.Json;

namespace NodeRed.Editor.Services;

/// <summary>
/// Clipboard service for copy/paste operations on nodes.
/// Translated from RED.clipboard module.
/// </summary>
public class Clipboard
{
    private readonly EditorState _state;
    private List<Dictionary<string, object>> _clipboardData = new();
    private bool _disabled = false;

    public Clipboard(EditorState state)
    {
        _state = state;
    }

    /// <summary>
    /// Copy currently selected nodes to clipboard.
    /// Translated from copyNodes() in clipboard.js
    /// </summary>
    public void CopySelection(IEnumerable<FlowNode> selection)
    {
        if (_disabled) return;

        if (!selection.Any()) return;

        _clipboardData.Clear();

        foreach (var node in selection)
        {
            // Clone the node data
            var nodeClone = CloneNode(node);
            _clipboardData.Add(nodeClone);
        }
    }

    /// <summary>
    /// Paste nodes from clipboard.
    /// Translated from pasteNodes() in clipboard.js
    /// </summary>
    public List<FlowNode> PasteNodes(double offsetX = 20, double offsetY = 20)
    {
        if (_clipboardData.Count == 0) return new List<FlowNode>();

        var pastedNodes = new List<FlowNode>();
        var idMap = new Dictionary<string, string>();
        var activeWorkspace = _state.Workspaces.Active();

        // First pass: create new nodes with new IDs
        foreach (var nodeData in _clipboardData)
        {
            var oldId = nodeData.TryGetValue("id", out var id) ? id?.ToString() ?? "" : "";
            var newId = Guid.NewGuid().ToString();
            idMap[oldId] = newId;

            var newNode = new FlowNode
            {
                Id = newId,
                Type = nodeData.TryGetValue("type", out var type) ? type?.ToString() ?? "" : "",
                Name = nodeData.TryGetValue("name", out var name) ? name?.ToString() ?? "" : "",
                X = (nodeData.TryGetValue("x", out var x) && x is JsonElement xElem ? xElem.GetDouble() : 0) + offsetX,
                Y = (nodeData.TryGetValue("y", out var y) && y is JsonElement yElem ? yElem.GetDouble() : 0) + offsetY,
                Z = activeWorkspace,
                Dirty = true
            };

            // Copy additional properties
            foreach (var kvp in nodeData)
            {
                if (kvp.Key != "id" && kvp.Key != "x" && kvp.Key != "y" && kvp.Key != "z" &&
                    kvp.Key != "type" && kvp.Key != "name" && kvp.Key != "wires")
                {
                    newNode.Properties[kvp.Key] = kvp.Value;
                }
            }

            pastedNodes.Add(newNode);
        }

        return pastedNodes;
    }

    /// <summary>
    /// Check if clipboard has data.
    /// </summary>
    public bool HasData() => _clipboardData.Count > 0;

    /// <summary>
    /// Export nodes to JSON string.
    /// Translated from exportNodes() in clipboard.js
    /// </summary>
    public string ExportNodes(IEnumerable<FlowNode> nodes)
    {
        var exportData = nodes.Select(n => new Dictionary<string, object>
        {
            ["id"] = n.Id,
            ["type"] = n.Type,
            ["x"] = n.X,
            ["y"] = n.Y,
            ["z"] = n.Z ?? "",
            ["name"] = n.Name ?? ""
        }).ToList();

        foreach (var (node, data) in nodes.Zip(exportData))
        {
            foreach (var prop in node.Properties)
            {
                data[prop.Key] = prop.Value;
            }
        }

        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Import nodes from JSON string.
    /// Translated from importNodes() in clipboard.js
    /// </summary>
    public List<FlowNode> ImportNodes(string json)
    {
        try
        {
            var nodesData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
            if (nodesData == null) return new List<FlowNode>();

            _clipboardData = nodesData;
            return PasteNodes();
        }
        catch
        {
            return new List<FlowNode>();
        }
    }

    private Dictionary<string, object> CloneNode(FlowNode node)
    {
        var clone = new Dictionary<string, object>
        {
            ["id"] = node.Id,
            ["type"] = node.Type,
            ["x"] = node.X,
            ["y"] = node.Y,
            ["z"] = node.Z ?? "",
            ["name"] = node.Name ?? ""
        };

        foreach (var prop in node.Properties)
        {
            clone[prop.Key] = prop.Value;
        }

        return clone;
    }

    public void Disable() => _disabled = true;
    public void Enable() => _disabled = false;
}
