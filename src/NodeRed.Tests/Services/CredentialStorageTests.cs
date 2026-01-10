// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using FluentAssertions;
using NodeRed.Runtime.Services;
using Xunit;

namespace NodeRed.Tests.Services;

/// <summary>
/// Tests for InMemoryCredentialStorage - encrypted credential storage.
/// </summary>
public class CredentialStorageTests
{
    #region Basic Storage Tests

    [Fact]
    public async Task GetAsync_ShouldReturnEmpty_WhenNoCredentials()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage();

        // Act
        var credentials = await storage.GetAsync("unknown-node");

        // Assert
        credentials.Should().BeEmpty();
    }

    [Fact]
    public async Task SetAsync_ShouldStoreCredentials()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage();
        var credentials = new Dictionary<string, string>
        {
            { "username", "admin" },
            { "password", "secret123" }
        };

        // Act
        await storage.SetAsync("node-1", credentials);
        var retrieved = await storage.GetAsync("node-1");

        // Assert
        retrieved.Should().HaveCount(2);
        retrieved["username"].Should().Be("admin");
        retrieved["password"].Should().Be("secret123");
    }

    [Fact]
    public async Task SetAsync_ShouldOverwriteExisting()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage();
        await storage.SetAsync("node-1", new Dictionary<string, string> { { "key", "old-value" } });

        // Act
        await storage.SetAsync("node-1", new Dictionary<string, string> { { "key", "new-value" } });
        var retrieved = await storage.GetAsync("node-1");

        // Assert
        retrieved["key"].Should().Be("new-value");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveCredentials()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage();
        await storage.SetAsync("node-1", new Dictionary<string, string> { { "key", "value" } });

        // Act
        await storage.DeleteAsync("node-1");
        var retrieved = await storage.GetAsync("node-1");

        // Assert
        retrieved.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_ShouldNotThrow_WhenNodeNotExists()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage();

        // Act & Assert - should not throw
        await storage.DeleteAsync("nonexistent-node");
    }

    #endregion

    #region Single Value Tests

    [Fact]
    public async Task GetValueAsync_ShouldReturnValue_WhenExists()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage();
        await storage.SetAsync("node-1", new Dictionary<string, string> { { "api-key", "abc123" } });

        // Act
        var value = await storage.GetValueAsync("node-1", "api-key");

        // Assert
        value.Should().Be("abc123");
    }

    [Fact]
    public async Task GetValueAsync_ShouldReturnNull_WhenKeyNotExists()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage();
        await storage.SetAsync("node-1", new Dictionary<string, string> { { "key", "value" } });

        // Act
        var value = await storage.GetValueAsync("node-1", "nonexistent-key");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public async Task GetValueAsync_ShouldReturnNull_WhenNodeNotExists()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage();

        // Act
        var value = await storage.GetValueAsync("nonexistent-node", "key");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public async Task SetValueAsync_ShouldAddValue_ToNewNode()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage();

        // Act
        await storage.SetValueAsync("node-1", "token", "secret-token");
        var value = await storage.GetValueAsync("node-1", "token");

        // Assert
        value.Should().Be("secret-token");
    }

    [Fact]
    public async Task SetValueAsync_ShouldAddValue_ToExistingNode()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage();
        await storage.SetAsync("node-1", new Dictionary<string, string> { { "existing", "value" } });

        // Act
        await storage.SetValueAsync("node-1", "new-key", "new-value");
        var existing = await storage.GetValueAsync("node-1", "existing");
        var newValue = await storage.GetValueAsync("node-1", "new-key");

        // Assert
        existing.Should().Be("value");
        newValue.Should().Be("new-value");
    }

    #endregion

    #region Encryption Tests

    [Fact]
    public void Encrypt_ShouldProduceDifferentOutput_ForSameInput()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage("test-password");

        // Act
        var encrypted1 = storage.Encrypt("secret");
        var encrypted2 = storage.Encrypt("secret");

        // Assert - different IVs should produce different ciphertexts
        encrypted1.Should().NotBe(encrypted2);
    }

    [Fact]
    public void Decrypt_ShouldRecoverOriginalText()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage("test-password");
        var original = "my-secret-password-123!@#";

        // Act
        var encrypted = storage.Encrypt(original);
        var decrypted = storage.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(original);
    }

    [Fact]
    public void Encrypt_ShouldHandleEmptyString()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage("test-password");

        // Act
        var encrypted = storage.Encrypt("");
        var decrypted = storage.Decrypt(encrypted);

        // Assert
        decrypted.Should().BeEmpty();
    }

    [Fact]
    public void Encrypt_ShouldHandleUnicodeCharacters()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage("test-password");
        var original = "密码 パスワード 🔐";

        // Act
        var encrypted = storage.Encrypt(original);
        var decrypted = storage.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(original);
    }

    [Fact]
    public void Encrypt_ShouldHandleLongText()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage("test-password");
        var original = new string('x', 10000);

        // Act
        var encrypted = storage.Encrypt(original);
        var decrypted = storage.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(original);
    }

    #endregion

    #region Export/Import Tests

    [Fact]
    public void ExportEncrypted_ShouldProduceBase64String()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage("test-password");
        storage.SetAsync("node-1", new Dictionary<string, string> { { "key", "value" } }).Wait();

        // Act
        var exported = storage.ExportEncrypted();

        // Assert
        exported.Should().NotBeNullOrEmpty();
        // Should be valid base64
        var action = () => Convert.FromBase64String(exported);
        action.Should().NotThrow();
    }

    [Fact]
    public void ImportEncrypted_ShouldRestoreCredentials()
    {
        // Arrange
        var storage1 = new InMemoryCredentialStorage("test-password");
        storage1.SetAsync("node-1", new Dictionary<string, string> 
        { 
            { "username", "admin" },
            { "password", "secret" }
        }).Wait();
        var exported = storage1.ExportEncrypted();

        // Act
        var storage2 = new InMemoryCredentialStorage("test-password");
        storage2.ImportEncrypted(exported);

        // Assert
        var credentials = storage2.GetAsync("node-1").Result;
        credentials.Should().HaveCount(2);
        credentials["username"].Should().Be("admin");
        credentials["password"].Should().Be("secret");
    }

    [Fact]
    public void ImportEncrypted_ShouldClearExisting()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage("test-password");
        storage.SetAsync("old-node", new Dictionary<string, string> { { "key", "value" } }).Wait();

        var exportStorage = new InMemoryCredentialStorage("test-password");
        exportStorage.SetAsync("new-node", new Dictionary<string, string> { { "key", "value" } }).Wait();
        var exported = exportStorage.ExportEncrypted();

        // Act
        storage.ImportEncrypted(exported);

        // Assert
        var oldNode = storage.GetAsync("old-node").Result;
        var newNode = storage.GetAsync("new-node").Result;
        oldNode.Should().BeEmpty();
        newNode.Should().NotBeEmpty();
    }

    #endregion

    #region Isolation Tests

    [Fact]
    public async Task GetAsync_ShouldReturnCopy_NotReference()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage();
        await storage.SetAsync("node-1", new Dictionary<string, string> { { "key", "value" } });

        // Act
        var retrieved = await storage.GetAsync("node-1");
        retrieved["key"] = "modified";
        var retrievedAgain = await storage.GetAsync("node-1");

        // Assert - original should not be modified
        retrievedAgain["key"].Should().Be("value");
    }

    [Fact]
    public async Task CredentialsByNode_ShouldBeIsolated()
    {
        // Arrange
        var storage = new InMemoryCredentialStorage();
        await storage.SetAsync("node-1", new Dictionary<string, string> { { "key", "value-1" } });
        await storage.SetAsync("node-2", new Dictionary<string, string> { { "key", "value-2" } });

        // Act
        var node1Creds = await storage.GetAsync("node-1");
        var node2Creds = await storage.GetAsync("node-2");

        // Assert
        node1Creds["key"].Should().Be("value-1");
        node2Creds["key"].Should().Be("value-2");
    }

    #endregion
}
