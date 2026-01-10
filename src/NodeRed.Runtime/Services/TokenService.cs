// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Security.Cryptography;
using NodeRed.Core.Entities;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Services;

/// <summary>
/// In-memory implementation of token management.
/// For production, use a database-backed implementation with proper token encryption.
/// </summary>
public class InMemoryTokenService : ITokenService
{
    private readonly Dictionary<string, AuthToken> _tokens = new();
    private readonly Dictionary<string, AuthToken> _refreshTokens = new();
    private readonly IUserService _userService;
    private readonly object _lock = new();

    /// <summary>
    /// Token expiration time in hours.
    /// </summary>
    public int AccessTokenExpirationHours { get; set; } = 24;

    /// <summary>
    /// Refresh token expiration time in days.
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 30;

    public InMemoryTokenService(IUserService userService)
    {
        _userService = userService;
    }

    /// <inheritdoc />
    public Task<AuthToken> CreateTokenAsync(User user, string clientId, IEnumerable<string>? scopes = null)
    {
        lock (_lock)
        {
            var token = new AuthToken
            {
                AccessToken = GenerateToken(),
                RefreshToken = GenerateToken(),
                TokenType = "Bearer",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(AccessTokenExpirationHours),
                UserId = user.Id,
                ClientId = clientId,
                Scopes = scopes?.ToList() ?? user.Permissions
            };

            _tokens[token.AccessToken] = token;
            _refreshTokens[token.RefreshToken!] = token;

            return Task.FromResult(token);
        }
    }

    /// <inheritdoc />
    public async Task<User?> ValidateTokenAsync(string accessToken)
    {
        lock (_lock)
        {
            if (!_tokens.TryGetValue(accessToken, out var token))
            {
                return null;
            }

            if (token.Revoked || token.ExpiresAt < DateTimeOffset.UtcNow)
            {
                _tokens.Remove(accessToken);
                return null;
            }

            // Token is valid, get the user
            return _userService.GetUserByIdAsync(token.UserId).Result;
        }
    }

    /// <inheritdoc />
    public Task RevokeTokenAsync(string accessToken)
    {
        lock (_lock)
        {
            if (_tokens.TryGetValue(accessToken, out var token))
            {
                token.Revoked = true;
                _tokens.Remove(accessToken);

                if (token.RefreshToken != null)
                {
                    _refreshTokens.Remove(token.RefreshToken);
                }
            }

            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public Task RevokeAllUserTokensAsync(string userId)
    {
        lock (_lock)
        {
            var tokensToRemove = _tokens.Values
                .Where(t => t.UserId == userId)
                .ToList();

            foreach (var token in tokensToRemove)
            {
                token.Revoked = true;
                _tokens.Remove(token.AccessToken);
                if (token.RefreshToken != null)
                {
                    _refreshTokens.Remove(token.RefreshToken);
                }
            }

            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public async Task<AuthToken?> RefreshTokenAsync(string refreshToken)
    {
        AuthToken? oldToken;
        lock (_lock)
        {
            if (!_refreshTokens.TryGetValue(refreshToken, out oldToken))
            {
                return null;
            }

            if (oldToken.Revoked)
            {
                return null;
            }

            // Revoke old token
            oldToken.Revoked = true;
            _tokens.Remove(oldToken.AccessToken);
            _refreshTokens.Remove(refreshToken);
        }

        // Get user and create new token
        var user = await _userService.GetUserByIdAsync(oldToken.UserId);
        if (user == null)
        {
            return null;
        }

        return await CreateTokenAsync(user, oldToken.ClientId, oldToken.Scopes);
    }

    /// <inheritdoc />
    public Task CleanupExpiredTokensAsync()
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            var expiredTokens = _tokens.Values
                .Where(t => t.ExpiresAt < now || t.Revoked)
                .ToList();

            foreach (var token in expiredTokens)
            {
                _tokens.Remove(token.AccessToken);
                if (token.RefreshToken != null)
                {
                    _refreshTokens.Remove(token.RefreshToken);
                }
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Generates a cryptographically secure random token.
    /// </summary>
    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
