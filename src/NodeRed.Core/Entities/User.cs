// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Core.Entities;

/// <summary>
/// Represents a user in the Node-RED system.
/// </summary>
public class User
{
    /// <summary>
    /// Unique identifier for the user.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Username for authentication.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Hashed password. Use PasswordHasher to set this.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// List of permission scopes granted to this user.
    /// Examples: "flows.read", "flows.write", "*"
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Whether this is an anonymous user.
    /// </summary>
    public bool Anonymous { get; set; }

    /// <summary>
    /// Display name for the user.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Email address (optional).
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// When the user was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Last login timestamp.
    /// </summary>
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// Whether the user account is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Creates an anonymous user with a random name.
    /// </summary>
    public static User CreateAnonymous()
    {
        var random = new Random();
        return new User
        {
            Anonymous = true,
            Username = $"Anon {random.Next(100)}",
            DisplayName = $"Anonymous User",
            Permissions = new List<string> { "flows.read" } // Read-only by default
        };
    }
}

/// <summary>
/// Represents an authentication token.
/// </summary>
public class AuthToken
{
    /// <summary>
    /// The access token string.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Token type (usually "Bearer").
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// When the token expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Refresh token for obtaining new access tokens.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// The user this token belongs to.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The scopes/permissions this token grants.
    /// </summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>
    /// Client identifier that requested this token.
    /// </summary>
    public string ClientId { get; set; } = "node-red-editor";

    /// <summary>
    /// Whether this token has been revoked.
    /// </summary>
    public bool Revoked { get; set; }
}

/// <summary>
/// Login request payload.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Username to authenticate.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Password to authenticate.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Client identifier.
    /// </summary>
    public string ClientId { get; set; } = "node-red-editor";

    /// <summary>
    /// Grant type (usually "password").
    /// </summary>
    public string GrantType { get; set; } = "password";
}

/// <summary>
/// Login response payload.
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// Whether login was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The authentication token if successful.
    /// </summary>
    public AuthToken? Token { get; set; }

    /// <summary>
    /// Error message if login failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Login prompts for the UI.
    /// </summary>
    public List<LoginPrompt>? Prompts { get; set; }

    /// <summary>
    /// Authentication type ("credentials", "strategy").
    /// </summary>
    public string? Type { get; set; }
}

/// <summary>
/// Login prompt for the UI.
/// </summary>
public class LoginPrompt
{
    /// <summary>
    /// Prompt identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Type of prompt ("text", "password", "button").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Label for the prompt.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// URL for button prompts.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Icon for the prompt.
    /// </summary>
    public string? Icon { get; set; }
}
