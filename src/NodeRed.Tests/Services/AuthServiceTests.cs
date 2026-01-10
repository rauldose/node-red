// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using FluentAssertions;
using NodeRed.Core.Entities;
using NodeRed.Core.Interfaces;
using NodeRed.Runtime.Services;
using Xunit;

namespace NodeRed.Tests.Services;

/// <summary>
/// Tests for authentication and authorization services.
/// </summary>
public class AuthServiceTests
{
    #region UserService Tests

    [Fact]
    public async Task UserService_AuthenticateAsync_ShouldReturnUser_WhenCredentialsValid()
    {
        // Arrange
        var userService = new InMemoryUserService();

        // Act - admin/admin is the default user
        var user = await userService.AuthenticateAsync("admin", "admin");

        // Assert
        user.Should().NotBeNull();
        user!.Username.Should().Be("admin");
    }

    [Fact]
    public async Task UserService_AuthenticateAsync_ShouldReturnNull_WhenCredentialsInvalid()
    {
        // Arrange
        var userService = new InMemoryUserService();

        // Act
        var user = await userService.AuthenticateAsync("admin", "wrongpassword");

        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task UserService_AuthenticateAsync_ShouldReturnNull_WhenUserNotFound()
    {
        // Arrange
        var userService = new InMemoryUserService();

        // Act
        var user = await userService.AuthenticateAsync("nonexistent", "password");

        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task UserService_CreateUserAsync_ShouldCreateUser()
    {
        // Arrange
        var userService = new InMemoryUserService();
        var newUser = new User
        {
            Username = "testuser",
            DisplayName = "Test User",
            Permissions = new List<string> { Permissions.FlowsRead }
        };

        // Act
        var createdUser = await userService.CreateUserAsync(newUser, "testpassword");

        // Assert
        createdUser.Should().NotBeNull();
        createdUser.Username.Should().Be("testuser");

        // Verify authentication works
        var authUser = await userService.AuthenticateAsync("testuser", "testpassword");
        authUser.Should().NotBeNull();
    }

    [Fact]
    public async Task UserService_CreateUserAsync_ShouldThrow_WhenUserExists()
    {
        // Arrange
        var userService = new InMemoryUserService();
        var user1 = new User { Username = "duplicate" };
        await userService.CreateUserAsync(user1, "password");

        // Act & Assert
        var user2 = new User { Username = "duplicate" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            userService.CreateUserAsync(user2, "password"));
    }

    [Fact]
    public async Task UserService_ChangePasswordAsync_ShouldUpdatePassword()
    {
        // Arrange
        var userService = new InMemoryUserService();
        var user = new User { Username = "changepass" };
        var created = await userService.CreateUserAsync(user, "oldpassword");

        // Act
        var result = await userService.ChangePasswordAsync(created.Id, "oldpassword", "newpassword");

        // Assert
        result.Should().BeTrue();

        // Verify new password works
        var authUser = await userService.AuthenticateAsync("changepass", "newpassword");
        authUser.Should().NotBeNull();

        // Verify old password no longer works
        var oldAuth = await userService.AuthenticateAsync("changepass", "oldpassword");
        oldAuth.Should().BeNull();
    }

    [Fact]
    public void UserService_HashPassword_ShouldProduceDifferentHashes()
    {
        // Arrange
        var userService = new InMemoryUserService();

        // Act
        var hash1 = userService.HashPassword("password");
        var hash2 = userService.HashPassword("password");

        // Assert - different salts should produce different hashes
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void UserService_VerifyPassword_ShouldVerifyCorrectPassword()
    {
        // Arrange
        var userService = new InMemoryUserService();
        var hash = userService.HashPassword("mypassword");

        // Act & Assert
        userService.VerifyPassword("mypassword", hash).Should().BeTrue();
        userService.VerifyPassword("wrongpassword", hash).Should().BeFalse();
    }

    #endregion

    #region TokenService Tests

    [Fact]
    public async Task TokenService_CreateTokenAsync_ShouldCreateToken()
    {
        // Arrange
        var userService = new InMemoryUserService();
        var tokenService = new InMemoryTokenService(userService);
        var user = await userService.GetUserByIdAsync("admin");

        // Act
        var token = await tokenService.CreateTokenAsync(user!, "test-client");

        // Assert
        token.Should().NotBeNull();
        token.AccessToken.Should().NotBeNullOrEmpty();
        token.RefreshToken.Should().NotBeNullOrEmpty();
        token.TokenType.Should().Be("Bearer");
        token.UserId.Should().Be("admin");
        token.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task TokenService_ValidateTokenAsync_ShouldReturnUser()
    {
        // Arrange
        var userService = new InMemoryUserService();
        var tokenService = new InMemoryTokenService(userService);
        var user = await userService.GetUserByIdAsync("admin");
        var token = await tokenService.CreateTokenAsync(user!, "test-client");

        // Act
        var validatedUser = await tokenService.ValidateTokenAsync(token.AccessToken);

        // Assert
        validatedUser.Should().NotBeNull();
        validatedUser!.Id.Should().Be("admin");
    }

    [Fact]
    public async Task TokenService_ValidateTokenAsync_ShouldReturnNull_WhenTokenInvalid()
    {
        // Arrange
        var userService = new InMemoryUserService();
        var tokenService = new InMemoryTokenService(userService);

        // Act
        var user = await tokenService.ValidateTokenAsync("invalid-token");

        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task TokenService_RevokeTokenAsync_ShouldInvalidateToken()
    {
        // Arrange
        var userService = new InMemoryUserService();
        var tokenService = new InMemoryTokenService(userService);
        var user = await userService.GetUserByIdAsync("admin");
        var token = await tokenService.CreateTokenAsync(user!, "test-client");

        // Act
        await tokenService.RevokeTokenAsync(token.AccessToken);

        // Assert
        var validatedUser = await tokenService.ValidateTokenAsync(token.AccessToken);
        validatedUser.Should().BeNull();
    }

    [Fact]
    public async Task TokenService_RefreshTokenAsync_ShouldCreateNewToken()
    {
        // Arrange
        var userService = new InMemoryUserService();
        var tokenService = new InMemoryTokenService(userService);
        var user = await userService.GetUserByIdAsync("admin");
        var originalToken = await tokenService.CreateTokenAsync(user!, "test-client");

        // Act
        var newToken = await tokenService.RefreshTokenAsync(originalToken.RefreshToken!);

        // Assert
        newToken.Should().NotBeNull();
        newToken!.AccessToken.Should().NotBe(originalToken.AccessToken);

        // Original token should be revoked
        var oldUser = await tokenService.ValidateTokenAsync(originalToken.AccessToken);
        oldUser.Should().BeNull();

        // New token should be valid
        var newUser = await tokenService.ValidateTokenAsync(newToken.AccessToken);
        newUser.Should().NotBeNull();
    }

    #endregion

    #region PermissionService Tests

    [Fact]
    public void PermissionService_HasPermission_ShouldReturnTrue_WhenExactMatch()
    {
        // Arrange
        var service = new PermissionService();
        var scopes = new List<string> { Permissions.FlowsRead, Permissions.FlowsWrite };

        // Act & Assert
        service.HasPermission(scopes, Permissions.FlowsRead).Should().BeTrue();
        service.HasPermission(scopes, Permissions.FlowsWrite).Should().BeTrue();
    }

    [Fact]
    public void PermissionService_HasPermission_ShouldReturnFalse_WhenNoMatch()
    {
        // Arrange
        var service = new PermissionService();
        var scopes = new List<string> { Permissions.FlowsRead };

        // Act & Assert
        service.HasPermission(scopes, Permissions.FlowsWrite).Should().BeFalse();
        service.HasPermission(scopes, Permissions.NodesWrite).Should().BeFalse();
    }

    [Fact]
    public void PermissionService_HasPermission_ShouldReturnTrue_WhenFullAccess()
    {
        // Arrange
        var service = new PermissionService();
        var scopes = new List<string> { Permissions.FullAccess };

        // Act & Assert
        service.HasPermission(scopes, Permissions.FlowsRead).Should().BeTrue();
        service.HasPermission(scopes, Permissions.FlowsWrite).Should().BeTrue();
        service.HasPermission(scopes, Permissions.NodesWrite).Should().BeTrue();
    }

    [Fact]
    public void PermissionService_HasPermission_ShouldReturnTrue_ForWildcard()
    {
        // Arrange
        var service = new PermissionService();
        var scopes = new List<string> { "flows.*" };

        // Act & Assert
        service.HasPermission(scopes, Permissions.FlowsRead).Should().BeTrue();
        service.HasPermission(scopes, Permissions.FlowsWrite).Should().BeTrue();
        service.HasPermission(scopes, Permissions.NodesRead).Should().BeFalse();
    }

    [Fact]
    public void PermissionService_GetRequiredPermission_ShouldReturnCorrectPermission()
    {
        // Arrange
        var service = new PermissionService();

        // Act & Assert
        service.GetRequiredPermission("GET", "/flows").Should().Be("flows.read");
        service.GetRequiredPermission("POST", "/flows").Should().Be("flows.write");
        service.GetRequiredPermission("PUT", "/flows").Should().Be("flows.write");
        service.GetRequiredPermission("DELETE", "/flows").Should().Be("flows.write");
        service.GetRequiredPermission("GET", "/nodes").Should().Be("nodes.read");
    }

    #endregion
}
