// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;

namespace NodeRed.Core.Interfaces;

/// <summary>
/// Service for user authentication and management.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Authenticates a user with username and password.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <returns>The authenticated user, or null if authentication failed.</returns>
    Task<User?> AuthenticateAsync(string username, string password);

    /// <summary>
    /// Gets a user by their ID.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The user, or null if not found.</returns>
    Task<User?> GetUserByIdAsync(string userId);

    /// <summary>
    /// Gets a user by their username.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <returns>The user, or null if not found.</returns>
    Task<User?> GetUserByUsernameAsync(string username);

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="user">The user to create.</param>
    /// <param name="password">The plain-text password to hash.</param>
    /// <returns>The created user.</returns>
    Task<User> CreateUserAsync(User user, string password);

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    /// <param name="user">The user to update.</param>
    Task UpdateUserAsync(User user);

    /// <summary>
    /// Deletes a user.
    /// </summary>
    /// <param name="userId">The user ID to delete.</param>
    Task DeleteUserAsync(string userId);

    /// <summary>
    /// Gets all users.
    /// </summary>
    /// <returns>List of all users.</returns>
    Task<IEnumerable<User>> GetAllUsersAsync();

    /// <summary>
    /// Changes a user's password.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="currentPassword">The current password for verification.</param>
    /// <param name="newPassword">The new password.</param>
    /// <returns>True if password was changed successfully.</returns>
    Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword);

    /// <summary>
    /// Verifies if a password matches the stored hash.
    /// </summary>
    /// <param name="password">The plain-text password.</param>
    /// <param name="passwordHash">The stored hash.</param>
    /// <returns>True if the password matches.</returns>
    bool VerifyPassword(string password, string passwordHash);

    /// <summary>
    /// Hashes a password for storage.
    /// </summary>
    /// <param name="password">The plain-text password.</param>
    /// <returns>The hashed password.</returns>
    string HashPassword(string password);
}

/// <summary>
/// Service for managing authentication tokens.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Creates a new access token for a user.
    /// </summary>
    /// <param name="user">The user to create a token for.</param>
    /// <param name="clientId">The client requesting the token.</param>
    /// <param name="scopes">The scopes to grant.</param>
    /// <returns>The created token.</returns>
    Task<AuthToken> CreateTokenAsync(User user, string clientId, IEnumerable<string>? scopes = null);

    /// <summary>
    /// Validates an access token.
    /// </summary>
    /// <param name="accessToken">The access token to validate.</param>
    /// <returns>The user associated with the token, or null if invalid.</returns>
    Task<User?> ValidateTokenAsync(string accessToken);

    /// <summary>
    /// Revokes an access token.
    /// </summary>
    /// <param name="accessToken">The access token to revoke.</param>
    Task RevokeTokenAsync(string accessToken);

    /// <summary>
    /// Revokes all tokens for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    Task RevokeAllUserTokensAsync(string userId);

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <returns>A new token pair, or null if refresh failed.</returns>
    Task<AuthToken?> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Cleans up expired tokens.
    /// </summary>
    Task CleanupExpiredTokensAsync();
}

/// <summary>
/// Service for checking user permissions.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Checks if a user has a specific permission.
    /// </summary>
    /// <param name="user">The user to check.</param>
    /// <param name="permission">The permission required (e.g., "flows.write").</param>
    /// <returns>True if the user has the permission.</returns>
    bool HasPermission(User user, string permission);

    /// <summary>
    /// Checks if a set of scopes includes a specific permission.
    /// </summary>
    /// <param name="scopes">The granted scopes.</param>
    /// <param name="permission">The permission required.</param>
    /// <returns>True if the scopes include the permission.</returns>
    bool HasPermission(IEnumerable<string> scopes, string permission);

    /// <summary>
    /// Gets the default permissions for anonymous users.
    /// </summary>
    /// <returns>List of default permissions.</returns>
    IEnumerable<string> GetAnonymousPermissions();

    /// <summary>
    /// Gets all available permissions.
    /// </summary>
    /// <returns>List of all defined permissions.</returns>
    IEnumerable<string> GetAllPermissions();
}

/// <summary>
/// Standard permission scopes for Node-RED.
/// </summary>
public static class Permissions
{
    public const string FlowsRead = "flows.read";
    public const string FlowsWrite = "flows.write";
    public const string NodesRead = "nodes.read";
    public const string NodesWrite = "nodes.write";
    public const string LibraryRead = "library.read";
    public const string LibraryWrite = "library.write";
    public const string ContextRead = "context.read";
    public const string ContextWrite = "context.write";
    public const string SettingsRead = "settings.read";
    public const string SettingsWrite = "settings.write";
    public const string FullAccess = "*";

    public static readonly string[] All = new[]
    {
        FlowsRead, FlowsWrite,
        NodesRead, NodesWrite,
        LibraryRead, LibraryWrite,
        ContextRead, ContextWrite,
        SettingsRead, SettingsWrite,
        FullAccess
    };

    public static readonly string[] ReadOnly = new[]
    {
        FlowsRead, NodesRead, LibraryRead, ContextRead, SettingsRead
    };

    public static readonly string[] Default = new[]
    {
        FlowsRead, FlowsWrite, NodesRead, LibraryRead, ContextRead
    };
}
