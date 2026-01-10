// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Services;

/// <summary>
/// In-memory credential storage with encryption.
/// For production, use a secure vault or database.
/// </summary>
public class InMemoryCredentialStorage : ICredentialStorage
{
    private readonly Dictionary<string, Dictionary<string, string>> _credentials = new();
    private readonly byte[] _encryptionKey;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public InMemoryCredentialStorage(string? encryptionKey = null)
    {
        // Use provided key or generate a random one
        if (!string.IsNullOrEmpty(encryptionKey))
        {
            _encryptionKey = DeriveKey(encryptionKey);
        }
        else
        {
            _encryptionKey = new byte[32];
            RandomNumberGenerator.Fill(_encryptionKey);
        }
    }

    /// <inheritdoc />
    public Task<Dictionary<string, string>> GetAsync(string nodeId)
    {
        if (_credentials.TryGetValue(nodeId, out var creds))
        {
            // Return a copy to prevent modification
            return Task.FromResult(new Dictionary<string, string>(creds));
        }
        return Task.FromResult(new Dictionary<string, string>());
    }

    /// <inheritdoc />
    public Task SetAsync(string nodeId, Dictionary<string, string> credentials)
    {
        _credentials[nodeId] = new Dictionary<string, string>(credentials);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string nodeId)
    {
        _credentials.Remove(nodeId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetValueAsync(string nodeId, string key)
    {
        if (_credentials.TryGetValue(nodeId, out var creds) && creds.TryGetValue(key, out var value))
        {
            return Task.FromResult<string?>(value);
        }
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc />
    public Task SetValueAsync(string nodeId, string key, string value)
    {
        if (!_credentials.ContainsKey(nodeId))
        {
            _credentials[nodeId] = new Dictionary<string, string>();
        }
        _credentials[nodeId][key] = value;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Encrypts a credential value.
    /// </summary>
    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to cipher text
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts a credential value.
    /// </summary>
    public string Decrypt(string cipherText)
    {
        var fullCipher = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;

        // Extract IV from beginning
        var iv = new byte[16];
        var cipher = new byte[fullCipher.Length - 16];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
        Buffer.BlockCopy(fullCipher, 16, cipher, 0, cipher.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Exports all credentials as encrypted JSON.
    /// </summary>
    public string ExportEncrypted()
    {
        var json = JsonSerializer.Serialize(_credentials, JsonOptions);
        return Encrypt(json);
    }

    /// <summary>
    /// Imports credentials from encrypted JSON.
    /// </summary>
    public void ImportEncrypted(string encryptedJson)
    {
        var json = Decrypt(encryptedJson);
        var imported = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json, JsonOptions);
        if (imported != null)
        {
            _credentials.Clear();
            foreach (var kvp in imported)
            {
                _credentials[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Derives a 256-bit key from a password.
    /// </summary>
    private static byte[] DeriveKey(string password)
    {
        // Use a fixed salt for simplicity; in production, use a random salt stored separately
        var salt = Encoding.UTF8.GetBytes("NodeRed.Credentials.Salt");
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32);
    }
}
