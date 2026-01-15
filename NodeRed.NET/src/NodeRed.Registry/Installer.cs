// ============================================================
// SOURCE: packages/node_modules/@node-red/registry/lib/installer.js
// LINES: 1-626
// ============================================================
// ORIGINAL CODE:
// ------------------------------------------------------------
// let installerEnabled = false;
// let settings;
// const moduleRe = /^(@[^/@]+?[/])?[^/@]+?$/;
//
// function init(_settings) { ... }
// async function installModule(module, version, url) { ... }
// function uninstallModule(module) { ... }
// async function checkPrereq() { ... }
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NodeRed.Util;

namespace NodeRed.Registry
{
    /// <summary>
    /// Module allow/deny rule.
    /// </summary>
    public class ModuleRule
    {
        public string Module { get; set; } = string.Empty;
        public string? Version { get; set; }
    }

    /// <summary>
    /// Module installation result.
    /// </summary>
    public class InstallResult
    {
        public string Name { get; set; } = string.Empty;
        public string? Version { get; set; }
        public List<NodeInfo> Nodes { get; set; } = new();
        public List<NodeInfo> Plugins { get; set; } = new();
    }

    /// <summary>
    /// Installer for Node-RED modules.
    /// Handles installation and uninstallation of node modules via NuGet/dotnet.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/registry/lib/installer.js
    /// Note: In .NET, we use NuGet instead of npm for package management.
    /// </remarks>
    public class Installer
    {
        private Runtime.Settings? _settings;
        private bool _installerEnabled;

        // Regex patterns from original
        private static readonly Regex ModuleRegex = new(@"^(@[^/@]+?[/])?[^/@]+?$");
        private static readonly Regex PkgUrlRegex = new(@"^(https?|git(\+https?|\+ssh|\+file)?):\/\/");
        private static readonly Regex LocalTgzRegex = new(@"^([a-zA-Z]:|\/).+\.nupkg$");

        // Allow/deny lists
        private List<ModuleRule> _installAllowList = new() { new ModuleRule { Module = "*" } };
        private List<ModuleRule> _installDenyList = new();
        private bool _installAllAllowed = true;
        private bool _installVersionRestricted;

        private bool _updateAllowed = true;
        private List<ModuleRule> _updateAllowList = new() { new ModuleRule { Module = "*" } };
        private List<ModuleRule> _updateDenyList = new();
        private bool _updateAllAllowed = true;

        // Active promise chain for sequential operations
        private Task _activePromise = Task.CompletedTask;

        /// <summary>
        /// Gets whether the installer is enabled.
        /// </summary>
        public bool InstallerEnabled => _installerEnabled;

        /// <summary>
        /// Initialize the installer.
        /// </summary>
        /// <param name="settings">Runtime settings.</param>
        public void Init(Runtime.Settings settings)
        {
            _settings = settings;

            // Reset to defaults
            _installAllowList = new List<ModuleRule> { new ModuleRule { Module = "*" } };
            _installDenyList = new List<ModuleRule>();
            _installAllAllowed = true;
            _installVersionRestricted = false;

            _updateAllowed = true;
            _updateAllowList = new List<ModuleRule> { new ModuleRule { Module = "*" } };
            _updateDenyList = new List<ModuleRule>();
            _updateAllAllowed = true;

            // TODO: Parse settings.externalModules.palette configuration
            // for allowList/denyList settings

            _installAllAllowed = _installDenyList.Count == 0;
            _updateAllAllowed = _updateAllowed && _updateDenyList.Count == 0;
        }

        /// <summary>
        /// Check prerequisites for the installer.
        /// </summary>
        /// <returns>A task that completes when checked.</returns>
        public async Task CheckPrereqAsync()
        {
            // Check if palette editing is disabled
            // TODO: Check settings.externalModules.palette.allowInstall

            // Check if dotnet CLI is available
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    _installerEnabled = true;
                    Log.Info("Palette installer enabled (dotnet CLI available)");
                }
                else
                {
                    Log.Info(I18n._("server.palette-editor.npm-not-found"));
                    _installerEnabled = false;
                }
            }
            catch (Exception)
            {
                Log.Info(I18n._("server.palette-editor.npm-not-found"));
                _installerEnabled = false;
            }
        }

        /// <summary>
        /// Install a module.
        /// </summary>
        /// <param name="module">The module name or path.</param>
        /// <param name="version">The version to install (optional).</param>
        /// <param name="url">URL to install from (optional).</param>
        /// <returns>The installation result.</returns>
        public async Task<InstallResult> InstallModuleAsync(string module, string? version = null, string? url = null)
        {
            if (_settings == null)
            {
                throw new InvalidOperationException("Installer not initialized");
            }

            module = module ?? "";
            var installName = module;
            var isRegistryPackage = true;
            var isUpgrade = false;

            // Validate module name/URL
            if (!string.IsNullOrEmpty(url))
            {
                if (PkgUrlRegex.IsMatch(url) || LocalTgzRegex.IsMatch(url))
                {
                    installName = url;
                    isRegistryPackage = false;
                }
                else
                {
                    Log.Warn(I18n._("server.install.install-failed-url", new { name = module, url }));
                    var e = new InvalidOperationException("Invalid url");
                    throw e;
                }
            }
            else if (ModuleRegex.IsMatch(module))
            {
                if (!string.IsNullOrEmpty(version))
                {
                    installName = $"{module}@{version}";
                }
            }
            else if (module.Contains('/') || module.Contains('\\'))
            {
                // A path - check if there's a valid package
                installName = module;
                isRegistryPackage = false;
            }
            else
            {
                Log.Warn(I18n._("server.install.install-failed-name", new { name = module }));
                throw new InvalidOperationException("Invalid module name");
            }

            // Check allow/deny lists
            if (!_installAllAllowed)
            {
                if (!CheckModuleAllowed(module, version))
                {
                    throw new InvalidOperationException("Install not allowed");
                }
            }

            // Check if module is already loaded
            // TODO: Check registry.getModuleInfo(module)

            if (!isUpgrade)
            {
                Log.Info(I18n._("server.install.installing", new { name = module, version = version ?? "latest" }));
            }
            else
            {
                Log.Info(I18n._("server.install.upgrading", new { name = module, version = version ?? "latest" }));
            }

            // Run hooks and install
            var triggerPayload = new Dictionary<string, object?>
            {
                { "module", module },
                { "version", version },
                { "url", url },
                { "isUpgrade", isUpgrade }
            };

            await Hooks.TriggerAsync("preInstall", triggerPayload);

            // Run dotnet add package
            // In Node-RED this uses npm, we use dotnet/NuGet
            var installDir = GetInstallDirectory();

            try
            {
                await RunDotnetCommandAsync(installDir, "add", "package", module);

                await Hooks.TriggerAsync("postInstall", triggerPayload);

                Log.Info(I18n._("server.install.installed", new { name = module }));

                return new InstallResult
                {
                    Name = module,
                    Version = version,
                    Nodes = new List<NodeInfo>()
                };
            }
            catch (Exception ex)
            {
                Log.Warn(I18n._("server.install.install-failed-long", new { name = module }));
                Log.Warn("------------------------------------------");
                Log.Warn(ex.Message);
                Log.Warn("------------------------------------------");
                throw new InvalidOperationException(I18n._("server.install.install-failed"));
            }
        }

        /// <summary>
        /// Uninstall a module.
        /// </summary>
        /// <param name="module">The module name.</param>
        /// <returns>List of removed nodes.</returns>
        public async Task<List<NodeInfo>> UninstallModuleAsync(string module)
        {
            if (_settings == null)
            {
                throw new InvalidOperationException("Installer not initialized");
            }

            // Validate module name
            if (Regex.IsMatch(module, @"[\s;]"))
            {
                throw new InvalidOperationException(I18n._("server.install.invalid"));
            }

            Log.Info(I18n._("server.install.uninstalling", new { name = module }));

            var triggerPayload = new Dictionary<string, object?>
            {
                { "module", module }
            };

            await Hooks.TriggerAsync("preUninstall", triggerPayload);

            // Run dotnet remove package
            var installDir = GetInstallDirectory();

            try
            {
                await RunDotnetCommandAsync(installDir, "remove", "package", module);

                Log.Info(I18n._("server.install.uninstalled", new { name = module }));

                await Hooks.TriggerAsync("postUninstall", triggerPayload);

                return new List<NodeInfo>();
            }
            catch (Exception ex)
            {
                Log.Warn(I18n._("server.install.uninstall-failed-long", new { name = module }));
                Log.Warn("------------------------------------------");
                Log.Warn(ex.Message);
                Log.Warn("------------------------------------------");
                throw new InvalidOperationException(I18n._("server.install.uninstall-failed", new { name = module }));
            }
        }

        /// <summary>
        /// Check if a module is allowed by the allow/deny lists.
        /// </summary>
        /// <param name="module">The module name.</param>
        /// <param name="version">The version (optional).</param>
        /// <returns>True if allowed.</returns>
        private bool CheckModuleAllowed(string module, string? version)
        {
            // Check deny list first
            foreach (var rule in _installDenyList)
            {
                if (MatchesRule(module, version, rule))
                {
                    return false;
                }
            }

            // Check allow list
            if (_installAllowList.Count == 0) return true;

            foreach (var rule in _installAllowList)
            {
                if (MatchesRule(module, version, rule))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if a module matches a rule.
        /// </summary>
        private static bool MatchesRule(string module, string? version, ModuleRule rule)
        {
            if (rule.Module == "*") return true;

            if (rule.Module != module) return false;

            if (rule.Version == null) return true;

            return version == rule.Version;
        }

        /// <summary>
        /// Get the installation directory.
        /// </summary>
        private string GetInstallDirectory()
        {
            // TODO: Get from settings.userDir
            return Environment.CurrentDirectory;
        }

        /// <summary>
        /// Run a dotnet command.
        /// </summary>
        private static async Task RunDotnetCommandAsync(string workingDir, params string[] args)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = string.Join(" ", args),
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(error);
            }
        }
    }
}
// ------------------------------------------------------------
// MAPPING NOTES:
// - installerEnabled variable → InstallerEnabled property
// - settings variable → _settings field
// - moduleRe regex → ModuleRegex static field
// - pkgurlRe regex → PkgUrlRegex static field
// - localtgzRe regex → LocalTgzRegex static field (changed from .tgz to .nupkg)
// - installAllowList array → _installAllowList List
// - installDenyList array → _installDenyList List
// - updateAllowList array → _updateAllowList List
// - updateDenyList array → _updateDenyList List
// - activePromise → _activePromise Task
// - init function → Init method
// - checkPrereq function → CheckPrereqAsync method
// - installModule function → InstallModuleAsync method
// - uninstallModule function → UninstallModuleAsync method
// - npm command → dotnet command (adaptation for .NET ecosystem)
// - child_process.execFile → Process.Start
// - Promise → Task
// - hooks.trigger → Hooks.Instance.TriggerAsync
// ============================================================
