// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-api/lib/index.js
// LINES: 1-146
// ============================================================
// ORIGINAL CODE:
// ------------------------------------------------------------
// function init(settings,_server,storage,runtimeAPI) {
//     server = _server;
//     if (settings.httpAdminRoot !== false) {
//         adminApp = apiUtil.createExpressApp(settings);
//         ...
//     }
// }
// async function start() { ... }
// async function stop() { ... }
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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NodeRed.Util;

namespace NodeRed.EditorApi
{
    /// <summary>
    /// Main Editor API entry point.
    /// Provides Express-like application to serve the Node-RED editor and admin API.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/editor-api/lib/index.js
    /// </remarks>
    public class EditorApi
    {
        private Runtime.Settings? _settings;
        private Runtime.Storage? _storage;
        private Runtime.FlowsManager? _runtimeApi;
        private EditorServer? _editor;
        private AdminApi? _adminApi;
        private bool _initialized;

        /// <summary>
        /// Gets the authentication middleware.
        /// </summary>
        public AuthMiddleware Auth { get; } = new AuthMiddleware();

        /// <summary>
        /// Gets whether the admin root is enabled.
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                try
                {
                    return _settings?.Available() == true && _settings.Get("httpAdminRoot") is not false;
                }
                catch
                {
                    return true; // Default to enabled if settings not available
                }
            }
        }

        /// <summary>
        /// Initialize the Editor API module.
        /// </summary>
        /// <param name="settings">The runtime settings.</param>
        /// <param name="storage">An instance of Node-RED Storage.</param>
        /// <param name="runtimeApi">An instance of Node-RED Runtime API.</param>
        public void Init(Runtime.Settings settings, Runtime.Storage storage, Runtime.FlowsManager runtimeApi)
        {
            _settings = settings;
            _storage = storage;
            _runtimeApi = runtimeApi;

            try
            {
                var httpAdminRoot = settings.Available() ? settings.Get("httpAdminRoot") : null;
                if (httpAdminRoot is not false)
                {
                    Auth.Init(settings, storage);

                    _editor = new EditorServer();
                    _editor.Init(settings, runtimeApi);

                    _adminApi = new AdminApi();
                    _adminApi.Init(settings, runtimeApi);
                }
            }
            catch (InvalidOperationException)
            {
                // Settings not available, but we can still initialize
            }

            _initialized = true;
        }

        /// <summary>
        /// Configure ASP.NET Core endpoints for the Editor API.
        /// </summary>
        /// <param name="app">The web application builder.</param>
        public void MapEndpoints(WebApplication app)
        {
            if (!IsEnabled || !_initialized) return;

            // Auth endpoints
            app.MapGet("/auth/login", Auth.LoginHandler);

            if (_settings?.Get("adminAuth") != null)
            {
                app.MapPost("/auth/token", Auth.TokenHandler);
                app.MapPost("/auth/revoke", Auth.RevokeHandler);
            }

            // Admin API endpoints
            _adminApi?.MapEndpoints(app);

            // Editor endpoints
            _editor?.MapEndpoints(app);
        }

        /// <summary>
        /// Start the module.
        /// </summary>
        /// <returns>A task that resolves when the application is ready.</returns>
        public async Task StartAsync()
        {
            if (_editor != null)
            {
                await _editor.StartAsync();
            }
        }

        /// <summary>
        /// Stop the module.
        /// </summary>
        /// <returns>A task that resolves when the application is stopped.</returns>
        public async Task StopAsync()
        {
            if (_editor != null)
            {
                await _editor.StopAsync();
            }
        }
    }

    /// <summary>
    /// Editor server for serving the Blazor editor.
    /// </summary>
    public class EditorServer
    {
        private Runtime.Settings? _settings;
        private Runtime.FlowsManager? _runtimeApi;
        private CommsHandler? _comms;

        /// <summary>
        /// Initialize the editor server.
        /// </summary>
        public void Init(Runtime.Settings settings, Runtime.FlowsManager runtimeApi)
        {
            _settings = settings;
            _runtimeApi = runtimeApi;
            _comms = new CommsHandler();
            _comms.Init(settings, runtimeApi);
        }

        /// <summary>
        /// Map editor endpoints.
        /// </summary>
        public void MapEndpoints(WebApplication app)
        {
            // Locales endpoint
            app.MapGet("/locales/{scope}", LocalesHandler);
            app.MapGet("/locales/{scope}/{namespace}", LocaleHandler);

            // Library endpoints
            app.MapGet("/library/flows", LibraryFlowsHandler);
            app.MapGet("/library/flows/{name}", LibraryFlowHandler);
            app.MapPost("/library/flows/{name}", LibraryFlowPostHandler);

            // Theme endpoint
            app.MapGet("/theme", ThemeHandler);

            // Settings endpoint
            app.MapGet("/settings", SettingsHandler);

            // Projects endpoints (if enabled)
            var projectsEnabled = _settings?.Get("editorTheme.projects.enabled");
            if (projectsEnabled is true)
            {
                app.MapGet("/projects", ProjectsHandler);
            }
        }

        private async Task LocalesHandler(HttpContext context)
        {
            var scope = context.Request.RouteValues["scope"] as string ?? "";
            var lang = context.Request.Query["lng"].ToString();

            if (string.IsNullOrEmpty(lang))
            {
                lang = "en-US";
            }

            try
            {
                var catalog = I18n.GetCatalog(scope, lang);
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(catalog);
            }
            catch (Exception)
            {
                context.Response.StatusCode = 404;
            }
        }

        private async Task LocaleHandler(HttpContext context)
        {
            var scope = context.Request.RouteValues["scope"] as string ?? "";
            var ns = context.Request.RouteValues["namespace"] as string ?? "";
            var lang = context.Request.Query["lng"].ToString();

            if (string.IsNullOrEmpty(lang))
            {
                lang = "en-US";
            }

            try
            {
                var catalog = I18n.GetCatalog($"{scope}/{ns}", lang);
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(catalog);
            }
            catch (Exception)
            {
                context.Response.StatusCode = 404;
            }
        }

        private async Task LibraryFlowsHandler(HttpContext context)
        {
            // TODO: Implement library flows listing
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new List<object>());
        }

        private async Task LibraryFlowHandler(HttpContext context)
        {
            var name = context.Request.RouteValues["name"] as string ?? "";
            // TODO: Implement library flow retrieval
            context.Response.StatusCode = 404;
            await Task.CompletedTask;
        }

        private async Task LibraryFlowPostHandler(HttpContext context)
        {
            var name = context.Request.RouteValues["name"] as string ?? "";
            // TODO: Implement library flow save
            context.Response.StatusCode = 204;
            await Task.CompletedTask;
        }

        private async Task ThemeHandler(HttpContext context)
        {
            var theme = new Dictionary<string, object>();

            // Build theme from settings
            var editorTheme = _settings?.Get("editorTheme");
            if (editorTheme != null)
            {
                // Copy relevant theme settings
            }

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(theme);
        }

        private async Task SettingsHandler(HttpContext context)
        {
            var settings = new Dictionary<string, object?>
            {
                { "httpNodeRoot", _settings?.Get("httpNodeRoot") ?? "/" },
                { "version", _settings?.Get("version") ?? "1.0.0" },
                { "paletteCategories", _settings?.Get("paletteCategories") }
            };

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(settings);
        }

        private async Task ProjectsHandler(HttpContext context)
        {
            // TODO: Implement projects listing
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { projects = new List<object>() });
        }

        /// <summary>
        /// Start the editor server.
        /// </summary>
        public Task StartAsync()
        {
            _comms?.Start();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop the editor server.
        /// </summary>
        public Task StopAsync()
        {
            _comms?.Stop();
            return Task.CompletedTask;
        }
    }
}
// ------------------------------------------------------------
// MAPPING NOTES:
// - Express app → ASP.NET Core minimal API
// - app.get/post → app.MapGet/MapPost
// - bodyParser → built-in JSON parsing
// - cors middleware → CORS configuration in ASP.NET Core
// - passport → custom AuthMiddleware
// - init function → Init method
// - start function → StartAsync method
// - stop function → StopAsync method
// ============================================================
