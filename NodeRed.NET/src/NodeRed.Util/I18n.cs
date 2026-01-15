// ============================================================
// SOURCE: packages/node_modules/@node-red/util/lib/i18n.js
// LINES: 1-256
// ============================================================
// ORIGINAL CODE:
// ------------------------------------------------------------
// var i18n = require("i18next");
// var defaultLang = "en-US";
// var resourceMap = {};
// var resourceCache = {};
//
// function registerMessageCatalog(namespace, dir, file) { ... }
// function registerMessageCatalogs(catalogs) { ... }
// function getCatalog(namespace, lang) { ... }
// function availableLanguages(namespace) { ... }
// function init(settings) { ... }
// obj['_'] = function() { return i18n.t.apply(i18n, arguments); }
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
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NodeRed.Util
{
    /// <summary>
    /// Resource map entry for a message catalog.
    /// </summary>
    public class ResourceMapEntry
    {
        public string BaseDir { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public List<string> Languages { get; set; } = new();
    }

    /// <summary>
    /// Message catalog registration info.
    /// </summary>
    public class MessageCatalog
    {
        public string Namespace { get; set; } = string.Empty;
        public string Dir { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
    }

    /// <summary>
    /// I18n settings for initialization.
    /// </summary>
    public class I18nSettings
    {
        public string? Lang { get; set; }
    }

    /// <summary>
    /// Internationalization utilities for Node-RED.
    /// Provides localization and message catalog functionality.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/util/lib/i18n.js
    /// </remarks>
    public static class I18n
    {
        /// <summary>
        /// Default language for the runtime.
        /// </summary>
        public static string DefaultLang { get; } = "en-US";

        /// <summary>
        /// Map of namespace to resource info.
        /// </summary>
        private static readonly Dictionary<string, ResourceMapEntry> _resourceMap = new();

        /// <summary>
        /// Cache of loaded resources by namespace and language.
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, Dictionary<string, object>>> _resourceCache = new();

        /// <summary>
        /// Current language setting.
        /// </summary>
        private static string _currentLang = DefaultLang;

        /// <summary>
        /// Lock object for thread safety.
        /// </summary>
        private static readonly object _lock = new();

        /// <summary>
        /// Initialization task.
        /// </summary>
        private static Task? _initTask;

        /// <summary>
        /// Initialize the i18n system.
        /// </summary>
        /// <param name="settings">The i18n settings.</param>
        public static void Init(I18nSettings? settings = null)
        {
            _initTask = Task.Run(() =>
            {
                var lang = settings?.Lang ?? GetCurrentLocale();
                if (!string.IsNullOrEmpty(lang))
                {
                    _currentLang = lang;
                }
            });
        }

        /// <summary>
        /// Register multiple message catalogs.
        /// </summary>
        /// <param name="catalogs">The catalogs to register.</param>
        /// <returns>A task that completes when all catalogs are registered.</returns>
        public static async Task RegisterMessageCatalogsAsync(IEnumerable<MessageCatalog> catalogs)
        {
            var tasks = new List<Task>();
            foreach (var catalog in catalogs)
            {
                tasks.Add(RegisterMessageCatalogAsync(catalog.Namespace, catalog.Dir, catalog.File));
            }
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Register a message catalog.
        /// </summary>
        /// <param name="namespace">The namespace for the catalog.</param>
        /// <param name="dir">The directory containing language folders.</param>
        /// <param name="file">The filename of the catalog JSON file.</param>
        /// <returns>A task that completes when the catalog is registered.</returns>
        public static async Task RegisterMessageCatalogAsync(string @namespace, string dir, string file)
        {
            if (_initTask != null)
            {
                await _initTask;
            }

            lock (_lock)
            {
                _resourceMap[@namespace] = new ResourceMapEntry
                {
                    BaseDir = dir,
                    File = file,
                    Languages = new List<string>()
                };
            }

            try
            {
                if (Directory.Exists(dir))
                {
                    var directories = Directory.GetDirectories(dir);
                    foreach (var langDir in directories)
                    {
                        var langName = Path.GetFileName(langDir);
                        var catalogPath = Path.Combine(langDir, file);
                        if (System.IO.File.Exists(catalogPath))
                        {
                            lock (_lock)
                            {
                                _resourceMap[@namespace].Languages.Add(langName);
                            }
                        }
                    }
                }

                // Pre-load the default language
                await ReadFileAsync(_currentLang, @namespace);
            }
            catch
            {
                // Ignore errors during registration - matches original behavior
            }
        }

        /// <summary>
        /// Read and cache a catalog file.
        /// </summary>
        private static async Task<Dictionary<string, object>?> ReadFileAsync(string lng, string ns)
        {
            // Validate language to prevent path traversal
            if (!Regex.IsMatch(lng, @"^[a-zA-Z\-]+$"))
            {
                throw new ArgumentException($"Invalid language: {lng}");
            }

            lock (_lock)
            {
                if (_resourceCache.TryGetValue(ns, out var nsCache) &&
                    nsCache.TryGetValue(lng, out var cached))
                {
                    return cached;
                }
            }

            if (!_resourceMap.TryGetValue(ns, out var resourceEntry))
            {
                throw new ArgumentException("Unrecognised namespace");
            }

            var filePath = Path.Combine(resourceEntry.BaseDir, lng, resourceEntry.File);

            if (!System.IO.File.Exists(filePath))
            {
                // Try base language (e.g., 'fr' instead of 'fr-FR')
                if (lng.Contains('-'))
                {
                    var baseLng = lng.Split('-')[0];
                    return await ReadFileAsync(baseLng, ns);
                }
                return null;
            }

            var content = await System.IO.File.ReadAllTextAsync(filePath);

            // Remove BOM if present
            if (content.StartsWith("\uFEFF"))
            {
                content = content.Substring(1);
            }

            var catalog = JsonSerializer.Deserialize<Dictionary<string, object>>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new Dictionary<string, object>();

            // Migrate v3 to v4 format (_plural → _other)
            catalog = MigrateMessageCatalogV3toV4(catalog);

            // Merge with base language if needed
            var baseLang = lng.Split('-')[0];
            if (baseLang != lng)
            {
                var baseCatalog = await ReadFileAsync(baseLang, ns);
                if (baseCatalog != null)
                {
                    MergeCatalog(baseCatalog, catalog);
                }
            }

            // Merge with default language if not the default
            if (lng != DefaultLang)
            {
                var defaultCatalog = await ReadFileAsync(DefaultLang, ns);
                if (defaultCatalog != null)
                {
                    MergeCatalog(defaultCatalog, catalog);
                }
            }

            lock (_lock)
            {
                if (!_resourceCache.TryGetValue(ns, out var nsCache))
                {
                    nsCache = new Dictionary<string, Dictionary<string, object>>();
                    _resourceCache[ns] = nsCache;
                }
                nsCache[lng] = catalog;
            }

            return catalog;
        }

        /// <summary>
        /// Migrate message catalog from i18next v3 to v4 format.
        /// </summary>
        private static Dictionary<string, object> MigrateMessageCatalogV3toV4(Dictionary<string, object> catalog)
        {
            var keysToProcess = new List<string>(catalog.Keys);
            foreach (var key in keysToProcess)
            {
                var value = catalog[key];

                if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
                {
                    var nested = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText())
                        ?? new Dictionary<string, object>();
                    catalog[key] = MigrateMessageCatalogV3toV4(nested);
                }
                else if (key.EndsWith("_plural"))
                {
                    var otherKey = key.Replace("_plural", "_other");
                    if (!catalog.ContainsKey(otherKey))
                    {
                        catalog[otherKey] = value;
                    }
                    catalog.Remove(key);
                }
            }

            return catalog;
        }

        /// <summary>
        /// Merge fallback catalog values into the target catalog.
        /// </summary>
        private static void MergeCatalog(Dictionary<string, object> fallback, Dictionary<string, object> catalog)
        {
            foreach (var kvp in fallback)
            {
                if (!catalog.ContainsKey(kvp.Key))
                {
                    catalog[kvp.Key] = kvp.Value;
                }
                else if (kvp.Value is Dictionary<string, object> fallbackNested &&
                         catalog[kvp.Key] is Dictionary<string, object> catalogNested)
                {
                    MergeCatalog(fallbackNested, catalogNested);
                }
                else if (kvp.Value is JsonElement fallbackElement &&
                         fallbackElement.ValueKind == JsonValueKind.Object &&
                         catalog[kvp.Key] is JsonElement catalogElement &&
                         catalogElement.ValueKind == JsonValueKind.Object)
                {
                    var fallbackDict = JsonSerializer.Deserialize<Dictionary<string, object>>(fallbackElement.GetRawText())!;
                    var catalogDict = JsonSerializer.Deserialize<Dictionary<string, object>>(catalogElement.GetRawText())!;
                    MergeCatalog(fallbackDict, catalogDict);
                    catalog[kvp.Key] = catalogDict;
                }
            }
        }

        /// <summary>
        /// Get the current locale from environment variables.
        /// </summary>
        private static string? GetCurrentLocale()
        {
            string[] envVars = { "LC_ALL", "LC_MESSAGES", "LANG" };
            foreach (var name in envVars)
            {
                var val = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrEmpty(val))
                {
                    return val.Substring(0, Math.Min(2, val.Length));
                }
            }

            // Try to get from current culture
            try
            {
                return CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a message catalog.
        /// </summary>
        /// <param name="namespace">The namespace of the catalog.</param>
        /// <param name="lang">The language code (defaults to default language).</param>
        /// <returns>The catalog dictionary, or null if not found.</returns>
        public static Dictionary<string, object>? GetCatalog(string @namespace, string? lang = null)
        {
            lang ??= DefaultLang;

            // Validate language
            if (!Regex.IsMatch(lang, @"^[a-zA-Z\-]+$"))
            {
                throw new ArgumentException($"Invalid language: {lang}");
            }

            lock (_lock)
            {
                if (_resourceCache.TryGetValue(@namespace, out var nsCache))
                {
                    if (nsCache.TryGetValue(lang, out var catalog))
                    {
                        return catalog;
                    }

                    // Try base language
                    if (lang.Contains('-'))
                    {
                        var baseLang = lang.Split('-')[0];
                        if (nsCache.TryGetValue(baseLang, out catalog))
                        {
                            return catalog;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a list of languages a given catalog is available in.
        /// </summary>
        /// <param name="namespace">The namespace of the catalog.</param>
        /// <returns>List of available language codes.</returns>
        public static List<string>? AvailableLanguages(string @namespace)
        {
            lock (_lock)
            {
                if (_resourceMap.TryGetValue(@namespace, out var entry))
                {
                    return new List<string>(entry.Languages);
                }
            }

            return null;
        }

        /// <summary>
        /// Perform a message catalog lookup (translation function).
        /// </summary>
        /// <param name="key">The message key in format "namespace:key.path".</param>
        /// <param name="options">Optional interpolation options.</param>
        /// <returns>The translated string, or the key if not found.</returns>
        public static string Translate(string key, Dictionary<string, object>? options = null)
        {
            // Parse key format: "namespace:key.path"
            var parts = key.Split(':', 2);
            var ns = parts.Length > 1 ? parts[0] : "runtime";
            var keyPath = parts.Length > 1 ? parts[1] : parts[0];

            var catalog = GetCatalog(ns, _currentLang);
            if (catalog == null)
            {
                return key;
            }

            // Navigate the key path
            var keyParts = keyPath.Split('.');
            object? current = catalog;

            foreach (var part in keyParts)
            {
                if (current is Dictionary<string, object> dict)
                {
                    if (!dict.TryGetValue(part, out current))
                    {
                        return key;
                    }
                }
                else if (current is JsonElement element && element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty(part, out var prop))
                    {
                        current = prop;
                    }
                    else
                    {
                        return key;
                    }
                }
                else
                {
                    return key;
                }
            }

            var result = current?.ToString() ?? key;

            // Perform interpolation if options provided
            if (options != null && result != null)
            {
                foreach (var kvp in options)
                {
                    result = result.Replace($"__{kvp.Key}__", kvp.Value?.ToString() ?? "");
                }
            }

            return result ?? key;
        }

        /// <summary>
        /// Shorthand for Translate method (matches original's _ function).
        /// </summary>
        public static string _(string key, Dictionary<string, object>? options = null)
        {
            return Translate(key, options);
        }

        /// <summary>
        /// Shorthand for Translate method with single parameter.
        /// </summary>
        public static string _(string key, object? singleParam)
        {
            if (singleParam == null)
            {
                return Translate(key);
            }

            var options = new Dictionary<string, object>();
            var props = singleParam.GetType().GetProperties();
            foreach (var prop in props)
            {
                var value = prop.GetValue(singleParam);
                if (value != null)
                {
                    options[prop.Name] = value;
                }
            }

            return Translate(key, options);
        }
    }
}
// ------------------------------------------------------------
// MAPPING NOTES:
// - i18next library → Custom implementation with Dictionary
// - defaultLang → DefaultLang property
// - resourceMap → _resourceMap Dictionary
// - resourceCache → _resourceCache Dictionary
// - registerMessageCatalog → RegisterMessageCatalogAsync
// - registerMessageCatalogs → RegisterMessageCatalogsAsync
// - getCatalog → GetCatalog
// - availableLanguages → AvailableLanguages
// - init → Init
// - _ function → Translate method and _ shorthand
// - fs.readdir → Directory.GetDirectories
// - fs.readFile → File.ReadAllTextAsync
// - JSON.parse → JsonSerializer.Deserialize
// - i18n.t → Custom Translate with interpolation
// - Interpolation prefix/suffix '__' preserved
// ============================================================
