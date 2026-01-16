// ============================================================
// SOURCE: packages/node_modules/@node-red/registry/lib/registry.js
// LINES: 1-779
// ============================================================
// ORIGINAL CODE:
// ------------------------------------------------------------
// var nodeConfigCache = {};
// var moduleConfigs = {};
// var nodeList = [];
// var nodeConstructors = {};
// var nodeTypeToId = {};
// var moduleNodes = {};
//
// function init(_settings, _loader) { ... }
// function load() { ... }
// function filterNodeInfo(n) { ... }
// function addModule(module) { ... }
// function removeNode(id) { ... }
// function removeModule(name, skipSave) { ... }
// function getNodeInfo(typeOrId) { ... }
// function getNodeList(filter) { ... }
// function getModuleList() { ... }
// function getModuleInfo(module) { ... }
// function registerNodeConstructor(nodeSet, type, constructor, options) { ... }
// function getAllNodeConfigs(lang) { ... }
// function enableNodeSet(typeOrId) { ... }
// function disableNodeSet(typeOrId) { ... }
// ... etc
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
using System.Linq;
using System.Threading.Tasks;
using NodeRed.Util;

namespace NodeRed.Registry
{
    /// <summary>
    /// Filtered node information returned by registry queries.
    /// </summary>
    public class NodeInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Module { get; set; }
        public List<string> Types { get; set; } = new();
        public bool Enabled { get; set; } = true;
        public bool Local { get; set; }
        public bool User { get; set; }
        public bool? Loaded { get; set; }
        public string? Version { get; set; }
        public string? PendingVersion { get; set; }
        public string? Err { get; set; }
        public List<PluginInfo>? Plugins { get; set; }
        public bool? Editor { get; set; }
        public bool? Runtime { get; set; }
    }

    /// <summary>
    /// Plugin information.
    /// </summary>
    public class PluginInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Module { get; set; }
    }

    /// <summary>
    /// Module configuration.
    /// </summary>
    public class ModuleConfig
    {
        public string Name { get; set; } = string.Empty;
        public string? Version { get; set; }
        public string? PendingVersion { get; set; }
        public bool Local { get; set; }
        public bool User { get; set; }
        public string? Path { get; set; }
        public Dictionary<string, NodeConfig> Nodes { get; set; } = new();
        public Dictionary<string, PluginConfig>? Plugins { get; set; }
        public List<IconInfo>? Icons { get; set; }
        public ExamplesInfo? Examples { get; set; }
        public ResourcesInfo? Resources { get; set; }
        public List<string>? Dependencies { get; set; }
        public List<string>? UsedBy { get; set; }
        public string? Err { get; set; }
    }

    /// <summary>
    /// Node configuration within a module.
    /// </summary>
    public class NodeConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Module { get; set; }
        public string? File { get; set; }
        public string? Template { get; set; }
        public List<string> Types { get; set; } = new();
        public bool Enabled { get; set; } = true;
        public bool Loaded { get; set; }
        public string? Version { get; set; }
        public bool Local { get; set; }
        public bool User { get; set; }
        public string? Config { get; set; }
        public Dictionary<string, string>? Help { get; set; }
        public string? Namespace { get; set; }
        public string? Err { get; set; }
    }

    /// <summary>
    /// Plugin configuration within a module.
    /// </summary>
    public class PluginConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Module { get; set; }
        public string Type { get; set; } = "plugin";
        public string? File { get; set; }
        public string? Template { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Loaded { get; set; }
        public string? Version { get; set; }
        public bool Local { get; set; }
        public string? Config { get; set; }
        public string? Err { get; set; }
    }

    /// <summary>
    /// Icon path information.
    /// </summary>
    public class IconInfo
    {
        public string Path { get; set; } = string.Empty;
        public List<string> Icons { get; set; } = new();
    }

    /// <summary>
    /// Examples information.
    /// </summary>
    public class ExamplesInfo
    {
        public string Path { get; set; } = string.Empty;
    }

    /// <summary>
    /// Resources information.
    /// </summary>
    public class ResourcesInfo
    {
        public string Path { get; set; } = string.Empty;
    }

    /// <summary>
    /// Module information for getModuleInfo.
    /// </summary>
    public class ModuleInfo
    {
        public string Name { get; set; } = string.Empty;
        public string? Version { get; set; }
        public string? PendingVersion { get; set; }
        public bool Local { get; set; }
        public bool User { get; set; }
        public string? Path { get; set; }
        public List<NodeInfo> Nodes { get; set; } = new();
        public List<NodeInfo> Plugins { get; set; } = new();
        public List<string>? Dependencies { get; set; }
    }

    /// <summary>
    /// Node constructor options.
    /// </summary>
    public class NodeOptions
    {
        public List<string>? DynamicModuleList { get; set; }
    }

    /// <summary>
    /// Registry for node modules and types.
    /// Manages the registration, lookup, and lifecycle of node types and modules.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/registry/lib/registry.js
    /// </remarks>
    public class Registry
    {
        private Runtime.Settings? _settings;
        private Loader? _loader;

        private readonly Dictionary<string, string> _nodeConfigCache = new();
        private Dictionary<string, ModuleConfig> _moduleConfigs = new();
        private readonly List<string> _nodeList = new();
        private readonly Dictionary<string, Func<object, object>> _nodeConstructors = new();
        private readonly Dictionary<string, NodeOptions> _nodeOptions = new();
        private readonly Dictionary<string, object> _subflowModules = new();
        private readonly Dictionary<string, string> _nodeTypeToId = new();
        private readonly Dictionary<string, List<string>> _moduleNodes = new();
        private readonly Dictionary<string, List<string>> _iconPaths = new();
        private readonly Dictionary<string, string> _iconCache = new();

        /// <summary>
        /// Initialize the registry.
        /// </summary>
        /// <param name="settings">Runtime settings.</param>
        /// <param name="loader">Node loader.</param>
        public void Init(Runtime.Settings settings, Loader? loader = null)
        {
            _settings = settings;
            _loader = loader;
            Clear();
        }

        /// <summary>
        /// Load module configurations from settings.
        /// </summary>
        public void Load()
        {
            if (_settings?.Available() == true)
            {
                _moduleConfigs = LoadNodeConfigs();
            }
            else
            {
                _moduleConfigs = new Dictionary<string, ModuleConfig>();
            }
        }

        /// <summary>
        /// Clear all registry data.
        /// </summary>
        public void Clear()
        {
            _nodeConfigCache.Clear();
            _moduleConfigs.Clear();
            _nodeList.Clear();
            _nodeConstructors.Clear();
            _nodeOptions.Clear();
            _subflowModules.Clear();
            _nodeTypeToId.Clear();
            _iconPaths.Clear();
            _iconCache.Clear();
        }

        /// <summary>
        /// Filter node configuration to create NodeInfo.
        /// </summary>
        /// <param name="config">The node configuration.</param>
        /// <returns>Filtered node info.</returns>
        public static NodeInfo FilterNodeInfo(NodeConfig config)
        {
            var info = new NodeInfo
            {
                Id = config.Id ?? $"{config.Module}/{config.Name}",
                Name = config.Name,
                Types = config.Types,
                Enabled = config.Enabled,
                Local = config.Local,
                User = config.User
            };

            if (config.Module != null)
            {
                info.Module = config.Module;
            }

            if (config.Err != null)
            {
                info.Err = config.Err;
            }

            return info;
        }

        /// <summary>
        /// Get module name from set ID.
        /// </summary>
        /// <param name="id">The set ID (e.g., "module/node").</param>
        /// <returns>The module name.</returns>
        public static string GetModuleFromSetId(string id)
        {
            var parts = id.Split('/');
            return string.Join("/", parts.Take(parts.Length - 1));
        }

        /// <summary>
        /// Get node name from set ID.
        /// </summary>
        /// <param name="id">The set ID (e.g., "module/node").</param>
        /// <returns>The node name.</returns>
        public static string GetNodeFromSetId(string id)
        {
            var parts = id.Split('/');
            return parts[^1];
        }

        /// <summary>
        /// Add a module to the registry.
        /// </summary>
        /// <param name="module">The module configuration.</param>
        public void AddModule(ModuleConfig module)
        {
            _moduleNodes[module.Name] = new List<string>();
            _moduleConfigs[module.Name] = module;

            foreach (var setName in module.Nodes.Keys)
            {
                var set = module.Nodes[setName];

                if (set.Types == null || set.Types.Count == 0)
                {
                    set.Err = "Set has no types";
                }

                _moduleNodes[module.Name].Add(set.Name);
                _nodeList.Add(set.Id);

                if (set.Err == null)
                {
                    foreach (var type in set.Types)
                    {
                        if (_nodeTypeToId.ContainsKey(type))
                        {
                            set.Err = $"Type {type} already registered";
                            break;
                        }
                    }

                    if (set.Err == null)
                    {
                        foreach (var type in set.Types)
                        {
                            _nodeTypeToId[type] = set.Id;
                        }
                    }
                }
            }

            if (module.Icons != null)
            {
                if (!_iconPaths.ContainsKey(module.Name))
                {
                    _iconPaths[module.Name] = new List<string>();
                }

                foreach (var icon in module.Icons)
                {
                    _iconPaths[module.Name].Add(icon.Path);
                }
            }

            _nodeConfigCache.Clear();
        }

        /// <summary>
        /// Remove a node from the registry.
        /// </summary>
        /// <param name="id">The node ID.</param>
        /// <returns>The removed node info.</returns>
        public NodeInfo RemoveNode(string id)
        {
            var moduleName = GetModuleFromSetId(id);
            var nodeName = GetNodeFromSetId(id);

            if (!_moduleConfigs.TryGetValue(moduleName, out var module) ||
                !module.Nodes.TryGetValue(nodeName, out var config))
            {
                throw new ArgumentException($"Unrecognised id: {id}");
            }

            module.Nodes.Remove(nodeName);

            var index = _nodeList.IndexOf(id);
            if (index > -1)
            {
                _nodeList.RemoveAt(index);
            }

            foreach (var type in config.Types)
            {
                if (_nodeTypeToId.TryGetValue(type, out var typeId) && typeId == id)
                {
                    _subflowModules.Remove(type);
                    _nodeConstructors.Remove(type);
                    _nodeOptions.Remove(type);
                    _nodeTypeToId.Remove(type);
                }
            }

            config.Enabled = false;
            config.Loaded = false;
            _nodeConfigCache.Clear();

            return FilterNodeInfo(config);
        }

        /// <summary>
        /// Remove a module from the registry.
        /// </summary>
        /// <param name="name">The module name.</param>
        /// <param name="skipSave">Whether to skip saving the node list.</param>
        /// <returns>List of removed node info.</returns>
        public async Task<List<NodeInfo>> RemoveModuleAsync(string name, bool skipSave = false)
        {
            if (_settings?.Available() != true)
            {
                throw new InvalidOperationException("Settings unavailable");
            }

            var infoList = new List<NodeInfo>();

            if (!_moduleConfigs.TryGetValue(name, out var module))
            {
                throw new ArgumentException($"Unrecognised module: {name}");
            }

            if (!_moduleNodes.TryGetValue(name, out var nodes))
            {
                throw new ArgumentException($"Unrecognised module: {name}");
            }

            if (module.UsedBy != null && module.UsedBy.Count > 0)
            {
                // Module is used by others - just mark as not user-installed
                module.User = false;
                foreach (var nodeName in nodes)
                {
                    if (module.Nodes.TryGetValue(nodeName, out var node))
                    {
                        infoList.Add(FilterNodeInfo(node));
                    }
                }
            }
            else
            {
                // Remove dependencies if they are not user-installed
                if (module.Dependencies != null)
                {
                    foreach (var dep in module.Dependencies)
                    {
                        if (_moduleConfigs.TryGetValue(dep, out var depModule) && !depModule.User)
                        {
                            if (depModule.UsedBy != null)
                            {
                                depModule.UsedBy.Remove(name);
                                if (depModule.UsedBy.Count == 0)
                                {
                                    await RemoveModuleAsync(dep, true);
                                }
                            }
                        }
                    }
                }

                foreach (var nodeName in nodes)
                {
                    infoList.Add(RemoveNode($"{name}/{nodeName}"));
                }

                _moduleNodes.Remove(name);
                _moduleConfigs.Remove(name);
            }

            if (!skipSave)
            {
                await SaveNodeListAsync();
            }

            return infoList;
        }

        /// <summary>
        /// Get node info by type or ID.
        /// </summary>
        /// <param name="typeOrId">The node type or ID.</param>
        /// <returns>The node info, or null if not found.</returns>
        public NodeInfo? GetNodeInfo(string typeOrId)
        {
            var id = _nodeTypeToId.TryGetValue(typeOrId, out var mappedId) ? mappedId : typeOrId;

            if (string.IsNullOrEmpty(id)) return null;

            var moduleName = GetModuleFromSetId(id);

            if (!_moduleConfigs.TryGetValue(moduleName, out var module)) return null;

            var nodeName = GetNodeFromSetId(id);

            if (!module.Nodes.TryGetValue(nodeName, out var config)) return null;

            var info = FilterNodeInfo(config);

            if (config.Loaded)
            {
                info.Loaded = config.Loaded;
            }

            if (module.PendingVersion != null)
            {
                info.PendingVersion = module.PendingVersion;
            }

            info.Version = module.Version;

            return info;
        }

        /// <summary>
        /// Get full node info (including file path).
        /// </summary>
        /// <param name="typeOrId">The node type or ID.</param>
        /// <returns>The full node config, or null if not found.</returns>
        public NodeConfig? GetFullNodeInfo(string typeOrId)
        {
            var id = _nodeTypeToId.TryGetValue(typeOrId, out var mappedId) ? mappedId : typeOrId;

            if (string.IsNullOrEmpty(id)) return null;

            var moduleName = GetModuleFromSetId(id);

            if (!_moduleConfigs.TryGetValue(moduleName, out var module)) return null;

            var nodeName = GetNodeFromSetId(id);

            return module.Nodes.TryGetValue(nodeName, out var config) ? config : null;
        }

        /// <summary>
        /// Get list of all nodes.
        /// </summary>
        /// <param name="filter">Optional filter function.</param>
        /// <returns>List of node info.</returns>
        public List<NodeInfo> GetNodeList(Func<NodeConfig, bool>? filter = null)
        {
            var list = new List<NodeInfo>();

            foreach (var module in _moduleConfigs.Values)
            {
                // Skip non-user modules that are used by others
                if (!module.User && module.UsedBy != null && module.UsedBy.Count > 0)
                {
                    continue;
                }

                foreach (var node in module.Nodes.Values)
                {
                    if (filter == null || filter(node))
                    {
                        var info = FilterNodeInfo(node);
                        info.Version = module.Version;

                        if (module.PendingVersion != null)
                        {
                            info.PendingVersion = module.PendingVersion;
                        }

                        list.Add(info);
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Get all module configurations.
        /// </summary>
        /// <returns>Dictionary of module configurations.</returns>
        public Dictionary<string, ModuleConfig> GetModuleList()
        {
            return _moduleConfigs;
        }

        /// <summary>
        /// Get a specific module configuration.
        /// </summary>
        /// <param name="id">The module ID.</param>
        /// <returns>The module configuration, or null if not found.</returns>
        public ModuleConfig? GetModule(string id)
        {
            return _moduleConfigs.TryGetValue(id, out var module) ? module : null;
        }

        /// <summary>
        /// Get module info.
        /// </summary>
        /// <param name="moduleName">The module name.</param>
        /// <returns>The module info, or null if not found.</returns>
        public ModuleInfo? GetModuleInfo(string moduleName)
        {
            if (!_moduleNodes.TryGetValue(moduleName, out var nodes)) return null;

            var module = _moduleConfigs[moduleName];
            var info = new ModuleInfo
            {
                Name = moduleName,
                Version = module.Version,
                Local = module.Local,
                User = module.User,
                Path = module.Path,
                Dependencies = module.Dependencies
            };

            if (module.PendingVersion != null)
            {
                info.PendingVersion = module.PendingVersion;
            }

            foreach (var nodeName in nodes)
            {
                if (module.Nodes.TryGetValue(nodeName, out var nodeConfig))
                {
                    var nodeInfo = FilterNodeInfo(nodeConfig);
                    nodeInfo.Version = module.Version;
                    info.Nodes.Add(nodeInfo);
                }
            }

            if (module.Plugins != null)
            {
                foreach (var plugin in module.Plugins.Values)
                {
                    var pluginInfo = new NodeInfo
                    {
                        Id = plugin.Id,
                        Name = plugin.Name,
                        Module = plugin.Module,
                        Types = new List<string>(),
                        Enabled = plugin.Enabled,
                        Version = module.Version
                    };
                    info.Plugins.Add(pluginInfo);
                }
            }

            return info;
        }

        /// <summary>
        /// Register a node constructor.
        /// </summary>
        /// <param name="nodeSet">The node set ID.</param>
        /// <param name="type">The node type.</param>
        /// <param name="constructor">The constructor function.</param>
        /// <param name="options">Optional node options.</param>
        public void RegisterNodeConstructor(string nodeSet, string type, Func<object, object> constructor, NodeOptions? options = null)
        {
            if (_nodeConstructors.ContainsKey(type))
            {
                throw new InvalidOperationException($"{type} already registered");
            }

            var nodeSetInfo = GetFullNodeInfo(nodeSet);
            if (nodeSetInfo != null)
            {
                if (!nodeSetInfo.Types.Contains(type))
                {
                    nodeSetInfo.Types.Add(type);
                }
            }

            _nodeConstructors[type] = constructor;

            if (options != null)
            {
                _nodeOptions[type] = options;
            }

            Events.Instance.Emit("type-registered", type);
        }

        /// <summary>
        /// Get node constructor for a type.
        /// </summary>
        /// <param name="type">The node type.</param>
        /// <returns>The constructor, or null if not found/disabled.</returns>
        public object? GetNodeConstructor(string type)
        {
            if (!_nodeTypeToId.TryGetValue(type, out var id))
            {
                return _nodeConstructors.TryGetValue(type, out var ctor) ? ctor :
                       _subflowModules.TryGetValue(type, out var subflow) ? subflow : null;
            }

            var moduleName = GetModuleFromSetId(id);
            var nodeName = GetNodeFromSetId(id);

            if (!_moduleConfigs.TryGetValue(moduleName, out var module)) return null;
            if (!module.Nodes.TryGetValue(nodeName, out var config)) return null;

            if (!config.Enabled || config.Err != null) return null;

            return _nodeConstructors.TryGetValue(type, out var constructor) ? constructor :
                   _subflowModules.TryGetValue(type, out var sub) ? sub : null;
        }

        /// <summary>
        /// Get type ID.
        /// </summary>
        /// <param name="type">The type name.</param>
        /// <returns>The type ID, or null if not found.</returns>
        public string? GetTypeId(string type)
        {
            return _nodeTypeToId.TryGetValue(type, out var id) ? id : null;
        }

        /// <summary>
        /// Enable a node set.
        /// </summary>
        /// <param name="typeOrId">The type or ID.</param>
        /// <returns>The updated node info.</returns>
        public async Task<NodeInfo> EnableNodeSetAsync(string typeOrId)
        {
            if (_settings?.Available() != true)
            {
                throw new InvalidOperationException("Settings unavailable");
            }

            var id = _nodeTypeToId.TryGetValue(typeOrId, out var mappedId) ? mappedId : typeOrId;

            var moduleName = GetModuleFromSetId(id);
            var nodeName = GetNodeFromSetId(id);

            if (!_moduleConfigs.TryGetValue(moduleName, out var module) ||
                !module.Nodes.TryGetValue(nodeName, out var config))
            {
                throw new ArgumentException($"Unrecognised id: {typeOrId}");
            }

            config.Err = null;
            config.Enabled = true;
            _nodeConfigCache.Clear();

            _settings.EnableNodeSettings(config.Types);

            await SaveNodeListAsync();

            return FilterNodeInfo(config);
        }

        /// <summary>
        /// Disable a node set.
        /// </summary>
        /// <param name="typeOrId">The type or ID.</param>
        /// <returns>The updated node info.</returns>
        public async Task<NodeInfo> DisableNodeSetAsync(string typeOrId)
        {
            if (_settings?.Available() != true)
            {
                throw new InvalidOperationException("Settings unavailable");
            }

            var id = _nodeTypeToId.TryGetValue(typeOrId, out var mappedId) ? mappedId : typeOrId;

            var moduleName = GetModuleFromSetId(id);
            var nodeName = GetNodeFromSetId(id);

            if (!_moduleConfigs.TryGetValue(moduleName, out var module) ||
                !module.Nodes.TryGetValue(nodeName, out var config))
            {
                throw new ArgumentException($"Unrecognised id: {typeOrId}");
            }

            config.Enabled = false;
            _nodeConfigCache.Clear();

            _settings.DisableNodeSettings(config.Types);

            await SaveNodeListAsync();

            return FilterNodeInfo(config);
        }

        /// <summary>
        /// Set module pending updated version.
        /// </summary>
        /// <param name="moduleName">The module name.</param>
        /// <param name="version">The pending version.</param>
        /// <returns>The updated module info.</returns>
        public async Task<ModuleInfo?> SetModulePendingUpdatedAsync(string moduleName, string version)
        {
            if (_moduleConfigs.TryGetValue(moduleName, out var module))
            {
                module.PendingVersion = version;
                await SaveNodeListAsync();
            }

            return GetModuleInfo(moduleName);
        }

        /// <summary>
        /// Set user installed flag.
        /// </summary>
        /// <param name="moduleName">The module name.</param>
        /// <param name="userInstalled">Whether user installed.</param>
        /// <returns>The updated module info.</returns>
        public async Task<ModuleInfo?> SetUserInstalledAsync(string moduleName, bool userInstalled)
        {
            if (_moduleConfigs.TryGetValue(moduleName, out var module))
            {
                module.User = userInstalled;
                await SaveNodeListAsync();
            }

            return GetModuleInfo(moduleName);
        }

        /// <summary>
        /// Add module dependency.
        /// </summary>
        /// <param name="moduleName">The module name.</param>
        /// <param name="usedBy">The module using this dependency.</param>
        public void AddModuleDependency(string moduleName, string usedBy)
        {
            if (_moduleConfigs.TryGetValue(moduleName, out var module))
            {
                module.UsedBy ??= new List<string>();
                module.UsedBy.Add(usedBy);
            }
        }

        /// <summary>
        /// Get all node configs as HTML.
        /// </summary>
        /// <param name="lang">The language code.</param>
        /// <returns>Concatenated node configs.</returns>
        public string GetAllNodeConfigs(string lang = "en-US")
        {
            if (_nodeConfigCache.TryGetValue(lang, out var cached))
            {
                return cached;
            }

            var result = "";

            foreach (var id in _nodeList)
            {
                var moduleName = GetModuleFromSetId(id);
                var nodeName = GetNodeFromSetId(id);

                if (!_moduleConfigs.TryGetValue(moduleName, out var module)) continue;

                // Skip non-user modules used by others
                if (!module.User && module.UsedBy != null && module.UsedBy.Count > 0)
                {
                    continue;
                }

                if (!module.Nodes.TryGetValue(nodeName, out var config)) continue;

                if (config.Enabled && config.Err == null)
                {
                    result += $"\n<!-- --- [red-module:{id}] --- -->\n";
                    result += config.Config ?? "";

                    if (_loader != null && config.Help != null)
                    {
                        result += _loader.GetNodeHelp(config, lang) ?? "";
                    }
                }
            }

            _nodeConfigCache[lang] = result;
            return result;
        }

        /// <summary>
        /// Get a specific node config.
        /// </summary>
        /// <param name="id">The node ID.</param>
        /// <param name="lang">The language code.</param>
        /// <returns>The node config HTML, or null if not found.</returns>
        public string? GetNodeConfig(string id, string lang = "en-US")
        {
            var moduleName = GetModuleFromSetId(id);
            var nodeName = GetNodeFromSetId(id);

            if (!_moduleConfigs.TryGetValue(moduleName, out var module)) return null;
            if (!module.Nodes.TryGetValue(nodeName, out var config)) return null;

            var result = $"<!-- --- [red-module:{id}] --- -->\n{config.Config ?? ""}";

            if (_loader != null)
            {
                result += _loader.GetNodeHelp(config, lang) ?? "";
            }

            return result;
        }

        /// <summary>
        /// Get node icons.
        /// </summary>
        /// <returns>Dictionary of module icons.</returns>
        public Dictionary<string, List<string>> GetNodeIcons()
        {
            var iconList = new Dictionary<string, List<string>>();

            foreach (var module in _moduleConfigs.Values)
            {
                if (module.Icons != null)
                {
                    iconList[module.Name] = new List<string>();

                    foreach (var icon in module.Icons)
                    {
                        iconList[module.Name].AddRange(icon.Icons);
                    }
                }
            }

            return iconList;
        }

        /// <summary>
        /// Save node list to settings.
        /// </summary>
        /// <returns>A task that completes when saved.</returns>
        public async Task SaveNodeListAsync()
        {
            if (_settings == null)
            {
                throw new InvalidOperationException("Settings not initialized");
            }

            var moduleList = new Dictionary<string, object>();
            var hadPending = false;
            var hasPending = false;

            foreach (var module in _moduleConfigs.Values)
            {
                if (module.Nodes.Count == 0) continue;

                var moduleData = new Dictionary<string, object?>
                {
                    { "name", module.Name },
                    { "version", module.Version },
                    { "local", module.Local },
                    { "user", module.User },
                    { "nodes", new Dictionary<string, object>() }
                };

                if (module.PendingVersion != null)
                {
                    hadPending = true;
                    if (module.PendingVersion != module.Version)
                    {
                        moduleData["pending_version"] = module.PendingVersion;
                        hasPending = true;
                    }
                    else
                    {
                        module.PendingVersion = null;
                    }
                }

                var nodesDict = (Dictionary<string, object>)moduleData["nodes"]!;

                foreach (var node in module.Nodes.Values)
                {
                    var nodeData = new Dictionary<string, object?>
                    {
                        { "name", node.Name },
                        { "types", node.Types },
                        { "enabled", node.Enabled },
                        { "local", node.Local },
                        { "user", node.User },
                        { "file", node.File }
                    };

                    if (node.Module != null)
                    {
                        nodeData["module"] = node.Module;
                    }

                    nodesDict[node.Name] = nodeData;
                }

                moduleList[module.Name] = moduleData;
            }

            if (hadPending && !hasPending)
            {
                Events.Instance.Emit("runtime-event", new { id = "restart-required", retain = true });
            }

            await _settings.SetAsync("nodes", moduleList);
        }

        /// <summary>
        /// Load node configs from settings.
        /// </summary>
        /// <returns>Dictionary of module configurations.</returns>
        private Dictionary<string, ModuleConfig> LoadNodeConfigs()
        {
            var configs = _settings?.Get("nodes");

            if (configs == null)
            {
                return new Dictionary<string, ModuleConfig>();
            }

            // TODO: Implement config parsing from settings
            return new Dictionary<string, ModuleConfig>();
        }
    }

    /// <summary>
    /// Placeholder for the Loader class.
    /// </summary>
    public class Loader
    {
        private Runtime.Settings? _settings;

        /// <summary>
        /// Initialize the loader.
        /// </summary>
        /// <param name="settings">Runtime settings.</param>
        public void Init(Runtime.Settings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Load nodes.
        /// </summary>
        /// <param name="disableNodePathScan">Whether to disable node path scan.</param>
        /// <returns>A task that completes when loaded.</returns>
        public Task LoadAsync(bool disableNodePathScan = false)
        {
            Log.Info(I18n._("server.loading"));
            // TODO: Implement node loading
            return Task.CompletedTask;
        }

        /// <summary>
        /// Get node help content.
        /// </summary>
        /// <param name="node">The node config.</param>
        /// <param name="lang">The language code.</param>
        /// <returns>The help HTML, or null if not found.</returns>
        public string? GetNodeHelp(NodeConfig node, string lang)
        {
            if (node.Help == null) return null;

            if (node.Help.TryGetValue(lang, out var help))
            {
                return help;
            }

            // Try base language
            var langParts = lang.Split('-');
            if (langParts.Length == 2 && node.Help.TryGetValue(langParts[0], out var baseHelp))
            {
                return baseHelp;
            }

            // Fall back to default language
            if (node.Help.TryGetValue(I18n.DefaultLang, out var defaultHelp))
            {
                return defaultHelp;
            }

            return null;
        }
    }
}
// ------------------------------------------------------------
// MAPPING NOTES:
// - nodeConfigCache object → _nodeConfigCache Dictionary
// - moduleConfigs object → _moduleConfigs Dictionary
// - nodeList array → _nodeList List
// - nodeConstructors object → _nodeConstructors Dictionary
// - nodeTypeToId object → _nodeTypeToId Dictionary
// - moduleNodes object → _moduleNodes Dictionary
// - init function → Init method
// - load function → Load method
// - clear function → Clear method
// - filterNodeInfo function → FilterNodeInfo static method
// - getModuleFromSetId function → GetModuleFromSetId static method
// - getNodeFromSetId function → GetNodeFromSetId static method
// - addModule function → AddModule method
// - removeNode function → RemoveNode method
// - removeModule function → RemoveModuleAsync method
// - getNodeInfo function → GetNodeInfo method
// - getFullNodeInfo function → GetFullNodeInfo method
// - getNodeList function → GetNodeList method
// - getModuleList function → GetModuleList method
// - getModule function → GetModule method
// - getModuleInfo function → GetModuleInfo method
// - registerNodeConstructor function → RegisterNodeConstructor method
// - getNodeConstructor function → GetNodeConstructor method
// - getTypeId function → GetTypeId method
// - enableNodeSet function → EnableNodeSetAsync method
// - disableNodeSet function → DisableNodeSetAsync method
// - setModulePendingUpdated function → SetModulePendingUpdatedAsync method
// - setUserInstalled function → SetUserInstalledAsync method
// - addModuleDependency function → AddModuleDependency method
// - getAllNodeConfigs function → GetAllNodeConfigs method
// - getNodeConfig function → GetNodeConfig method
// - getNodeIcons function → GetNodeIcons method
// - saveNodeList function → SaveNodeListAsync method
// - loadNodeConfigs function → LoadNodeConfigs method
// ============================================================
