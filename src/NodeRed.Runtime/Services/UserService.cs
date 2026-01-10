// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Security.Cryptography;
using NodeRed.Core.Entities;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Services;

/// <summary>
/// In-memory implementation of user management for development.
/// For production, use a database-backed implementation.
/// </summary>
public class InMemoryUserService : IUserService
{
    private readonly Dictionary<string, User> _users = new();
    private readonly object _lock = new();

    public InMemoryUserService()
    {
        // Create default admin user
        var adminUser = new User
        {
            Id = "admin",
            Username = "admin",
            DisplayName = "Administrator",
            Permissions = new List<string> { Permissions.FullAccess },
            Enabled = true
        };
        adminUser.PasswordHash = HashPassword("admin");
        _users[adminUser.Id] = adminUser;
    }

    /// <inheritdoc />
    public Task<User?> AuthenticateAsync(string username, string password)
    {
        lock (_lock)
        {
            var user = _users.Values.FirstOrDefault(u => 
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && u.Enabled);

            if (user == null)
            {
                return Task.FromResult<User?>(null);
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                return Task.FromResult<User?>(null);
            }

            user.LastLoginAt = DateTimeOffset.UtcNow;
            return Task.FromResult<User?>(user);
        }
    }

    /// <inheritdoc />
    public Task<User?> GetUserByIdAsync(string userId)
    {
        lock (_lock)
        {
            _users.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }
    }

    /// <inheritdoc />
    public Task<User?> GetUserByUsernameAsync(string username)
    {
        lock (_lock)
        {
            var user = _users.Values.FirstOrDefault(u => 
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(user);
        }
    }

    /// <inheritdoc />
    public Task<User> CreateUserAsync(User user, string password)
    {
        lock (_lock)
        {
            if (_users.ContainsKey(user.Id))
            {
                throw new InvalidOperationException($"User with ID {user.Id} already exists");
            }

            if (_users.Values.Any(u => u.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"User with username {user.Username} already exists");
            }

            user.PasswordHash = HashPassword(password);
            user.CreatedAt = DateTimeOffset.UtcNow;
            _users[user.Id] = user;
            return Task.FromResult(user);
        }
    }

    /// <inheritdoc />
    public Task UpdateUserAsync(User user)
    {
        lock (_lock)
        {
            if (!_users.ContainsKey(user.Id))
            {
                throw new InvalidOperationException($"User with ID {user.Id} not found");
            }

            _users[user.Id] = user;
            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public Task DeleteUserAsync(string userId)
    {
        lock (_lock)
        {
            _users.Remove(userId);
            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public Task<IEnumerable<User>> GetAllUsersAsync()
    {
        lock (_lock)
        {
            return Task.FromResult<IEnumerable<User>>(_users.Values.ToList());
        }
    }

    /// <inheritdoc />
    public Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(userId, out var user))
            {
                return Task.FromResult(false);
            }

            if (!VerifyPassword(currentPassword, user.PasswordHash))
            {
                return Task.FromResult(false);
            }

            user.PasswordHash = HashPassword(newPassword);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        try
        {
            var parts = passwordHash.Split(':');
            if (parts.Length != 3)
            {
                return false;
            }

            var iterations = int.Parse(parts[0]);
            var salt = Convert.FromBase64String(parts[1]);
            var storedHash = Convert.FromBase64String(parts[2]);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            var computedHash = pbkdf2.GetBytes(32);

            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public string HashPassword(string password)
    {
        const int iterations = 100000;
        var salt = RandomNumberGenerator.GetBytes(16);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);

        return $"{iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }
}
