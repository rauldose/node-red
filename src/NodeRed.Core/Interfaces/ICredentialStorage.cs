// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Core.Interfaces;

/// <summary>
/// Interface for secure credential storage.
/// </summary>
public interface ICredentialStorage
{
    /// <summary>
    /// Gets credentials for a node.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>Dictionary of credential key-value pairs.</returns>
    Task<Dictionary<string, string>> GetAsync(string nodeId);

    /// <summary>
    /// Sets credentials for a node.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <param name="credentials">Dictionary of credential key-value pairs.</param>
    Task SetAsync(string nodeId, Dictionary<string, string> credentials);

    /// <summary>
    /// Deletes credentials for a node.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    Task DeleteAsync(string nodeId);

    /// <summary>
    /// Gets a single credential value.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <param name="key">The credential key.</param>
    /// <returns>The credential value or null if not found.</returns>
    Task<string?> GetValueAsync(string nodeId, string key);

    /// <summary>
    /// Sets a single credential value.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <param name="key">The credential key.</param>
    /// <param name="value">The credential value.</param>
    Task SetValueAsync(string nodeId, string key, string value);
}
