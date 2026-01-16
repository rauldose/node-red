// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/ui/library.js
// ============================================================
// TRANSLATION: JavaScript library module to C# service
// ============================================================

using System.Text.Json;

namespace NodeRed.Editor.Services;

/// <summary>
/// Library management service for import/export of flows.
/// Translated from RED.library module.
/// </summary>
public class Library
{
    private readonly EditorState _state;
    private readonly Dictionary<string, LibraryEntry> _localLibrary = new();

    public event EventHandler<LibraryChangedEventArgs>? LibraryChanged;

    public Library(EditorState state)
    {
        _state = state;
    }

    /// <summary>
    /// Save flows to library.
    /// Translated from saveToLibrary() in library.js
    /// </summary>
    public async Task<bool> SaveToLibrary(string path, string name, IEnumerable<FlowNode> nodes)
    {
        try
        {
            var flowData = SerializeNodes(nodes);
            var entry = new LibraryEntry
            {
                Id = Guid.NewGuid().ToString(),
                Path = path,
                Name = name,
                Type = "flows",
                Data = flowData,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var key = $"{path}/{name}";
            _localLibrary[key] = entry;

            LibraryChanged?.Invoke(this, new LibraryChangedEventArgs { Action = "save", Path = key });

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Load flows from library.
    /// Translated from loadFromLibrary() in library.js
    /// </summary>
    public async Task<List<FlowNode>?> LoadFromLibrary(string path)
    {
        try
        {
            if (!_localLibrary.TryGetValue(path, out var entry))
            {
                return null;
            }

            return DeserializeNodes(entry.Data);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get library contents.
    /// Translated from getLibrary() in library.js
    /// </summary>
    public List<LibraryEntry> GetLibrary(string type = "flows", string path = "/")
    {
        return _localLibrary.Values
            .Where(e => e.Type == type && e.Path.StartsWith(path))
            .OrderBy(e => e.Path)
            .ThenBy(e => e.Name)
            .ToList();
    }

    /// <summary>
    /// Export nodes to JSON string.
    /// Translated from exportNodes() in library.js
    /// </summary>
    public string ExportNodes(IEnumerable<FlowNode> nodes, bool pretty = true)
    {
        return SerializeNodes(nodes, pretty);
    }

    /// <summary>
    /// Export all flows to JSON string.
    /// </summary>
    public string ExportAllFlows(bool pretty = true)
    {
        var flows = _state.Workspaces.GetAll()
            .Select(w => new Dictionary<string, object>
            {
                ["id"] = w.Id,
                ["type"] = "tab",
                ["label"] = w.Label,
                ["disabled"] = w.Disabled,
                ["info"] = w.Info ?? ""
            });

        var exportData = flows.Cast<object>().ToList();

        var options = new JsonSerializerOptions 
        { 
            WriteIndented = pretty 
        };
        return JsonSerializer.Serialize(exportData, options);
    }

    /// <summary>
    /// Import nodes from JSON string.
    /// Translated from importNodes() in library.js
    /// Note: Full import implementation requires EditorState integration.
    /// </summary>
    public LibraryImportResult ImportNodes(string json, bool replaceAll = false)
    {
        try
        {
            var data = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);
            if (data == null)
            {
                return new LibraryImportResult { Success = false, Error = "Invalid JSON format" };
            }

            var result = new LibraryImportResult { Success = true };

            // TODO: Full implementation would:
            // 1. Parse tabs/flows, subflows, and nodes from JSON
            // 2. Generate new IDs for pasted nodes
            // 3. Add to EditorState
            // 4. Create wires between nodes

            return result;
        }
        catch (Exception ex)
        {
            return new LibraryImportResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Delete from library.
    /// </summary>
    public bool DeleteFromLibrary(string path)
    {
        if (_localLibrary.Remove(path))
        {
            LibraryChanged?.Invoke(this, new LibraryChangedEventArgs { Action = "delete", Path = path });
            return true;
        }
        return false;
    }

    private string SerializeNodes(IEnumerable<FlowNode> nodes, bool pretty = true)
    {
        var data = nodes.Select(NodeToDictionary).ToList();
        var options = new JsonSerializerOptions { WriteIndented = pretty };
        return JsonSerializer.Serialize(data, options);
    }

    private Dictionary<string, object> NodeToDictionary(FlowNode node)
    {
        var dict = new Dictionary<string, object>
        {
            ["id"] = node.Id,
            ["type"] = node.Type,
            ["x"] = node.X,
            ["y"] = node.Y,
            ["z"] = node.Z ?? ""
        };

        if (!string.IsNullOrEmpty(node.Name))
        {
            dict["name"] = node.Name;
        }

        foreach (var prop in node.Properties)
        {
            dict[prop.Key] = prop.Value;
        }

        return dict;
    }

    private List<FlowNode>? DeserializeNodes(string json)
    {
        var data = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);
        if (data == null) return null;

        return data.Select(item => new FlowNode
        {
            Id = item.TryGetValue("id", out var id) ? id.GetString() ?? "" : Guid.NewGuid().ToString(),
            Type = item.TryGetValue("type", out var type) ? type.GetString() ?? "" : "",
            Name = item.TryGetValue("name", out var name) ? name.GetString() ?? "" : "",
            X = item.TryGetValue("x", out var x) ? x.GetDouble() : 0,
            Y = item.TryGetValue("y", out var y) ? y.GetDouble() : 0,
            Z = item.TryGetValue("z", out var z) ? z.GetString() ?? "" : ""
        }).ToList();
    }
}

/// <summary>
/// Library entry model.
/// </summary>
public class LibraryEntry
{
    public string Id { get; set; } = "";
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Data { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Result of import operation.
/// </summary>
public class LibraryImportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<FlowNode> Nodes { get; } = new();
    public List<NodeLink> Links { get; } = new();
    public List<FlowWorkspace> Flows { get; } = new();
}

/// <summary>
/// Library changed event args.
/// </summary>
public class LibraryChangedEventArgs : EventArgs
{
    public string Action { get; set; } = "";
    public string Path { get; set; } = "";
}
