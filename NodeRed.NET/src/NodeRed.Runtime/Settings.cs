// ============================================================
// SOURCE: packages/node_modules/@node-red/runtime/lib/settings.js
// LINES: 1-191
// ============================================================
// ORIGINAL CODE:
// ------------------------------------------------------------
// var localSettings = null;
// var globalSettings = null;
// var nodeSettings = null;
// var userSettings = null;
// var disableNodeSettings = null;
// var storage = null;
//
// var persistentSettings = {
//     init: function(settings) { ... },
//     load: function(_storage) { ... },
//     get: function(prop) { ... },
//     set: function(prop, value) { ... },
//     delete: function(prop) { ... },
//     available: function() { ... },
//     reset: function() { ... },
//     registerNodeSettings: function(type, opts) { ... },
//     exportNodeSettings: function(safeSettings) { ... },
//     enableNodeSettings: function(types) { ... },
//     disableNodeSettings: function(types) { ... },
//     getUserSettings: function(username) { ... },
//     setUserSettings: function(username, settings) { ... }
// }
// ------------------------------------------------------------
// TRANSLATION:
// ------------------------------------------------------------

// Copyright JS Foundation and other contributors, http://js.foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using NodeRed.Util;

namespace NodeRed.Runtime
{
    /// <summary>
    /// Node settings options for registration.
    /// </summary>
    public class NodeSettingsOption
    {
        public bool Exportable { get; set; }
        public object? Value { get; set; }
    }

    /// <summary>
    /// Storage interface for settings persistence.
    /// </summary>
    public interface ISettingsStorage
    {
        Task<Dictionary<string, object?>?> GetSettingsAsync();
        Task SaveSettingsAsync(Dictionary<string, object?> settings);
    }

    /// <summary>
    /// Persistent settings manager for Node-RED runtime.
    /// Manages local settings (from settings file), global settings (from storage),
    /// node-specific settings, and per-user settings.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/runtime/lib/settings.js
    /// </remarks>
    public class Settings
    {
        /// <summary>
        /// Settings provided in the runtime settings file.
        /// </summary>
        private Dictionary<string, object?>? _localSettings;

        /// <summary>
        /// Settings provided by storage (e.g., .config.json).
        /// </summary>
        private Dictionary<string, object?>? _globalSettings;

        /// <summary>
        /// Settings that node modules define as being available.
        /// </summary>
        private Dictionary<string, Dictionary<string, NodeSettingsOption>> _nodeSettings = new();

        /// <summary>
        /// Per-user settings (subset of globalSettings).
        /// </summary>
        private Dictionary<string, Dictionary<string, object?>?> _userSettings = new();

        /// <summary>
        /// Map of disabled node settings by type.
        /// </summary>
        private Dictionary<string, bool> _disableNodeSettings = new();

        /// <summary>
        /// Reference to storage module.
        /// </summary>
        private ISettingsStorage? _storage;

        /// <summary>
        /// Reserved property names that cannot be overwritten.
        /// </summary>
        private static readonly HashSet<string> ReservedProperties = new()
        {
            "load", "get", "set", "available", "reset"
        };

        /// <summary>
        /// Initialize settings with local settings.
        /// </summary>
        /// <param name="settings">The local settings dictionary.</param>
        public void Init(Dictionary<string, object?> settings)
        {
            _localSettings = new Dictionary<string, object?>(settings);
            _globalSettings = null;
            _nodeSettings = new Dictionary<string, Dictionary<string, NodeSettingsOption>>();
            _disableNodeSettings = new Dictionary<string, bool>();
        }

        /// <summary>
        /// Load global settings from storage.
        /// </summary>
        /// <param name="storage">The storage implementation.</param>
        /// <returns>A task that completes when settings are loaded.</returns>
        public async Task LoadAsync(ISettingsStorage storage)
        {
            _storage = storage;
            var settings = await storage.GetSettingsAsync();
            _globalSettings = settings ?? new Dictionary<string, object?>();

            if (_globalSettings.TryGetValue("users", out var usersObj) && usersObj is Dictionary<string, Dictionary<string, object?>?> users)
            {
                _userSettings = users;
            }
            else if (usersObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
            {
                _userSettings = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object?>?>>(jsonElement.GetRawText())
                    ?? new Dictionary<string, Dictionary<string, object?>?>();
            }
            else
            {
                _userSettings = new Dictionary<string, Dictionary<string, object?>?>();
            }
        }

        /// <summary>
        /// Get a settings property value.
        /// </summary>
        /// <param name="prop">The property name.</param>
        /// <returns>The property value, or null if not found.</returns>
        public object? Get(string prop)
        {
            if (prop == "users")
            {
                throw new InvalidOperationException("Do not access user settings directly. Use GetUserSettings");
            }

            if (_localSettings != null && _localSettings.TryGetValue(prop, out var localValue))
            {
                return CloneValue(localValue);
            }

            if (_globalSettings == null)
            {
                throw new InvalidOperationException(I18n._("settings.not-available"));
            }

            if (_globalSettings.TryGetValue(prop, out var globalValue))
            {
                return CloneValue(globalValue);
            }

            return null;
        }

        /// <summary>
        /// Set a settings property value.
        /// </summary>
        /// <param name="prop">The property name.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>A task that completes when the setting is saved.</returns>
        public async Task SetAsync(string prop, object? value)
        {
            if (prop == "users")
            {
                throw new InvalidOperationException("Do not access user settings directly. Use SetUserSettings");
            }

            if (_localSettings != null && _localSettings.ContainsKey(prop))
            {
                throw new InvalidOperationException(I18n._("settings.property-read-only", new { prop }));
            }

            if (_globalSettings == null)
            {
                throw new InvalidOperationException(I18n._("settings.not-available"));
            }

            var current = _globalSettings.TryGetValue(prop, out var v) ? v : null;
            _globalSettings[prop] = CloneValue(value);

            // Check if value actually changed
            if (!DeepEquals(current, value) && _storage != null)
            {
                await _storage.SaveSettingsAsync(CloneDictionary(_globalSettings));
            }
        }

        /// <summary>
        /// Delete a settings property.
        /// </summary>
        /// <param name="prop">The property name.</param>
        /// <returns>A task that completes when the setting is deleted.</returns>
        public async Task DeleteAsync(string prop)
        {
            if (_localSettings != null && _localSettings.ContainsKey(prop))
            {
                throw new InvalidOperationException(I18n._("settings.property-read-only", new { prop }));
            }

            if (_globalSettings == null)
            {
                throw new InvalidOperationException(I18n._("settings.not-available"));
            }

            if (_globalSettings.ContainsKey(prop))
            {
                _globalSettings.Remove(prop);
                if (_storage != null)
                {
                    await _storage.SaveSettingsAsync(CloneDictionary(_globalSettings));
                }
            }
        }

        /// <summary>
        /// Check if global settings are available.
        /// </summary>
        /// <returns>True if global settings have been loaded.</returns>
        public bool Available()
        {
            return _globalSettings != null;
        }

        /// <summary>
        /// Reset all settings to initial state.
        /// </summary>
        public void Reset()
        {
            _localSettings = null;
            _globalSettings = null;
            _userSettings = new Dictionary<string, Dictionary<string, object?>?>();
            _storage = null;
            _nodeSettings = new Dictionary<string, Dictionary<string, NodeSettingsOption>>();
            _disableNodeSettings = new Dictionary<string, bool>();
        }

        /// <summary>
        /// Register node-specific settings.
        /// </summary>
        /// <param name="type">The node type.</param>
        /// <param name="opts">The settings options.</param>
        public void RegisterNodeSettings(string type, Dictionary<string, NodeSettingsOption> opts)
        {
            var normalisedType = Util.Util.NormaliseNodeTypeName(type);

            foreach (var property in opts.Keys)
            {
                if (!property.StartsWith(normalisedType))
                {
                    throw new ArgumentException(
                        $"Registered invalid property name '{property}'. Properties for this node must start with '{normalisedType}'");
                }
            }

            _nodeSettings[type] = opts;
        }

        /// <summary>
        /// Export node settings to a safe settings dictionary.
        /// </summary>
        /// <param name="safeSettings">The dictionary to export settings into.</param>
        /// <returns>The updated safe settings dictionary.</returns>
        public Dictionary<string, object?> ExportNodeSettings(Dictionary<string, object?> safeSettings)
        {
            foreach (var type in _nodeSettings.Keys)
            {
                if (_disableNodeSettings.TryGetValue(type, out var disabled) && disabled)
                {
                    continue;
                }

                var nodeTypeSettings = _nodeSettings[type];
                foreach (var property in nodeTypeSettings.Keys)
                {
                    var setting = nodeTypeSettings[property];
                    if (setting.Exportable)
                    {
                        if (safeSettings.ContainsKey(property))
                        {
                            // Cannot overwrite existing setting
                            continue;
                        }

                        if (_localSettings != null && _localSettings.TryGetValue(property, out var localVal))
                        {
                            safeSettings[property] = localVal;
                        }
                        else if (setting.Value != null)
                        {
                            safeSettings[property] = setting.Value;
                        }
                    }
                }
            }

            return safeSettings;
        }

        /// <summary>
        /// Enable node settings for specified types.
        /// </summary>
        /// <param name="types">The types to enable.</param>
        public void EnableNodeSettings(IEnumerable<string> types)
        {
            foreach (var type in types)
            {
                _disableNodeSettings[type] = false;
            }
        }

        /// <summary>
        /// Disable node settings for specified types.
        /// </summary>
        /// <param name="types">The types to disable.</param>
        public void DisableNodeSettings(IEnumerable<string> types)
        {
            foreach (var type in types)
            {
                _disableNodeSettings[type] = true;
            }
        }

        /// <summary>
        /// Get user-specific settings.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <returns>The user's settings, or null if not found.</returns>
        public Dictionary<string, object?>? GetUserSettings(string username)
        {
            if (_userSettings.TryGetValue(username, out var settings))
            {
                return settings != null ? CloneDictionary(settings) : null;
            }
            return null;
        }

        /// <summary>
        /// Set user-specific settings.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="settings">The settings to save.</param>
        /// <returns>A task that completes when settings are saved.</returns>
        public async Task SetUserSettingsAsync(string username, Dictionary<string, object?> settings)
        {
            if (_globalSettings == null)
            {
                throw new InvalidOperationException(I18n._("settings.not-available"));
            }

            var current = _userSettings.TryGetValue(username, out var v) ? v : null;
            _userSettings[username] = settings;

            if (!DeepEquals(current, settings) && _storage != null)
            {
                _globalSettings["users"] = _userSettings;
                await _storage.SaveSettingsAsync(CloneDictionary(_globalSettings));
            }
        }

        /// <summary>
        /// Indexer for getting settings by property name.
        /// </summary>
        public object? this[string prop]
        {
            get
            {
                if (_localSettings != null && _localSettings.TryGetValue(prop, out var value))
                {
                    return value;
                }
                return null;
            }
        }

        /// <summary>
        /// Check if a property exists in local settings.
        /// </summary>
        public bool HasProperty(string prop)
        {
            return _localSettings != null && _localSettings.ContainsKey(prop);
        }

        #region Helper Methods

        private static object? CloneValue(object? value)
        {
            if (value == null) return null;
            if (value is string or int or long or double or float or bool or decimal) return value;

            // Deep clone via JSON serialization
            var json = JsonSerializer.Serialize(value);
            return JsonSerializer.Deserialize<object>(json);
        }

        private static Dictionary<string, object?> CloneDictionary(Dictionary<string, object?> dict)
        {
            var json = JsonSerializer.Serialize(dict);
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
                ?? new Dictionary<string, object?>();
        }

        private static bool DeepEquals(object? a, object? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            var jsonA = JsonSerializer.Serialize(a);
            var jsonB = JsonSerializer.Serialize(b);
            return jsonA == jsonB;
        }

        #endregion
    }
}
// ------------------------------------------------------------
// MAPPING NOTES:
// - localSettings variable → _localSettings field
// - globalSettings variable → _globalSettings field
// - nodeSettings variable → _nodeSettings field
// - userSettings variable → _userSettings field
// - disableNodeSettings variable → _disableNodeSettings field
// - storage variable → _storage field
// - persistentSettings.init → Init method
// - persistentSettings.load → LoadAsync method
// - persistentSettings.get → Get method
// - persistentSettings.set → SetAsync method
// - persistentSettings.delete → DeleteAsync method
// - persistentSettings.available → Available method
// - persistentSettings.reset → Reset method
// - persistentSettings.registerNodeSettings → RegisterNodeSettings method
// - persistentSettings.exportNodeSettings → ExportNodeSettings method
// - persistentSettings.enableNodeSettings → EnableNodeSettings method
// - persistentSettings.disableNodeSettings → DisableNodeSettings method
// - persistentSettings.getUserSettings → GetUserSettings method
// - persistentSettings.setUserSettings → SetUserSettingsAsync method
// - clone() library → JSON serialization deep clone
// - assert.deepEqual → DeepEquals helper method
// - Promise → Task
// ============================================================
