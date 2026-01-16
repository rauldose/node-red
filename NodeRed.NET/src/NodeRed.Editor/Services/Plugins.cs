using System.Collections.Concurrent;
using System.Text.Json;

namespace NodeRed.Editor.Services;

/// <summary>
/// Translated from: @node-red/editor-client/src/js/plugins.js
/// Plugin management system for extending Node-RED functionality
/// </summary>
public class Plugins
{
    private readonly ConcurrentDictionary<string, PluginDefinition> _plugins = new();
    private readonly ConcurrentDictionary<string, List<PluginDefinition>> _pluginsByType = new();
    private readonly Events _events;

    public Plugins(Events events)
    {
        _events = events;
    }

    public class PluginDefinition
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string? Module { get; set; }
        public string? Name { get; set; }
        public object? Definition { get; set; }
        public bool Enabled { get; set; } = true;
        public Dictionary<string, object> Config { get; set; } = new();
    }

    /// <summary>
    /// Register a new plugin
    /// </summary>
    public void RegisterPlugin(PluginDefinition plugin)
    {
        if (string.IsNullOrEmpty(plugin.Id))
        {
            plugin.Id = Guid.NewGuid().ToString();
        }

        _plugins[plugin.Id] = plugin;

        if (!string.IsNullOrEmpty(plugin.Type))
        {
            if (!_pluginsByType.ContainsKey(plugin.Type))
            {
                _pluginsByType[plugin.Type] = new List<PluginDefinition>();
            }
            _pluginsByType[plugin.Type].Add(plugin);
        }

        _events.Emit("plugins:add", plugin);
    }

    /// <summary>
    /// Get a plugin by ID
    /// </summary>
    public PluginDefinition? GetPlugin(string id)
    {
        return _plugins.TryGetValue(id, out var plugin) ? plugin : null;
    }

    /// <summary>
    /// Get all plugins of a specific type
    /// </summary>
    public IEnumerable<PluginDefinition> GetPluginsByType(string type)
    {
        if (_pluginsByType.TryGetValue(type, out var plugins))
        {
            return plugins.Where(p => p.Enabled);
        }
        return Enumerable.Empty<PluginDefinition>();
    }

    /// <summary>
    /// Get all registered plugins
    /// </summary>
    public IEnumerable<PluginDefinition> GetAllPlugins()
    {
        return _plugins.Values;
    }

    /// <summary>
    /// Remove a plugin
    /// </summary>
    public bool RemovePlugin(string id)
    {
        if (_plugins.TryRemove(id, out var plugin))
        {
            if (!string.IsNullOrEmpty(plugin.Type) && 
                _pluginsByType.TryGetValue(plugin.Type, out var typeList))
            {
                typeList.RemoveAll(p => p.Id == id);
            }
            _events.Emit("plugins:remove", plugin);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Enable or disable a plugin
    /// </summary>
    public void SetPluginEnabled(string id, bool enabled)
    {
        if (_plugins.TryGetValue(id, out var plugin))
        {
            plugin.Enabled = enabled;
            _events.Emit("plugins:enabled", new { id, enabled });
        }
    }

    /// <summary>
    /// Get plugin configuration
    /// </summary>
    public T? GetPluginConfig<T>(string id, string key) where T : class
    {
        if (_plugins.TryGetValue(id, out var plugin) &&
            plugin.Config.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
                return typedValue;
            if (value is JsonElement element)
                return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        return null;
    }

    /// <summary>
    /// Set plugin configuration
    /// </summary>
    public void SetPluginConfig(string id, string key, object value)
    {
        if (_plugins.TryGetValue(id, out var plugin))
        {
            plugin.Config[key] = value;
            _events.Emit("plugins:config", new { id, key, value });
        }
    }
}
