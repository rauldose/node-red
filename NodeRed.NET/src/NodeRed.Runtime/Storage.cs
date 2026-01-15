// ============================================================
// SOURCE: packages/node_modules/@node-red/runtime/lib/storage/index.js
// LINES: 1-232
// ============================================================
// ORIGINAL CODE:
// ------------------------------------------------------------
// var storageModuleInterface = {
//     init: async function(_runtime) { ... },
//     getFlows: async function() { ... },
//     saveFlows: async function(config, user) { ... },
//     saveCredentials: async function(credentials) { ... },
//     getSettings: async function() { ... },
//     saveSettings: async function(settings) { ... },
//     getSessions: async function() { ... },
//     saveSessions: async function(sessions) { ... },
//     getLibraryEntry: async function(type, path) { ... },
//     saveLibraryEntry: async function(type, path, meta, body) { ... },
//     getAllFlows: async function() { ... },
//     getFlow: function(fn) { ... },
//     saveFlow: function(fn, data) { ... }
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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NodeRed.Util;

namespace NodeRed.Runtime
{
    /// <summary>
    /// Flow configuration with credentials.
    /// </summary>
    public class FlowConfig
    {
        public List<Dictionary<string, object?>> Flows { get; set; } = new();
        public Dictionary<string, object?> Credentials { get; set; } = new();
        public bool CredentialsDirty { get; set; }
        public string? Rev { get; set; }
    }

    /// <summary>
    /// Library entry metadata.
    /// </summary>
    public class LibraryEntryMeta
    {
        public string? Fn { get; set; }
        public string? Name { get; set; }
    }

    /// <summary>
    /// Interface for storage module implementations.
    /// </summary>
    public interface IStorageModule
    {
        Task InitAsync(Settings settings, object runtime);
        Task<List<Dictionary<string, object?>>> GetFlowsAsync();
        Task SaveFlowsAsync(List<Dictionary<string, object?>> flows, string? user);
        Task<Dictionary<string, object?>> GetCredentialsAsync();
        Task SaveCredentialsAsync(Dictionary<string, object?> credentials);
        Task<Dictionary<string, object?>?> GetSettingsAsync();
        Task SaveSettingsAsync(Dictionary<string, object?> settings);
        Task<Dictionary<string, object?>?> GetSessionsAsync();
        Task SaveSessionsAsync(Dictionary<string, object?> sessions);
        Task<object> GetLibraryEntryAsync(string type, string path);
        Task SaveLibraryEntryAsync(string type, string path, LibraryEntryMeta meta, object body);

        bool HasSettings { get; }
        bool HasSessions { get; }
    }

    /// <summary>
    /// Forbidden operation exception.
    /// </summary>
    public class ForbiddenException : Exception
    {
        public string Code { get; } = "forbidden";
        public ForbiddenException() : base("Forbidden") { }
    }

    /// <summary>
    /// Storage interface for Node-RED runtime.
    /// Provides abstraction layer over actual storage implementations.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/runtime/lib/storage/index.js
    /// </remarks>
    public class Storage : ISettingsStorage
    {
        private object? _runtime;
        private IStorageModule? _storageModule;
        private bool _settingsAvailable;
        private bool _sessionsAvailable;
        private readonly SemaphoreSlim _settingsSaveMutex = new(1, 1);
        private object? _libraryFlowsCachedResult;

        /// <summary>
        /// Initialize storage with runtime.
        /// </summary>
        /// <param name="runtime">The runtime instance.</param>
        /// <param name="storageModule">The storage module implementation.</param>
        /// <returns>A task that completes when initialized.</returns>
        public async Task InitAsync(object runtime, IStorageModule storageModule)
        {
            _runtime = runtime;
            _storageModule = storageModule;

            _settingsAvailable = storageModule.HasSettings;
            _sessionsAvailable = storageModule.HasSessions;

            // Get settings from runtime - for now, create a default settings instance
            var settings = new Settings();
            await storageModule.InitAsync(settings, runtime);
        }

        /// <summary>
        /// Get flows with credentials and revision hash.
        /// </summary>
        /// <returns>The flow configuration.</returns>
        public async Task<FlowConfig> GetFlowsAsync()
        {
            if (_storageModule == null)
            {
                throw new InvalidOperationException("Storage not initialized");
            }

            var flows = await _storageModule.GetFlowsAsync();
            var creds = await _storageModule.GetCredentialsAsync();

            var result = new FlowConfig
            {
                Flows = flows,
                Credentials = creds
            };

            // Generate SHA256 hash of flows as revision
            var flowsJson = JsonSerializer.Serialize(result.Flows);
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(flowsJson));
            result.Rev = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            return result;
        }

        /// <summary>
        /// Save flows configuration.
        /// </summary>
        /// <param name="config">The flow configuration to save.</param>
        /// <param name="user">The user performing the save.</param>
        /// <returns>The new revision hash.</returns>
        public async Task<string> SaveFlowsAsync(FlowConfig config, string? user)
        {
            if (_storageModule == null)
            {
                throw new InvalidOperationException("Storage not initialized");
            }

            if (config.CredentialsDirty)
            {
                await _storageModule.SaveCredentialsAsync(config.Credentials);
            }

            await _storageModule.SaveFlowsAsync(config.Flows, user);

            // Generate revision hash
            var flowsJson = JsonSerializer.Serialize(config.Flows);
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(flowsJson));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Save credentials.
        /// </summary>
        /// <param name="credentials">The credentials to save.</param>
        public async Task SaveCredentialsAsync(Dictionary<string, object?> credentials)
        {
            if (_storageModule == null)
            {
                throw new InvalidOperationException("Storage not initialized");
            }

            await _storageModule.SaveCredentialsAsync(credentials);
        }

        /// <summary>
        /// Get settings from storage.
        /// </summary>
        /// <returns>The settings dictionary, or null if not available.</returns>
        public async Task<Dictionary<string, object?>?> GetSettingsAsync()
        {
            if (!_settingsAvailable || _storageModule == null)
            {
                return null;
            }

            return await _storageModule.GetSettingsAsync();
        }

        /// <summary>
        /// Save settings to storage with mutex lock.
        /// </summary>
        /// <param name="settings">The settings to save.</param>
        public async Task SaveSettingsAsync(Dictionary<string, object?> settings)
        {
            if (!_settingsAvailable || _storageModule == null)
            {
                return;
            }

            await _settingsSaveMutex.WaitAsync();
            try
            {
                await _storageModule.SaveSettingsAsync(settings);
            }
            finally
            {
                _settingsSaveMutex.Release();
            }
        }

        /// <summary>
        /// Get sessions from storage.
        /// </summary>
        /// <returns>The sessions dictionary, or null if not available.</returns>
        public async Task<Dictionary<string, object?>?> GetSessionsAsync()
        {
            if (!_sessionsAvailable || _storageModule == null)
            {
                return null;
            }

            return await _storageModule.GetSessionsAsync();
        }

        /// <summary>
        /// Save sessions to storage.
        /// </summary>
        /// <param name="sessions">The sessions to save.</param>
        public async Task SaveSessionsAsync(Dictionary<string, object?> sessions)
        {
            if (!_sessionsAvailable || _storageModule == null)
            {
                return;
            }

            await _storageModule.SaveSessionsAsync(sessions);
        }

        /// <summary>
        /// Get a library entry.
        /// </summary>
        /// <param name="type">The entry type.</param>
        /// <param name="path">The entry path.</param>
        /// <returns>The library entry.</returns>
        public async Task<object> GetLibraryEntryAsync(string type, string path)
        {
            if (IsMalicious(path))
            {
                throw new ForbiddenException();
            }

            if (_storageModule == null)
            {
                throw new InvalidOperationException("Storage not initialized");
            }

            return await _storageModule.GetLibraryEntryAsync(type, path);
        }

        /// <summary>
        /// Save a library entry.
        /// </summary>
        /// <param name="type">The entry type.</param>
        /// <param name="path">The entry path.</param>
        /// <param name="meta">The entry metadata.</param>
        /// <param name="body">The entry body.</param>
        public async Task SaveLibraryEntryAsync(string type, string path, LibraryEntryMeta meta, object body)
        {
            if (IsMalicious(path))
            {
                throw new ForbiddenException();
            }

            if (_storageModule == null)
            {
                throw new InvalidOperationException("Storage not initialized");
            }

            await _storageModule.SaveLibraryEntryAsync(type, path, meta, body);
        }

        /// <summary>
        /// Check if a path contains path traversal attempts.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>True if the path is malicious.</returns>
        private static bool IsMalicious(string path)
        {
            return path.Contains("../") || path.Contains("..\\");
        }

        #region Deprecated Functions

        /// <summary>
        /// Get a flow by filename (deprecated).
        /// </summary>
        /// <param name="fn">The filename.</param>
        /// <returns>The flow data.</returns>
        public async Task<object> GetFlowAsync(string fn)
        {
            if (IsMalicious(fn))
            {
                throw new ForbiddenException();
            }

            return await GetLibraryEntryAsync("flows", fn);
        }

        /// <summary>
        /// Save a flow (deprecated).
        /// </summary>
        /// <param name="fn">The filename.</param>
        /// <param name="data">The flow data.</param>
        public async Task SaveFlowAsync(string fn, object data)
        {
            if (IsMalicious(fn))
            {
                throw new ForbiddenException();
            }

            _libraryFlowsCachedResult = null;
            await SaveLibraryEntryAsync("flows", fn, new LibraryEntryMeta(), data);
        }

        #endregion
    }
}
// ------------------------------------------------------------
// MAPPING NOTES:
// - storageModuleInterface.init → InitAsync method
// - storageModuleInterface.getFlows → GetFlowsAsync method
// - storageModuleInterface.saveFlows → SaveFlowsAsync method
// - storageModuleInterface.saveCredentials → SaveCredentialsAsync method
// - storageModuleInterface.getSettings → GetSettingsAsync method
// - storageModuleInterface.saveSettings → SaveSettingsAsync method
// - storageModuleInterface.getSessions → GetSessionsAsync method
// - storageModuleInterface.saveSessions → SaveSessionsAsync method
// - storageModuleInterface.getLibraryEntry → GetLibraryEntryAsync method
// - storageModuleInterface.saveLibraryEntry → SaveLibraryEntryAsync method
// - storageModuleInterface.getFlow → GetFlowAsync method (deprecated)
// - storageModuleInterface.saveFlow → SaveFlowAsync method (deprecated)
// - is_malicious function → IsMalicious static method
// - Mutex from async-mutex → SemaphoreSlim
// - crypto.createHash('sha256') → SHA256.Create()
// - Promise → Task
// ============================================================
