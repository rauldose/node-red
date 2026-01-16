// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-api/lib/auth/index.js
// LINES: 1-285
// ============================================================
// ORIGINAL CODE:
// ------------------------------------------------------------
// function init(_settings,storage) { ... }
// function needsPermission(permission) { ... }
// async function login(req,res) { ... }
// function revoke(req,res) { ... }
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
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NodeRed.Util;

namespace NodeRed.EditorApi
{
    /// <summary>
    /// User information.
    /// </summary>
    public class AuthUser
    {
        public string Username { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
        public AuthTokens? Tokens { get; set; }
    }

    /// <summary>
    /// Authentication tokens.
    /// </summary>
    public class AuthTokens
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    /// <summary>
    /// Authentication middleware for the Editor API.
    /// Provides authentication and authorization for the Node-RED editor.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/editor-api/lib/auth/index.js
    /// </remarks>
    public class AuthMiddleware
    {
        private Runtime.Settings? _settings;
        private Runtime.Storage? _storage;
        private readonly TokenStore _tokens = new();
        private readonly UserStore _users = new();

        /// <summary>
        /// Initialize the auth middleware.
        /// </summary>
        public void Init(Runtime.Settings settings, Runtime.Storage storage)
        {
            _settings = settings;
            _storage = storage;

            var adminAuth = settings.Get("adminAuth");
            if (adminAuth != null)
            {
                _users.Init(adminAuth);
                _tokens.Init(adminAuth, storage);
            }
        }

        /// <summary>
        /// Returns a middleware function that ensures the user has the necessary permission.
        /// </summary>
        public Func<HttpContext, Func<Task>, Task> NeedsPermission(string permission)
        {
            return async (context, next) =>
            {
                if (_settings?.Get("adminAuth") != null)
                {
                    // Extract bearer token
                    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                    if (authHeader?.StartsWith("Bearer ") == true)
                    {
                        var token = authHeader.Substring(7);
                        var user = _tokens.Validate(token);

                        if (user != null)
                        {
                            if (Permissions.HasPermission(user.Permissions, permission))
                            {
                                context.Items["user"] = user;
                                await next();
                                return;
                            }
                        }
                    }

                    // No valid token or insufficient permissions
                    Log.Audit(new LogMessage { Event = "permission.fail", Msg = new { permissions = permission } });
                    context.Response.StatusCode = 401;
                    return;
                }

                // No auth configured, allow access
                await next();
            };
        }

        /// <summary>
        /// Handle login request.
        /// </summary>
        public async Task LoginHandler(HttpContext context)
        {
            var response = new Dictionary<string, object>();

            var adminAuth = _settings?.Get("adminAuth");
            if (adminAuth != null)
            {
                // Determine auth type
                var authType = GetAuthType(adminAuth);

                if (authType == "credentials")
                {
                    response["type"] = "credentials";
                    response["prompts"] = new List<object>
                    {
                        new { id = "username", type = "text", label = "user.username" },
                        new { id = "password", type = "password", label = "user.password" }
                    };
                }
                else if (authType == "strategy")
                {
                    var httpAdminRoot = _settings?.Get("httpAdminRoot")?.ToString()?.TrimEnd('/') ?? "";
                    if (httpAdminRoot.Length > 0) httpAdminRoot += "/";

                    response["type"] = "strategy";
                    response["prompts"] = new List<object>
                    {
                        new { type = "button", label = "Login", url = httpAdminRoot + "auth/strategy" }
                    };
                }
            }

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(response);
        }

        /// <summary>
        /// Handle token request.
        /// </summary>
        public async Task TokenHandler(HttpContext context)
        {
            try
            {
                // Read request body
                var form = await context.Request.ReadFormAsync();
                var grantType = form["grant_type"].FirstOrDefault();
                var username = form["username"].FirstOrDefault();
                var password = form["password"].FirstOrDefault();

                if (grantType == "password")
                {
                    var user = await _users.AuthenticateAsync(username ?? "", password ?? "");

                    if (user != null)
                    {
                        var tokens = _tokens.Create(user.Username, "node-red-editor", user.Permissions);

                        Log.Audit(new LogMessage { Event = "auth.login", User = username, Msg = new { scope = user.Permissions } });

                        await context.Response.WriteAsJsonAsync(new
                        {
                            access_token = tokens.AccessToken,
                            refresh_token = tokens.RefreshToken,
                            expires_in = tokens.ExpiresIn,
                            token_type = "Bearer"
                        });
                        return;
                    }
                }

                Log.Audit(new LogMessage { Event = "auth.login.fail", User = username });
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "invalid_grant" });
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Handle revoke request.
        /// </summary>
        public async Task RevokeHandler(HttpContext context)
        {
            try
            {
                var form = await context.Request.ReadFormAsync();
                var token = form["token"].FirstOrDefault();

                if (!string.IsNullOrEmpty(token))
                {
                    _tokens.Revoke(token);
                    Log.Audit(new LogMessage { Event = "auth.login.revoke" });
                }

                var logoutRedirect = _settings?.Get("editorTheme.logout.redirect")?.ToString();
                if (!string.IsNullOrEmpty(logoutRedirect))
                {
                    await context.Response.WriteAsJsonAsync(new { redirect = logoutRedirect });
                }
                else
                {
                    context.Response.StatusCode = 200;
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        private string GetAuthType(object? adminAuth)
        {
            if (adminAuth is IDictionary<string, object?> dict)
            {
                if (dict.TryGetValue("type", out var type))
                {
                    return type?.ToString() ?? "credentials";
                }
            }
            return "credentials";
        }
    }

    /// <summary>
    /// Permission checking utilities.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/editor-api/lib/auth/permissions.js
    /// </remarks>
    public static class Permissions
    {
        private static readonly Regex ReadRe = new(@"^((.+)\.)?read$");
        private static readonly Regex WriteRe = new(@"^((.+)\.)?write$");

        /// <summary>
        /// Check if the user scope includes the required permission.
        /// </summary>
        public static bool HasPermission(object? userScope, string permission)
        {
            if (string.IsNullOrEmpty(permission)) return true;

            // Handle array of permissions
            if (permission.Contains(','))
            {
                var permissions = permission.Split(',');
                return permissions.All(p => HasPermission(userScope, p.Trim()));
            }

            if (userScope is IEnumerable<string> scopeList)
            {
                if (!scopeList.Any()) return false;
                return scopeList.Any(s => HasPermission(s, permission));
            }

            if (userScope is string scope)
            {
                if (scope == "*" || scope == permission) return true;

                if (scope == "read" || scope == "*.read")
                {
                    return ReadRe.IsMatch(permission);
                }
                else if (scope == "write" || scope == "*.write")
                {
                    return WriteRe.IsMatch(permission);
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Token storage and management.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/editor-api/lib/auth/tokens.js
    /// </remarks>
    public class TokenStore
    {
        private readonly Dictionary<string, TokenEntry> _tokens = new();
        private Runtime.Storage? _storage;

        /// <summary>
        /// Initialize the token store.
        /// </summary>
        public void Init(object? adminAuth, Runtime.Storage? storage)
        {
            _storage = storage;
        }

        /// <summary>
        /// Create a new token for the user.
        /// </summary>
        public AuthTokens Create(string username, string clientId, List<string> permissions)
        {
            var accessToken = GenerateToken();
            var refreshToken = GenerateToken();

            var entry = new TokenEntry
            {
                Username = username,
                ClientId = clientId,
                Permissions = permissions,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresIn = 604800 // 7 days in seconds
            };

            _tokens[accessToken] = entry;

            return new AuthTokens
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = entry.ExpiresIn
            };
        }

        /// <summary>
        /// Validate a token and return the user.
        /// </summary>
        public AuthUser? Validate(string token)
        {
            if (_tokens.TryGetValue(token, out var entry))
            {
                // Check expiration
                var expiresAt = entry.CreatedAt.AddSeconds(entry.ExpiresIn);
                if (DateTime.UtcNow < expiresAt)
                {
                    return new AuthUser
                    {
                        Username = entry.Username,
                        Permissions = entry.Permissions
                    };
                }

                // Token expired, remove it
                _tokens.Remove(token);
            }

            return null;
        }

        /// <summary>
        /// Revoke a token.
        /// </summary>
        public void Revoke(string token)
        {
            _tokens.Remove(token);
        }

        private static string GenerateToken()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        private class TokenEntry
        {
            public string Username { get; set; } = string.Empty;
            public string ClientId { get; set; } = string.Empty;
            public List<string> Permissions { get; set; } = new();
            public string AccessToken { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public int ExpiresIn { get; set; }
        }
    }

    /// <summary>
    /// User storage and authentication.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/editor-api/lib/auth/users.js
    /// </remarks>
    public class UserStore
    {
        private readonly List<UserEntry> _users = new();
        private Func<string, string, Task<AuthUser?>>? _authenticateFunc;

        /// <summary>
        /// Initialize the user store.
        /// </summary>
        public void Init(object? adminAuth)
        {
            if (adminAuth is IDictionary<string, object?> dict)
            {
                // Check for users array
                if (dict.TryGetValue("users", out var usersObj) && usersObj is IEnumerable<object> users)
                {
                    foreach (var user in users)
                    {
                        if (user is IDictionary<string, object?> userDict)
                        {
                            var entry = new UserEntry
                            {
                                Username = userDict.TryGetValue("username", out var u) ? u?.ToString() ?? "" : "",
                                Password = userDict.TryGetValue("password", out var p) ? p?.ToString() ?? "" : "",
                                Permissions = new List<string> { "*" }
                            };

                            if (userDict.TryGetValue("permissions", out var perms))
                            {
                                if (perms is string permStr)
                                {
                                    entry.Permissions = new List<string> { permStr };
                                }
                                else if (perms is IEnumerable<string> permList)
                                {
                                    entry.Permissions = permList.ToList();
                                }
                            }

                            _users.Add(entry);
                        }
                    }
                }

                // Check for custom authenticate function
                if (dict.TryGetValue("authenticate", out var authFunc))
                {
                    _authenticateFunc = authFunc as Func<string, string, Task<AuthUser?>>;
                }
            }
        }

        /// <summary>
        /// Authenticate a user.
        /// </summary>
        public async Task<AuthUser?> AuthenticateAsync(string username, string password)
        {
            // Use custom authenticate function if available
            if (_authenticateFunc != null)
            {
                return await _authenticateFunc(username, password);
            }

            // Check local users
            var user = _users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (user != null)
            {
                // Check password
                if (VerifyPassword(password, user.Password))
                {
                    return new AuthUser
                    {
                        Username = user.Username,
                        Permissions = user.Permissions
                    };
                }
            }

            return null;
        }

        private static bool VerifyPassword(string password, string hash)
        {
            // Simple comparison for now
            // TODO: Implement bcrypt comparison
            if (hash.StartsWith("$2"))
            {
                // BCrypt hash - would need BCrypt.Net library
                return false;
            }

            return password == hash;
        }

        private class UserEntry
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public List<string> Permissions { get; set; } = new();
        }
    }
}
// ------------------------------------------------------------
// MAPPING NOTES:
// - passport → custom AuthMiddleware
// - oauth2orize → custom token handling
// - Tokens module → TokenStore class
// - Users module → UserStore class
// - permissions module → Permissions static class
// - bearerStrategy → token validation in NeedsPermission
// - session handling → stateless tokens (JWT-like)
// - init function → Init method
// - needsPermission function → NeedsPermission method
// - login function → LoginHandler method
// - revoke function → RevokeHandler method
// ============================================================
