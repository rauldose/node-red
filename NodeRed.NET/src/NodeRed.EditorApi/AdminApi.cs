// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-api/lib/admin/index.js
// LINES: 1-101
// ============================================================
// ORIGINAL CODE:
// ------------------------------------------------------------
// module.exports = {
//     init: function(settings, runtimeAPI) {
//         flows.init(runtimeAPI);
//         ...
//         adminApp.get("/flows", needsPermission("flows.read"), flows.get, ...);
//         adminApp.post("/flows", needsPermission("flows.write"), flows.post, ...);
//         ...
//     }
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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NodeRed.Util;

namespace NodeRed.EditorApi
{
    /// <summary>
    /// Admin API for Node-RED.
    /// Provides REST endpoints for flows, nodes, context, settings, plugins, and diagnostics.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/editor-api/lib/admin/index.js
    /// </remarks>
    public class AdminApi
    {
        private Runtime.Settings? _settings;
        private Runtime.FlowsManager? _runtimeApi;
        private AuthMiddleware _auth = new();

        /// <summary>
        /// Initialize the admin API.
        /// </summary>
        public void Init(Runtime.Settings settings, Runtime.FlowsManager runtimeApi)
        {
            _settings = settings;
            _runtimeApi = runtimeApi;
            _auth.Init(settings, null!);
        }

        /// <summary>
        /// Map admin API endpoints.
        /// </summary>
        public void MapEndpoints(WebApplication app)
        {
            // Flows endpoints
            app.MapGet("/flows", GetFlowsHandler);
            app.MapPost("/flows", PostFlowsHandler);

            // Flows state endpoints
            app.MapGet("/flows/state", GetFlowsStateHandler);

            var runtimeStateEnabled = _settings?.Get("runtimeState.enabled");
            if (runtimeStateEnabled is true)
            {
                app.MapPost("/flows/state", PostFlowsStateHandler);
            }

            // Individual flow endpoints
            app.MapGet("/flow/{id}", GetFlowHandler);
            app.MapPost("/flow", PostFlowHandler);
            app.MapDelete("/flow/{id}", DeleteFlowHandler);
            app.MapPut("/flow/{id}", PutFlowHandler);

            // Nodes endpoints
            app.MapGet("/nodes", GetNodesHandler);
            app.MapPost("/nodes", PostNodesHandler);
            app.MapGet("/nodes/messages", GetNodeMessagesHandler);

            // Context endpoints
            app.MapGet("/context/{scope}", GetContextHandler);
            app.MapGet("/context/{scope}/{id}", GetContextWithIdHandler);
            app.MapDelete("/context/{scope}/{id}/{key}", DeleteContextHandler);

            // Settings endpoint
            app.MapGet("/settings", GetSettingsHandler);

            // Plugins endpoints
            app.MapGet("/plugins", GetPluginsHandler);
            app.MapGet("/plugins/messages", GetPluginMessagesHandler);

            // Diagnostics endpoint
            app.MapGet("/diagnostics", GetDiagnosticsHandler);
        }

        #region Flows Handlers

        private async Task GetFlowsHandler(HttpContext context)
        {
            // Check API version
            var version = context.Request.Headers["Node-RED-API-Version"].ToString();
            if (string.IsNullOrEmpty(version)) version = "v1";

            if (!System.Text.RegularExpressions.Regex.IsMatch(version, @"^v[12]$"))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "invalid_api_version",
                    message = "Invalid API Version requested"
                });
                return;
            }

            try
            {
                var flows = _runtimeApi!.GetFlows();

                context.Response.ContentType = "application/json";

                if (version == "v1" && flows != null)
                {
                    await context.Response.WriteAsJsonAsync(flows.Flows);
                }
                else // v2
                {
                    await context.Response.WriteAsJsonAsync(flows);
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        private async Task PostFlowsHandler(HttpContext context)
        {
            var version = context.Request.Headers["Node-RED-API-Version"].ToString();
            if (string.IsNullOrEmpty(version)) version = "v1";

            if (!System.Text.RegularExpressions.Regex.IsMatch(version, @"^v[12]$"))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "invalid_api_version",
                    message = "Invalid API Version requested"
                });
                return;
            }

            var deploymentType = context.Request.Headers["Node-RED-Deployment-Type"].ToString();
            if (string.IsNullOrEmpty(deploymentType)) deploymentType = "full";

            try
            {
                if (deploymentType != "reload")
                {
                    var body = await JsonSerializer.DeserializeAsync<List<Dictionary<string, object?>>>(context.Request.Body);
                    await _runtimeApi!.SetFlowsAsync(body, null, deploymentType);
                }
                else
                {
                    await _runtimeApi!.SetFlowsAsync(null, null, "reload");
                }

                if (version == "v1")
                {
                    context.Response.StatusCode = 204;
                }
                else
                {
                    var result = _runtimeApi!.GetFlows();
                    await context.Response.WriteAsJsonAsync(result);
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        private async Task GetFlowsStateHandler(HttpContext context)
        {
            try
            {
                var state = _runtimeApi!.Started ? "start" : "stop";
                await context.Response.WriteAsJsonAsync(new { state });
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        private async Task PostFlowsStateHandler(HttpContext context)
        {
            try
            {
                var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Request.Body);
                var state = body.TryGetProperty("state", out var stateProp) ? stateProp.GetString() : "";

                if (state == "start")
                {
                    await _runtimeApi!.StartAsync();
                }
                else if (state == "stop")
                {
                    await _runtimeApi!.StopAsync();
                }

                var newState = _runtimeApi!.Started ? "start" : "stop";
                await context.Response.WriteAsJsonAsync(new { state = newState });
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        private async Task GetFlowHandler(HttpContext context)
        {
            var id = context.Request.RouteValues["id"] as string ?? "";

            try
            {
                var flow = _runtimeApi!.GetFlow(id);

                if (flow == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                await context.Response.WriteAsJsonAsync(flow);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        private async Task PostFlowHandler(HttpContext context)
        {
            try
            {
                var body = await JsonSerializer.DeserializeAsync<Runtime.FlowInput>(context.Request.Body);
                if (body != null)
                {
                    var result = await _runtimeApi!.AddFlowAsync(body, null);
                    context.Response.StatusCode = 201;
                    await context.Response.WriteAsJsonAsync(new { id = result });
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        private async Task DeleteFlowHandler(HttpContext context)
        {
            var id = context.Request.RouteValues["id"] as string ?? "";

            try
            {
                await _runtimeApi!.RemoveFlowAsync(id, null);
                context.Response.StatusCode = 204;
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        private async Task PutFlowHandler(HttpContext context)
        {
            var id = context.Request.RouteValues["id"] as string ?? "";

            try
            {
                var body = await JsonSerializer.DeserializeAsync<Runtime.FlowInput>(context.Request.Body);
                if (body != null)
                {
                    await _runtimeApi!.UpdateFlowAsync(id, body, null);
                    var result = _runtimeApi!.GetFlow(id);
                    await context.Response.WriteAsJsonAsync(result);
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        #endregion

        #region Nodes Handlers

        private async Task GetNodesHandler(HttpContext context)
        {
            try
            {
                // TODO: Get nodes from registry
                var nodes = new List<object>();
                await context.Response.WriteAsJsonAsync(nodes);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        private async Task PostNodesHandler(HttpContext context)
        {
            try
            {
                // TODO: Install node module
                context.Response.StatusCode = 201;
                await context.Response.WriteAsJsonAsync(new { });
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        private async Task GetNodeMessagesHandler(HttpContext context)
        {
            try
            {
                // Return message catalogs for all node modules
                var catalogs = new Dictionary<string, object>();
                await context.Response.WriteAsJsonAsync(catalogs);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        #endregion

        #region Context Handlers

        private async Task GetContextHandler(HttpContext context)
        {
            var scope = context.Request.RouteValues["scope"] as string ?? "";

            try
            {
                // TODO: Get context from context manager
                var contextData = new Dictionary<string, object>();
                await context.Response.WriteAsJsonAsync(contextData);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        private async Task GetContextWithIdHandler(HttpContext context)
        {
            var scope = context.Request.RouteValues["scope"] as string ?? "";
            var id = context.Request.RouteValues["id"] as string ?? "";

            try
            {
                // TODO: Get context from context manager
                var contextData = new Dictionary<string, object>();
                await context.Response.WriteAsJsonAsync(contextData);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        private async Task DeleteContextHandler(HttpContext context)
        {
            var scope = context.Request.RouteValues["scope"] as string ?? "";
            var id = context.Request.RouteValues["id"] as string ?? "";
            var key = context.Request.RouteValues["key"] as string ?? "";

            try
            {
                // TODO: Delete context key
                context.Response.StatusCode = 204;
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        #endregion

        #region Settings Handler

        private async Task GetSettingsHandler(HttpContext context)
        {
            try
            {
                var settings = new Dictionary<string, object?>
                {
                    { "httpNodeRoot", _settings?.Get("httpNodeRoot") ?? "/" },
                    { "version", _settings?.Get("version") ?? "1.0.0" },
                    { "context", new { stores = new[] { "memory" } } },
                    { "libraries", new List<object>() },
                    { "editorTheme", new Dictionary<string, object>() }
                };

                await context.Response.WriteAsJsonAsync(settings);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        #endregion

        #region Plugins Handlers

        private async Task GetPluginsHandler(HttpContext context)
        {
            try
            {
                // TODO: Get plugins from registry
                var plugins = new List<object>();
                await context.Response.WriteAsJsonAsync(plugins);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        private async Task GetPluginMessagesHandler(HttpContext context)
        {
            try
            {
                // Return message catalogs for all plugins
                var catalogs = new Dictionary<string, object>();
                await context.Response.WriteAsJsonAsync(catalogs);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        #endregion

        #region Diagnostics Handler

        private async Task GetDiagnosticsHandler(HttpContext context)
        {
            try
            {
                var diagnostics = new Dictionary<string, object>
                {
                    { "platform", Environment.OSVersion.ToString() },
                    { "runtime", Environment.Version.ToString() },
                    { "memory", GC.GetTotalMemory(false) }
                };

                await context.Response.WriteAsJsonAsync(diagnostics);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        #endregion
    }
}
// ------------------------------------------------------------
// MAPPING NOTES:
// - Express routes → ASP.NET Core MapGet/MapPost/etc
// - needsPermission middleware → Authorization middleware (to be integrated)
// - req.get("header") → context.Request.Headers["header"]
// - req.body → JsonSerializer.Deserialize(context.Request.Body)
// - res.json() → context.Response.WriteAsJsonAsync()
// - res.status(code).end() → context.Response.StatusCode = code
// - apiUtil.errorHandler → try/catch with JSON error response
// ============================================================
