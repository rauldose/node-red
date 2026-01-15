// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/red.js
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/nodes.js
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/settings.js
// ============================================================
// This file contains the EditorState service which holds all editor state,
// translating the global RED object and its sub-modules from the Node-RED
// editor client.
// ============================================================

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NodeRed.Editor.Services;

/// <summary>
/// Central state container for the editor, translating the global RED object.
/// SOURCE: packages/node_modules/@node-red/editor-client/src/js/red.js
/// </summary>
public class EditorState
{
    // ============================================================
    // Sub-modules - translated from RED.* properties
    // ============================================================
    
    public EditorEvents Events { get; } = new();
    public EditorSettings Settings { get; } = new();
    public EditorNodes Nodes { get; } = new();
    public EditorPlugins Plugins { get; } = new();
    public EditorI18n I18n { get; } = new();
    public EditorWorkspaces Workspaces { get; } = new();
    public EditorMenu Menu { get; } = new();
    public EditorDeploy Deploy { get; } = new();
    public EditorEventLog EventLog { get; } = new();
    
    public ThemeSettings? Theme { get; set; }
    
    // Plugin and node configs loaded from server
    public List<string> PluginConfigs { get; } = new();
    public List<string> NodeConfigs { get; } = new();
}

// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/events.js
// ============================================================
// ORIGINAL CODE:
// var handlers = {};
// return {
//     on: function(evt,func) { ... },
//     off: function(evt,func) { ... },
//     emit: function(evt,arg) { ... }
// }
// ============================================================

/// <summary>
/// Event system for the editor - translated from RED.events
/// </summary>
public class EditorEvents
{
    private readonly ConcurrentDictionary<string, List<Action<object?>>> _handlers = new();
    private readonly object _lockObj = new();

    public void On(string eventName, Action<object?> handler)
    {
        _handlers.AddOrUpdate(
            eventName,
            _ => new List<Action<object?>> { handler },
            (_, list) => { lock (_lockObj) { list.Add(handler); } return list; }
        );
    }

    public void Off(string eventName, Action<object?>? handler = null)
    {
        if (handler == null)
        {
            _handlers.TryRemove(eventName, out _);
        }
        else if (_handlers.TryGetValue(eventName, out var list))
        {
            lock (_lockObj) { list.Remove(handler); }
        }
    }

    public void Emit(string eventName, object? arg)
    {
        if (_handlers.TryGetValue(eventName, out var list))
        {
            List<Action<object?>> handlersCopy;
            lock (_lockObj) { handlersCopy = list.ToList(); }
            
            foreach (var handler in handlersCopy)
            {
                try
                {
                    handler(arg);
                }
                catch
                {
                    // Ignore handler errors
                }
            }
        }
    }
}

// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/settings.js
// ============================================================
// ORIGINAL CODE:
// var settings = {};
// var userSettings = {};
// function get(key, defaultValue) { ... }
// function set(key, value) { ... }
// function getLocal(key) { return localStorage.getItem("nr-editor-" + key); }
// function setLocal(key, value) { localStorage.setItem("nr-editor-" + key, value); }
// ============================================================

/// <summary>
/// Settings manager - translated from RED.settings
/// </summary>
public class EditorSettings
{
    private Dictionary<string, object?> _settings = new();
    private Dictionary<string, object?> _userSettings = new();
    private readonly Dictionary<string, string?> _localStorage = new();
    
    public string Version { get; private set; } = "";
    public Dictionary<string, object?>? EditorTheme { get; set; }
    public string ApiRootUrl { get; private set; } = "";

    public async Task InitAsync(string apiRootUrl)
    {
        ApiRootUrl = apiRootUrl;
        // In a real implementation, this would load settings from the server
        await Task.CompletedTask;
    }

    public T? Get<T>(string key, T? defaultValue = default)
    {
        var parts = key.Split('.');
        object? current = _settings;

        foreach (var part in parts)
        {
            if (current is Dictionary<string, object?> dict && dict.TryGetValue(part, out var value))
            {
                current = value;
            }
            else
            {
                return defaultValue;
            }
        }

        if (current is T typedValue)
        {
            return typedValue;
        }

        return defaultValue;
    }

    public void Set(string key, object? value)
    {
        _userSettings[key] = value;
    }

    public string? GetLocal(string key)
    {
        return _localStorage.TryGetValue("nr-editor-" + key, out var value) ? value : null;
    }

    public void SetLocal(string key, string? value)
    {
        _localStorage["nr-editor-" + key] = value;
    }

    public T? Theme<T>(string key, T? defaultValue = default)
    {
        if (EditorTheme == null) return defaultValue;

        var parts = key.Split('.');
        object? current = EditorTheme;

        foreach (var part in parts)
        {
            if (current is Dictionary<string, object?> dict && dict.TryGetValue(part, out var value))
            {
                current = value;
            }
            else
            {
                return defaultValue;
            }
        }

        if (current is T typedValue)
        {
            return typedValue;
        }

        return defaultValue;
    }

    public async Task RefreshSettingsAsync()
    {
        // Reload settings from server
        await Task.CompletedTask;
    }
}

// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/nodes.js
// ============================================================
// ORIGINAL CODE (partial - lines 1-100):
// var node_defs = {};
// var nodes = [];
// var configNodes = {};
// var links = [];
// var workspaces = {};
// var workspacesOrder = [];
// var subflows = {};
// var groups = [];
// var junctions = [];
// var dirty = false;
// ============================================================

/// <summary>
/// Node registry and management - translated from RED.nodes
/// </summary>
public class EditorNodes
{
    private readonly Dictionary<string, NodeDefinition> _nodeDefs = new();
    private readonly List<FlowNode> _nodes = new();
    private readonly Dictionary<string, FlowNode> _configNodes = new();
    private readonly List<NodeLink> _links = new();
    private readonly Dictionary<string, FlowWorkspace> _workspaces = new();
    private readonly List<string> _workspacesOrder = new();
    private readonly Dictionary<string, Subflow> _subflows = new();
    private readonly List<NodeGroup> _groups = new();
    private readonly List<Junction> _junctions = new();
    private readonly Dictionary<string, List<string>> _iconSets = new();
    private readonly List<NodeSetInfo> _nodeList = new();
    
    private bool _dirty = false;
    private string? _version;

    public async Task InitAsync()
    {
        // Initialize nodes system
        await Task.CompletedTask;
    }

    public void SetNodeList(List<NodeSetInfo> nodeList)
    {
        _nodeList.Clear();
        _nodeList.AddRange(nodeList);
    }

    public void SetIconSets(Dictionary<string, List<string>> iconSets)
    {
        _iconSets.Clear();
        foreach (var kvp in iconSets)
        {
            _iconSets[kvp.Key] = kvp.Value;
        }
    }

    public void SetVersion(string? rev)
    {
        _version = rev;
    }

    public string? GetVersion() => _version;

    public void Import(List<JsonElement>? flows)
    {
        if (flows == null) return;

        foreach (var flow in flows)
        {
            var type = flow.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

            if (type == "tab")
            {
                ImportWorkspace(flow);
            }
            else if (type == "subflow")
            {
                ImportSubflow(flow);
            }
            else if (type == "group")
            {
                ImportGroup(flow);
            }
            else if (type == "junction")
            {
                ImportJunction(flow);
            }
            else
            {
                ImportNode(flow);
            }
        }

        // Import links after all nodes are imported
        ImportLinks(flows);
    }

    private void ImportWorkspace(JsonElement flow)
    {
        var ws = new FlowWorkspace
        {
            Id = flow.GetProperty("id").GetString() ?? "",
            Type = "tab",
            Label = flow.TryGetProperty("label", out var label) ? label.GetString() ?? "" : "",
            Disabled = flow.TryGetProperty("disabled", out var disabled) && disabled.GetBoolean(),
            Info = flow.TryGetProperty("info", out var info) ? info.GetString() ?? "" : "",
        };

        _workspaces[ws.Id] = ws;
        _workspacesOrder.Add(ws.Id);
    }

    private void ImportSubflow(JsonElement flow)
    {
        var sf = new Subflow
        {
            Id = flow.GetProperty("id").GetString() ?? "",
            Type = "subflow",
            Name = flow.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
        };

        _subflows[sf.Id] = sf;
    }

    private void ImportGroup(JsonElement flow)
    {
        var group = new NodeGroup
        {
            Id = flow.GetProperty("id").GetString() ?? "",
            Type = "group",
            Name = flow.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            Z = flow.TryGetProperty("z", out var z) ? z.GetString() ?? "" : "",
        };

        _groups.Add(group);
    }

    private void ImportJunction(JsonElement flow)
    {
        var junction = new Junction
        {
            Id = flow.GetProperty("id").GetString() ?? "",
            Type = "junction",
            Z = flow.TryGetProperty("z", out var z) ? z.GetString() ?? "" : "",
            X = flow.TryGetProperty("x", out var x) ? x.GetDouble() : 0,
            Y = flow.TryGetProperty("y", out var y) ? y.GetDouble() : 0,
        };

        _junctions.Add(junction);
    }

    private void ImportNode(JsonElement flow)
    {
        var node = new FlowNode
        {
            Id = flow.GetProperty("id").GetString() ?? "",
            Type = flow.TryGetProperty("type", out var type) ? type.GetString() ?? "" : "",
            Name = flow.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            Z = flow.TryGetProperty("z", out var z) ? z.GetString() ?? "" : "",
            X = flow.TryGetProperty("x", out var x) ? x.GetDouble() : 0,
            Y = flow.TryGetProperty("y", out var y) ? y.GetDouble() : 0,
        };

        // Check if config node (no z property or specific types)
        if (string.IsNullOrEmpty(node.Z))
        {
            _configNodes[node.Id] = node;
        }
        else
        {
            _nodes.Add(node);
        }
    }

    private void ImportLinks(List<JsonElement> flows)
    {
        foreach (var flow in flows)
        {
            if (flow.TryGetProperty("wires", out var wires) && wires.ValueKind == JsonValueKind.Array)
            {
                var sourceId = flow.GetProperty("id").GetString() ?? "";
                int portIndex = 0;

                foreach (var port in wires.EnumerateArray())
                {
                    if (port.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var targetId in port.EnumerateArray())
                        {
                            var link = new NodeLink
                            {
                                Source = GetNode(sourceId),
                                SourcePort = portIndex,
                                Target = GetNode(targetId.GetString() ?? ""),
                            };

                            if (link.Source != null && link.Target != null)
                            {
                                _links.Add(link);
                            }
                        }
                    }
                    portIndex++;
                }
            }
        }
    }

    public FlowNode? GetNode(string id)
    {
        var node = _nodes.FirstOrDefault(n => n.Id == id);
        if (node != null) return node;

        return _configNodes.TryGetValue(id, out var configNode) ? configNode : null;
    }

    public List<FlowNode> GetNodes() => _nodes.ToList();
    public List<NodeLink> GetLinks() => _links.ToList();
    public List<FlowWorkspace> GetWorkspaces() => _workspaces.Values.ToList();
    public List<string> GetWorkspaceOrder() => _workspacesOrder.ToList();
    public List<NodeGroup> GetGroups() => _groups.ToList();
    public List<Junction> GetJunctions() => _junctions.ToList();

    public bool IsDirty() => _dirty;
    public void SetDirty(bool dirty) => _dirty = dirty;

    public void AddNodeSet(NodeSetInfo nodeSet)
    {
        _nodeList.Add(nodeSet);
    }

    public NodeSetInfo? GetNodeSet(string id)
    {
        return _nodeList.FirstOrDefault(n => n.Id == id);
    }
}

// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/plugins.js
// ============================================================

/// <summary>
/// Plugin management - translated from RED.plugins
/// </summary>
public class EditorPlugins
{
    private readonly List<PluginInfo> _plugins = new();

    public async Task InitAsync()
    {
        await Task.CompletedTask;
    }

    public void SetPluginList(List<PluginInfo> plugins)
    {
        _plugins.Clear();
        _plugins.AddRange(plugins);
    }

    public void AddPlugin(PluginInfo plugin)
    {
        _plugins.Add(plugin);
    }

    public List<PluginInfo> GetPlugins() => _plugins.ToList();
}

// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/i18n.js
// ============================================================

/// <summary>
/// Internationalization - translated from RED.i18n
/// </summary>
public class EditorI18n
{
    private readonly Dictionary<string, Dictionary<string, string>> _catalogs = new();
    private string _currentLanguage = "en-US";

    public async Task InitAsync(string apiRootUrl)
    {
        _currentLanguage = DetectLanguage();
        await Task.CompletedTask;
    }

    public string DetectLanguage()
    {
        return System.Globalization.CultureInfo.CurrentUICulture.Name;
    }

    public async Task LoadPluginCatalogsAsync()
    {
        // Load plugin message catalogs
        await Task.CompletedTask;
    }

    public async Task LoadNodeCatalogsAsync()
    {
        // Load node message catalogs
        await Task.CompletedTask;
    }

    public string Translate(string key, object? data = null)
    {
        // Simple translation lookup
        var parts = key.Split(':');
        var ns = parts.Length > 1 ? parts[0] : "default";
        var msgKey = parts.Length > 1 ? parts[1] : parts[0];

        if (_catalogs.TryGetValue(ns, out var catalog) && catalog.TryGetValue(msgKey, out var msg))
        {
            return InterpolateMessage(msg, data);
        }

        // Return key as fallback
        return InterpolateMessage(key, data);
    }

    private string InterpolateMessage(string msg, object? data)
    {
        if (data == null) return msg;

        var result = msg;
        var props = data.GetType().GetProperties();

        foreach (var prop in props)
        {
            var value = prop.GetValue(data)?.ToString() ?? "";
            result = result.Replace("{{" + prop.Name + "}}", value);
            result = result.Replace("{" + prop.Name + "}", value);
        }

        return result;
    }
}

// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/ui/workspaces.js
// ============================================================

/// <summary>
/// Workspace management - translated from RED.workspaces
/// </summary>
public class EditorWorkspaces
{
    private string _activeWorkspace = "";
    private int _workspaceIndex = 0;
    private readonly List<string> _workspaceOrder = new();

    public int Count => _workspaceOrder.Count;

    public void Show(string id, bool force = false)
    {
        _activeWorkspace = id;
    }

    public string Active() => _activeWorkspace;

    public List<string> GetWorkspaceOrder() => _workspaceOrder.ToList();
}

// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/ui/menu.js (partial)
// ============================================================

/// <summary>
/// Menu state - translated from RED.menu
/// </summary>
public class EditorMenu
{
    public bool IsOpen { get; private set; }

    public void Toggle()
    {
        IsOpen = !IsOpen;
    }

    public void Open()
    {
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
    }
}

// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/ui/deploy.js
// ============================================================

/// <summary>
/// Deploy functionality - translated from RED.deploy
/// </summary>
public class EditorDeploy
{
    public event Func<Task>? OnDeploy;

    public async Task DeployAsync()
    {
        if (OnDeploy != null)
        {
            await OnDeploy.Invoke();
        }
    }
}

// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/ui/event-log.js
// ============================================================

/// <summary>
/// Event log - translated from RED.eventLog
/// </summary>
public class EditorEventLog
{
    private readonly List<EventLogEntry> _entries = new();

    public void Log(string id, object? payload)
    {
        _entries.Add(new EventLogEntry
        {
            Id = id,
            Payload = payload,
            Timestamp = DateTime.UtcNow
        });
    }

    public List<EventLogEntry> GetEntries() => _entries.ToList();
}

// ============================================================
// Data models
// ============================================================

public class ThemeSettings
{
    public ThemeHeader? Header { get; set; }
    public List<string>? Themes { get; set; }
}

public class ThemeHeader
{
    public string? Url { get; set; }
    public string? Image { get; set; }
    public string? Title { get; set; }
}

public class FlowsResponse
{
    [JsonPropertyName("rev")]
    public string? Rev { get; set; }

    [JsonPropertyName("flows")]
    public List<JsonElement>? Flows { get; set; }
}

public class PluginInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("plugins")]
    public List<PluginEntry>? Plugins { get; set; }
}

public class PluginEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class NodeSetInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("types")]
    public List<string>? Types { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("local")]
    public bool Local { get; set; }

    [JsonPropertyName("module")]
    public string? Module { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

public class NodeDefinition
{
    public string Type { get; set; } = "";
    public string Category { get; set; } = "";
    public int Inputs { get; set; }
    public int Outputs { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public string? Label { get; set; }
    public Dictionary<string, object?> Defaults { get; set; } = new();
}

public class FlowNode
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string Z { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public int Inputs { get; set; }
    public int Outputs { get; set; }
    public NodeStatus? Status { get; set; }
    public bool DirtyStatus { get; set; }
    public bool Dirty { get; set; }
    public Dictionary<string, object?> Properties { get; set; } = new();
}

public class FlowWorkspace
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "tab";
    public string Label { get; set; } = "";
    public bool Disabled { get; set; }
    public string Info { get; set; } = "";
}

public class Subflow
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "subflow";
    public string Name { get; set; } = "";
}

public class NodeGroup
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "group";
    public string Name { get; set; } = "";
    public string Z { get; set; } = "";
}

public class Junction
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "junction";
    public string Z { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
}

public class NodeLink
{
    public FlowNode? Source { get; set; }
    public int SourcePort { get; set; }
    public FlowNode? Target { get; set; }
}

public class NodeStatus
{
    [JsonPropertyName("fill")]
    public string? Fill { get; set; }

    [JsonPropertyName("shape")]
    public string? Shape { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class NotificationMessage
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("timeout")]
    public int? Timeout { get; set; }
}

public class EventLogEntry
{
    public string Id { get; set; } = "";
    public object? Payload { get; set; }
    public DateTime Timestamp { get; set; }
}
