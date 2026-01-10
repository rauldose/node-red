// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Services;

/// <summary>
/// Service for checking user permissions.
/// Based on packages/node_modules/@node-red/editor-api/lib/auth/permissions.js
/// </summary>
public class PermissionService : IPermissionService
{
    /// <summary>
    /// Default permissions for anonymous users.
    /// </summary>
    public List<string> AnonymousPermissions { get; set; } = new() { Permissions.FlowsRead };

    /// <inheritdoc />
    public bool HasPermission(User user, string permission)
    {
        if (user == null) return false;
        return HasPermission(user.Permissions, permission);
    }

    /// <inheritdoc />
    public bool HasPermission(IEnumerable<string> scopes, string permission)
    {
        if (scopes == null || string.IsNullOrEmpty(permission))
        {
            return false;
        }

        var scopeList = scopes.ToList();

        // Full access grants everything
        if (scopeList.Contains(Permissions.FullAccess))
        {
            return true;
        }

        // Check for exact match
        if (scopeList.Contains(permission))
        {
            return true;
        }

        // Check for wildcard permissions
        // e.g., "flows.*" grants "flows.read" and "flows.write"
        var parts = permission.Split('.');
        if (parts.Length == 2)
        {
            var wildcardPermission = $"{parts[0]}.*";
            if (scopeList.Contains(wildcardPermission))
            {
                return true;
            }
        }

        // Check if read permission is granted for a write permission
        // (write doesn't imply read, but we can check if read is available)
        
        return false;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAnonymousPermissions()
    {
        return AnonymousPermissions;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAllPermissions()
    {
        return Permissions.All;
    }

    /// <summary>
    /// Validates that a set of scopes contains only valid permissions.
    /// </summary>
    /// <param name="scopes">The scopes to validate.</param>
    /// <returns>True if all scopes are valid.</returns>
    public bool ValidateScopes(IEnumerable<string> scopes)
    {
        var validPermissions = new HashSet<string>(Permissions.All);
        
        foreach (var scope in scopes)
        {
            // Allow exact matches
            if (validPermissions.Contains(scope))
            {
                continue;
            }

            // Allow wildcard permissions for known categories
            if (scope.EndsWith(".*"))
            {
                var category = scope.Substring(0, scope.Length - 2);
                if (validPermissions.Any(p => p.StartsWith(category + ".")))
                {
                    continue;
                }
            }

            // Invalid scope
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the required permission for an API endpoint.
    /// </summary>
    /// <param name="method">HTTP method (GET, POST, PUT, DELETE).</param>
    /// <param name="path">API path.</param>
    /// <returns>The required permission.</returns>
    public string GetRequiredPermission(string method, string path)
    {
        var isWrite = method.ToUpperInvariant() switch
        {
            "POST" => true,
            "PUT" => true,
            "DELETE" => true,
            "PATCH" => true,
            _ => false
        };

        var suffix = isWrite ? ".write" : ".read";

        // Map paths to permission categories
        if (path.StartsWith("/flows"))
        {
            return "flows" + suffix;
        }
        if (path.StartsWith("/nodes"))
        {
            return "nodes" + suffix;
        }
        if (path.StartsWith("/library"))
        {
            return "library" + suffix;
        }
        if (path.StartsWith("/context"))
        {
            return "context" + suffix;
        }
        if (path.StartsWith("/settings"))
        {
            return "settings" + suffix;
        }

        // Default to requiring full access for unknown paths
        return Permissions.FullAccess;
    }
}
