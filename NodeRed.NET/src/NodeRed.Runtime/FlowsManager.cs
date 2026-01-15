// ============================================================
// SOURCE: packages/node_modules/@node-red/runtime/lib/flows/index.js
// LINES: 1-877
// ============================================================
// ORIGINAL CODE:
// ------------------------------------------------------------
// var activeFlows = {};
// var started = false;
// var state = 'stop'
//
// function init(runtime) { ... }
// function loadFlows() { ... }
// function load(forceStart) { ... }
// function setFlows(_config, _credentials, type, muteLog, forceStart, user) { ... }
// function getNode(id) { ... }
// function eachNode(cb) { ... }
// function getFlows() { ... }
// async function start(type, diff, muteLog, isDeploy) { ... }
// function stop(type, diff, muteLog, isDeploy) { ... }
// function addFlow(flow, user) { ... }
// function getFlow(id) { ... }
// async function updateFlow(id, newFlow, user) { ... }
// async function removeFlow(id, user) { ... }
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
using System.Text.Json;
using System.Threading.Tasks;
using NodeRed.Util;

namespace NodeRed.Runtime
{
    /// <summary>
    /// Flow state enumeration.
    /// </summary>
    public enum FlowState
    {
        Stop,
        Start,
        Safe
    }

    /// <summary>
    /// Flow configuration parsed from the raw config.
    /// </summary>
    public class FlowConfiguration
    {
        public Dictionary<string, Dictionary<string, object?>> AllNodes { get; set; } = new();
        public Dictionary<string, FlowDefinition> Flows { get; set; } = new();
        public Dictionary<string, SubflowDefinition> Subflows { get; set; } = new();
        public Dictionary<string, Dictionary<string, object?>> Configs { get; set; } = new();
        public List<string> MissingTypes { get; set; } = new();
    }

    /// <summary>
    /// Flow definition within the configuration.
    /// </summary>
    public class FlowDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string? Label { get; set; }
        public bool Disabled { get; set; }
        public string? Info { get; set; }
        public List<Dictionary<string, object?>>? Env { get; set; }
        public Dictionary<string, Dictionary<string, object?>> Nodes { get; set; } = new();
        public Dictionary<string, Dictionary<string, object?>> Configs { get; set; } = new();
        public Dictionary<string, Dictionary<string, object?>> Groups { get; set; } = new();
    }

    /// <summary>
    /// Subflow definition.
    /// </summary>
    public class SubflowDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public Dictionary<string, Dictionary<string, object?>> Nodes { get; set; } = new();
        public Dictionary<string, Dictionary<string, object?>> Configs { get; set; } = new();
        public Dictionary<string, Dictionary<string, object?>> Groups { get; set; } = new();
        public List<string> Instances { get; set; } = new();
    }

    /// <summary>
    /// Active configuration with revision.
    /// </summary>
    public class ActiveConfig
    {
        public List<Dictionary<string, object?>> Flows { get; set; } = new();
        public string? Rev { get; set; }
    }

    /// <summary>
    /// Diff result between two flow configurations.
    /// </summary>
    public class FlowDiff
    {
        public List<string> Added { get; set; } = new();
        public List<string> Changed { get; set; } = new();
        public List<string> Removed { get; set; } = new();
        public List<string> Rewired { get; set; } = new();
        public List<string> Linked { get; set; } = new();
        public List<string> FlowChanged { get; set; } = new();
        public bool GlobalConfigChanged { get; set; }
    }

    /// <summary>
    /// Flow input for adding/updating flows.
    /// </summary>
    public class FlowInput
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
        public bool? Disabled { get; set; }
        public string? Info { get; set; }
        public List<Dictionary<string, object?>>? Env { get; set; }
        public List<Dictionary<string, object?>> Nodes { get; set; } = new();
        public List<Dictionary<string, object?>>? Configs { get; set; }
        public Dictionary<string, object?>? Credentials { get; set; }
        public List<SubflowDefinition>? Subflows { get; set; }
    }

    /// <summary>
    /// Flow result for getFlow.
    /// </summary>
    public class FlowResult
    {
        public string Id { get; set; } = string.Empty;
        public string? Label { get; set; }
        public bool? Disabled { get; set; }
        public string? Info { get; set; }
        public List<Dictionary<string, object?>>? Env { get; set; }
        public List<Dictionary<string, object?>>? Nodes { get; set; }
        public List<Dictionary<string, object?>>? Configs { get; set; }
        public List<SubflowDefinition>? Subflows { get; set; }
    }

    /// <summary>
    /// Flows manager for Node-RED runtime.
    /// Manages flow lifecycle including loading, starting, stopping, and updating.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/runtime/lib/flows/index.js
    /// </remarks>
    public class FlowsManager
    {
        private Settings? _settings;
        private Storage? _storage;
        private ActiveConfig? _activeConfig;
        private FlowConfiguration? _activeFlowConfig;
        private readonly Dictionary<string, Flow> _activeFlows = new();
        private bool _started;
        private FlowState _state = FlowState.Stop;
        private bool _credentialsPendingReset;
        private readonly Dictionary<string, string> _activeNodesToFlow = new();
        private bool _typeEventRegistered;

        /// <summary>
        /// Gets whether flows are started.
        /// </summary>
        public bool Started => _started;

        /// <summary>
        /// Gets the current state.
        /// </summary>
        public FlowState State => _state;

        /// <summary>
        /// Initialize the flows manager.
        /// </summary>
        /// <param name="settings">Runtime settings.</param>
        /// <param name="storage">Storage instance.</param>
        public void Init(Settings settings, Storage storage)
        {
            if (_started)
            {
                throw new InvalidOperationException("Cannot init without a stop");
            }

            _settings = settings;
            _storage = storage;
            _started = false;
            _state = FlowState.Stop;

            if (!_typeEventRegistered)
            {
                Events.Instance.On("type-registered", OnTypeRegistered);
                _typeEventRegistered = true;
            }
        }

        private void OnTypeRegistered(object? sender, EventArgs e)
        {
            if (e is Events.NodeRedEventArgs args && args.Data is string type)
            {
                if (_activeFlowConfig != null && _activeFlowConfig.MissingTypes.Count > 0)
                {
                    var index = _activeFlowConfig.MissingTypes.IndexOf(type);
                    if (index != -1)
                    {
                        Log.Info(I18n._("nodes.flows.registered-missing", new { type }));
                        _activeFlowConfig.MissingTypes.RemoveAt(index);
                        if (_activeFlowConfig.MissingTypes.Count == 0 && _started)
                        {
                            Events.Instance.Emit("runtime-event", new { id = "runtime-state", retain = true });
                            _ = StartAsync();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Load flows from storage.
        /// </summary>
        /// <param name="forceStart">Force start even if in safe mode.</param>
        /// <returns>A task that completes when flows are loaded.</returns>
        public async Task<string?> LoadAsync(bool forceStart = false)
        {
            if (forceStart && _settings != null && _settings.HasProperty("safeMode"))
            {
                // Force reload from API - disable safeMode
                // Note: In full implementation, would need to delete the safeMode setting
            }

            return await SetFlowsAsync(null, null, "load", false, forceStart, null);
        }

        /// <summary>
        /// Get current active configuration.
        /// </summary>
        /// <returns>The active configuration.</returns>
        public ActiveConfig? GetFlows()
        {
            return _activeConfig;
        }

        /// <summary>
        /// Set flows configuration.
        /// </summary>
        /// <param name="config">New node array configuration.</param>
        /// <param name="credentials">New credentials configuration (optional).</param>
        /// <param name="type">Type of deployment: full, nodes, flows, load.</param>
        /// <param name="muteLog">Don't emit standard log messages.</param>
        /// <param name="forceStart">Force start.</param>
        /// <param name="user">User performing the change.</param>
        /// <returns>The flow revision.</returns>
        public async Task<string?> SetFlowsAsync(
            List<Dictionary<string, object?>>? config,
            Dictionary<string, object?>? credentials,
            string type = "full",
            bool muteLog = false,
            bool forceStart = false,
            string? user = null)
        {
            string? flowRevision = null;
            bool isLoad = type == "load";

            if (isLoad)
            {
                if (_storage == null)
                {
                    throw new InvalidOperationException("Storage not initialized");
                }

                var loadedConfig = await _storage.GetFlowsAsync();
                config = DeepClone(loadedConfig.Flows);
                _activeFlowConfig = ParseConfig(config);
                flowRevision = loadedConfig.Rev;
                type = "full";

                Log.Debug($"loaded flow revision: {flowRevision}");
            }
            else if (config != null)
            {
                config = DeepClone(config);
                _activeFlowConfig = ParseConfig(config);
            }

            _activeConfig = new ActiveConfig
            {
                Flows = config ?? new List<Dictionary<string, object?>>(),
                Rev = flowRevision
            };

            if (forceStart || _started)
            {
                await StopAsync(type, null, muteLog, true);

                if (!isLoad)
                {
                    Log.Info(I18n._("nodes.flows.updated-flows"));
                }

                await StartAsync(type, null, muteLog, true);

                Events.Instance.Emit("runtime-event", new
                {
                    id = "runtime-deploy",
                    payload = new { revision = flowRevision },
                    retain = true
                });

                return flowRevision;
            }
            else
            {
                if (!isLoad)
                {
                    Log.Info(I18n._("nodes.flows.updated-flows"));
                }

                Events.Instance.Emit("runtime-event", new
                {
                    id = "runtime-deploy",
                    payload = new { revision = flowRevision },
                    retain = true
                });

                return flowRevision;
            }
        }

        /// <summary>
        /// Get a node by ID.
        /// </summary>
        /// <param name="id">The node ID.</param>
        /// <returns>The node, or null if not found.</returns>
        public object? GetNode(string id)
        {
            if (_activeNodesToFlow.TryGetValue(id, out var flowId) && _activeFlows.TryGetValue(flowId, out var flow))
            {
                return flow.GetNode(id, true);
            }

            foreach (var kvp in _activeFlows)
            {
                var node = kvp.Value.GetNode(id, true);
                if (node != null)
                {
                    return node;
                }
            }

            return null;
        }

        /// <summary>
        /// Iterate over each node in the active configuration.
        /// </summary>
        /// <param name="callback">Callback for each node.</param>
        public void EachNode(Action<Dictionary<string, object?>> callback)
        {
            if (_activeFlowConfig == null) return;

            foreach (var node in _activeFlowConfig.AllNodes.Values)
            {
                callback(node);
            }
        }

        /// <summary>
        /// Start flows.
        /// </summary>
        /// <param name="type">Type of start: full, nodes, flows.</param>
        /// <param name="diff">Diff for partial starts.</param>
        /// <param name="muteLog">Mute log messages.</param>
        /// <param name="isDeploy">Whether this is a deploy operation.</param>
        public async Task StartAsync(string type = "full", FlowDiff? diff = null, bool muteLog = false, bool isDeploy = false)
        {
            if (diff?.GlobalConfigChanged == true)
            {
                type = "full";
            }

            _started = true;
            _state = FlowState.Start;

            // Check for missing types
            if (_activeFlowConfig?.MissingTypes.Count > 0)
            {
                Log.Info(I18n._("nodes.flows.missing-types"));
                foreach (var nodeType in _activeFlowConfig.MissingTypes)
                {
                    Log.Info($" - {nodeType}");
                }

                Events.Instance.Emit("runtime-event", new
                {
                    id = "runtime-state",
                    payload = new
                    {
                        state = "stop",
                        error = "missing-types",
                        type = "warning",
                        text = "notification.warnings.missing-types",
                        types = _activeFlowConfig.MissingTypes
                    },
                    retain = true
                });
                return;
            }

            // Check safe mode
            if (_settings?.HasProperty("safeMode") == true)
            {
                Log.Info("*****************************************************************");
                Log.Info(I18n._("nodes.flows.safe-mode"));
                Log.Info("*****************************************************************");
                _state = FlowState.Safe;

                Events.Instance.Emit("runtime-event", new
                {
                    id = "runtime-state",
                    payload = new
                    {
                        state = "safe",
                        error = "safe-mode",
                        type = "warning",
                        text = "notification.warnings.safe-mode"
                    },
                    retain = true
                });
                return;
            }

            if (!muteLog)
            {
                if (type != "full")
                {
                    Log.Info(I18n._($"nodes.flows.starting-modified-{type}"));
                }
                else
                {
                    Log.Info(I18n._("nodes.flows.starting-flows"));
                }
            }

            Events.Instance.Emit("flows:starting", new { config = _activeConfig, type, diff });

            // Start flows (simplified - full implementation would create Flow instances)
            if (type == "full")
            {
                // Create global flow if not exists
                if (!_activeFlows.ContainsKey("global") && _activeFlowConfig != null)
                {
                    Log.Debug("red/nodes/flows.start : starting flow : global");
                    _activeFlows["global"] = new Flow("global", _activeFlowConfig);
                }

                // Create individual flows
                if (_activeFlowConfig?.Flows != null)
                {
                    foreach (var flowDef in _activeFlowConfig.Flows.Values)
                    {
                        if (!flowDef.Disabled && !_activeFlows.ContainsKey(flowDef.Id))
                        {
                            _activeFlows[flowDef.Id] = new Flow(flowDef.Id, _activeFlowConfig);
                            Log.Debug($"red/nodes/flows.start : starting flow : {flowDef.Id}");
                        }
                    }
                }
            }

            // Start each flow
            foreach (var flow in _activeFlows.Values)
            {
                try
                {
                    await flow.StartAsync(diff);

                    // Map nodes to flows
                    var activeNodes = flow.GetActiveNodes();
                    foreach (var nodeId in activeNodes.Keys)
                    {
                        _activeNodesToFlow[nodeId] = flow.Id;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }
            }

            Events.Instance.Emit("flows:started", new { config = _activeConfig, type, diff });
            Events.Instance.Emit("nodes-started"); // Deprecated

            if (!_credentialsPendingReset)
            {
                Events.Instance.Emit("runtime-event", new
                {
                    id = "runtime-state",
                    payload = new { state = "start", deploy = isDeploy },
                    retain = true
                });
            }
            else
            {
                _credentialsPendingReset = false;
            }

            if (!muteLog)
            {
                if (type != "full")
                {
                    Log.Info(I18n._($"nodes.flows.started-modified-{type}"));
                }
                else
                {
                    Log.Info(I18n._("nodes.flows.started-flows"));
                }
            }
        }

        /// <summary>
        /// Stop flows.
        /// </summary>
        /// <param name="type">Type of stop: full, nodes, flows.</param>
        /// <param name="diff">Diff for partial stops.</param>
        /// <param name="muteLog">Mute log messages.</param>
        /// <param name="isDeploy">Whether this is a deploy operation.</param>
        public async Task StopAsync(string type = "full", FlowDiff? diff = null, bool muteLog = false, bool isDeploy = false)
        {
            if (!_started)
            {
                return;
            }

            diff ??= new FlowDiff();

            if (!muteLog)
            {
                if (type != "full")
                {
                    Log.Info(I18n._($"nodes.flows.stopping-modified-{type}"));
                }
                else
                {
                    Log.Info(I18n._("nodes.flows.stopping-flows"));
                }
            }

            if (diff.GlobalConfigChanged)
            {
                type = "full";
            }

            _started = false;
            _state = FlowState.Stop;

            Events.Instance.Emit("flows:stopping", new { config = _activeConfig, type, diff });

            // Stop global flow last
            var flowIds = _activeFlows.Keys.ToList();
            var globalIndex = flowIds.IndexOf("global");
            if (globalIndex != -1)
            {
                flowIds.RemoveAt(globalIndex);
                flowIds.Add("global");
            }

            var stopTasks = new List<Task>();
            foreach (var id in flowIds)
            {
                if (_activeFlows.TryGetValue(id, out var flow))
                {
                    Log.Debug($"red/nodes/flows.stop : stopping flow : {id}");
                    stopTasks.Add(flow.StopAsync(null, diff.Removed));

                    if (type == "full" || diff.FlowChanged.Contains(id) || diff.Removed.Contains(id))
                    {
                        _activeFlows.Remove(id);
                    }
                }
            }

            await Task.WhenAll(stopTasks);

            // Clean up node to flow mapping
            var nodesToRemove = _activeNodesToFlow
                .Where(kvp => !_activeFlows.ContainsKey(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var nodeId in nodesToRemove)
            {
                _activeNodesToFlow.Remove(nodeId);
            }

            if (!muteLog)
            {
                if (type != "full")
                {
                    Log.Info(I18n._($"nodes.flows.stopped-modified-{type}"));
                }
                else
                {
                    Log.Info(I18n._("nodes.flows.stopped-flows"));
                }
            }

            Events.Instance.Emit("flows:stopped", new { config = _activeConfig, type, diff });
            Events.Instance.Emit("runtime-event", new
            {
                id = "runtime-state",
                payload = new { state = "stop", deploy = isDeploy },
                retain = true
            });
            Events.Instance.Emit("nodes-stopped"); // Deprecated
        }

        /// <summary>
        /// Add a new flow.
        /// </summary>
        /// <param name="flow">The flow to add.</param>
        /// <param name="user">User performing the action.</param>
        /// <returns>The new flow ID.</returns>
        public async Task<string> AddFlowAsync(FlowInput flow, string? user)
        {
            if (flow.Nodes == null)
            {
                throw new ArgumentException("missing nodes property");
            }

            flow.Id = Util.Util.GenerateId();

            var tabNode = new Dictionary<string, object?>
            {
                { "type", "tab" },
                { "label", flow.Label },
                { "id", flow.Id }
            };

            if (flow.Info != null) tabNode["info"] = flow.Info;
            if (flow.Disabled != null) tabNode["disabled"] = flow.Disabled;
            if (flow.Env != null) tabNode["env"] = flow.Env;

            var nodes = new List<Dictionary<string, object?>> { tabNode };

            foreach (var node in flow.Nodes)
            {
                if (_activeFlowConfig?.AllNodes.ContainsKey(node["id"]?.ToString() ?? "") == true)
                {
                    throw new InvalidOperationException("duplicate id");
                }

                var nodeType = node.TryGetValue("type", out var t) ? t?.ToString() : null;
                if (nodeType == "tab" || nodeType == "subflow")
                {
                    throw new InvalidOperationException($"invalid node type: {nodeType}");
                }

                node["z"] = flow.Id;
                nodes.Add(node);
            }

            if (flow.Configs != null)
            {
                foreach (var node in flow.Configs)
                {
                    node["z"] = flow.Id;
                    nodes.Add(node);
                }
            }

            var newConfig = DeepClone(_activeConfig?.Flows ?? new List<Dictionary<string, object?>>());
            newConfig.AddRange(nodes);

            await SetFlowsAsync(newConfig, null, "flows", true, false, user);

            Log.Info(I18n._("nodes.flows.added-flow", new { label = $"{flow.Label ?? ""} [{flow.Id}]" }));

            return flow.Id;
        }

        /// <summary>
        /// Get a flow by ID.
        /// </summary>
        /// <param name="id">The flow ID.</param>
        /// <returns>The flow result, or null if not found.</returns>
        public FlowResult? GetFlow(string id)
        {
            FlowDefinition? flow = null;

            if (id == "global")
            {
                // Return global config - simplified
                return new FlowResult { Id = id };
            }
            else if (_activeFlowConfig?.Flows.TryGetValue(id, out flow) != true)
            {
                return null;
            }

            var result = new FlowResult
            {
                Id = id,
                Label = flow.Label,
                Disabled = flow.Disabled,
                Info = flow.Info,
                Env = flow.Env,
                Nodes = new List<Dictionary<string, object?>>()
            };

            // Add nodes
            foreach (var node in flow.Nodes.Values)
            {
                var nodeCopy = DeepClone(node);
                nodeCopy.Remove("credentials");
                result.Nodes.Add(nodeCopy);
            }

            // Add groups
            foreach (var group in flow.Groups.Values)
            {
                var groupCopy = DeepClone(group);
                groupCopy.Remove("credentials");
                result.Nodes.Add(groupCopy);
            }

            // Add configs
            if (flow.Configs.Count > 0)
            {
                result.Configs = flow.Configs.Values
                    .Select(c =>
                    {
                        var copy = DeepClone(c);
                        copy.Remove("credentials");
                        return copy;
                    })
                    .ToList();
            }

            return result;
        }

        /// <summary>
        /// Update a flow.
        /// </summary>
        /// <param name="id">The flow ID.</param>
        /// <param name="newFlow">The new flow data.</param>
        /// <param name="user">User performing the action.</param>
        public async Task UpdateFlowAsync(string id, FlowInput newFlow, string? user)
        {
            string? label = id;

            if (id != "global")
            {
                if (_activeFlowConfig?.Flows.TryGetValue(id, out var existingFlow) != true)
                {
                    throw new KeyNotFoundException($"Flow {id} not found");
                }
                label = existingFlow.Label;
            }

            var newConfig = DeepClone(_activeConfig?.Flows ?? new List<Dictionary<string, object?>>());

            if (id != "global")
            {
                newConfig = newConfig.Where(n =>
                {
                    var z = n.TryGetValue("z", out var zVal) ? zVal?.ToString() : null;
                    var nodeId = n.TryGetValue("id", out var idVal) ? idVal?.ToString() : null;
                    return z != id && nodeId != id;
                }).ToList();

                var tabNode = new Dictionary<string, object?>
                {
                    { "type", "tab" },
                    { "label", newFlow.Label },
                    { "id", id }
                };

                if (newFlow.Info != null) tabNode["info"] = newFlow.Info;
                if (newFlow.Disabled != null) tabNode["disabled"] = newFlow.Disabled;
                if (newFlow.Env != null) tabNode["env"] = newFlow.Env;
                if (newFlow.Credentials != null) tabNode["credentials"] = newFlow.Credentials;

                var nodes = new List<Dictionary<string, object?>> { tabNode };
                nodes.AddRange(newFlow.Nodes ?? new List<Dictionary<string, object?>>());
                nodes.AddRange(newFlow.Configs ?? new List<Dictionary<string, object?>>());

                foreach (var node in nodes)
                {
                    var nodeType = node.TryGetValue("type", out var t) ? t?.ToString() : null;
                    if (nodeType != "tab")
                    {
                        node["z"] = id;
                    }
                }

                newConfig.AddRange(nodes);
            }

            await SetFlowsAsync(newConfig, null, "flows", true, false, user);

            Log.Info(I18n._("nodes.flows.updated-flow", new { label = $"{label ?? ""} [{id}]" }));
        }

        /// <summary>
        /// Remove a flow.
        /// </summary>
        /// <param name="id">The flow ID.</param>
        /// <param name="user">User performing the action.</param>
        public async Task RemoveFlowAsync(string id, string? user)
        {
            if (id == "global")
            {
                throw new InvalidOperationException("not allowed to remove global");
            }

            if (_activeFlowConfig?.Flows.TryGetValue(id, out var flow) != true)
            {
                throw new KeyNotFoundException($"Flow {id} not found");
            }

            var newConfig = DeepClone(_activeConfig?.Flows ?? new List<Dictionary<string, object?>>())
                .Where(n =>
                {
                    var z = n.TryGetValue("z", out var zVal) ? zVal?.ToString() : null;
                    var nodeId = n.TryGetValue("id", out var idVal) ? idVal?.ToString() : null;
                    return z != id && nodeId != id;
                })
                .ToList();

            await SetFlowsAsync(newConfig, null, "flows", true, false, user);

            Log.Info(I18n._("nodes.flows.removed-flow", new { label = $"{flow.Label ?? ""} [{flow.Id}]" }));
        }

        /// <summary>
        /// Check if delivery mode is async.
        /// </summary>
        /// <returns>True if async delivery mode.</returns>
        public bool IsDeliveryModeAsync()
        {
            return _settings == null || !_settings.HasProperty("runtimeSyncDelivery");
        }

        #region Helper Methods

        private static FlowConfiguration ParseConfig(List<Dictionary<string, object?>>? config)
        {
            var result = new FlowConfiguration();

            if (config == null) return result;

            foreach (var node in config)
            {
                var id = node.TryGetValue("id", out var idVal) ? idVal?.ToString() ?? "" : "";
                var type = node.TryGetValue("type", out var typeVal) ? typeVal?.ToString() : null;

                result.AllNodes[id] = node;

                if (type == "tab")
                {
                    var flowDef = new FlowDefinition
                    {
                        Id = id,
                        Label = node.TryGetValue("label", out var l) ? l?.ToString() : null,
                        Disabled = node.TryGetValue("disabled", out var d) && d is bool b && b,
                        Info = node.TryGetValue("info", out var i) ? i?.ToString() : null
                    };
                    result.Flows[id] = flowDef;
                }
                else if (type == "subflow")
                {
                    var subflow = new SubflowDefinition
                    {
                        Id = id,
                        Name = node.TryGetValue("name", out var n) ? n?.ToString() : null
                    };
                    result.Subflows[id] = subflow;
                }
            }

            // Second pass: assign nodes to flows
            foreach (var node in config)
            {
                var id = node.TryGetValue("id", out var idVal) ? idVal?.ToString() ?? "" : "";
                var type = node.TryGetValue("type", out var typeVal) ? typeVal?.ToString() : null;
                var z = node.TryGetValue("z", out var zVal) ? zVal?.ToString() : null;

                if (type == "tab" || type == "subflow") continue;

                if (z != null && result.Flows.TryGetValue(z, out var flow))
                {
                    if (type == "group")
                    {
                        flow.Groups[id] = node;
                    }
                    else if (node.TryGetValue("_users", out _))
                    {
                        flow.Configs[id] = node;
                    }
                    else
                    {
                        flow.Nodes[id] = node;
                    }
                }
                else if (z == null)
                {
                    // Global config node
                    result.Configs[id] = node;
                }
            }

            return result;
        }

        private static List<Dictionary<string, object?>> DeepClone(List<Dictionary<string, object?>> source)
        {
            var json = JsonSerializer.Serialize(source);
            return JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(json)
                ?? new List<Dictionary<string, object?>>();
        }

        private static Dictionary<string, object?> DeepClone(Dictionary<string, object?> source)
        {
            var json = JsonSerializer.Serialize(source);
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
                ?? new Dictionary<string, object?>();
        }

        #endregion
    }

    /// <summary>
    /// Simplified Flow class representing a single flow instance.
    /// </summary>
    public class Flow
    {
        public string Id { get; }
        private readonly FlowConfiguration _config;
        private readonly Dictionary<string, object> _activeNodes = new();

        public Flow(string id, FlowConfiguration config)
        {
            Id = id;
            _config = config;
        }

        public async Task StartAsync(FlowDiff? diff)
        {
            // Simplified flow start - full implementation would instantiate nodes
            await Task.CompletedTask;
        }

        public async Task StopAsync(List<string>? stopList, List<string>? removedList)
        {
            _activeNodes.Clear();
            await Task.CompletedTask;
        }

        public object? GetNode(string id, bool cancelBubble = false)
        {
            return _activeNodes.TryGetValue(id, out var node) ? node : null;
        }

        public Dictionary<string, object> GetActiveNodes()
        {
            return _activeNodes;
        }

        public void Update(FlowConfiguration global, FlowDefinition? flow)
        {
            // Update flow configuration
        }
    }
}
// ------------------------------------------------------------
// MAPPING NOTES:
// - activeFlows object → _activeFlows Dictionary
// - started variable → Started property
// - state variable → State property (FlowState enum)
// - init function → Init method
// - loadFlows function → Integrated into LoadAsync
// - load function → LoadAsync method
// - setFlows function → SetFlowsAsync method
// - getNode function → GetNode method
// - eachNode function → EachNode method
// - getFlows function → GetFlows method
// - start function → StartAsync method
// - stop function → StopAsync method
// - addFlow function → AddFlowAsync method
// - getFlow function → GetFlow method
// - updateFlow function → UpdateFlowAsync method
// - removeFlow function → RemoveFlowAsync method
// - jsonClone (rfdc) → DeepClone via JSON serialization
// - Promise → Task
// - events.on/emit → Events.Instance.On/Emit
// ============================================================
