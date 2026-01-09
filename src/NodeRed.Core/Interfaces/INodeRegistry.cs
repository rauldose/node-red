// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;

namespace NodeRed.Core.Interfaces;

/// <summary>
/// Registry for node types.
/// </summary>
public interface INodeRegistry
{
    /// <summary>
    /// Registers a node type.
    /// </summary>
    /// <typeparam name="TNode">The node implementation type.</typeparam>
    void Register<TNode>() where TNode : class, INode, new();

    /// <summary>
    /// Registers a node type with a factory.
    /// </summary>
    /// <param name="type">The node type identifier.</param>
    /// <param name="factory">Factory function to create node instances.</param>
    void Register(string type, Func<INode> factory);

    /// <summary>
    /// Gets all registered node definitions.
    /// </summary>
    IEnumerable<NodeDefinition> GetAllDefinitions();

    /// <summary>
    /// Gets a node definition by type.
    /// </summary>
    /// <param name="type">The node type identifier.</param>
    NodeDefinition? GetDefinition(string type);

    /// <summary>
    /// Creates an instance of a node.
    /// </summary>
    /// <param name="type">The node type identifier.</param>
    INode? CreateNode(string type);

    /// <summary>
    /// Checks if a node type is registered.
    /// </summary>
    /// <param name="type">The node type identifier.</param>
    bool IsRegistered(string type);
}
