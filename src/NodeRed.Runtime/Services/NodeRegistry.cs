// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Interfaces;
using NodeRed.Runtime.Nodes;
using NodeRed.Runtime.Nodes.Common;
using NodeRed.Runtime.Nodes.Function;

namespace NodeRed.Runtime.Services;

/// <summary>
/// Registry for node types. Manages node type registration and instantiation.
/// </summary>
public class NodeRegistry : INodeRegistry
{
    private readonly Dictionary<string, Func<INode>> _nodeFactories = new();
    private readonly Dictionary<string, NodeDefinition> _definitions = new();

    public NodeRegistry()
    {
        RegisterBuiltInNodes();
    }

    private void RegisterBuiltInNodes()
    {
        // Common nodes
        Register<InjectNode>();
        Register<DebugNode>();
        Register<CommentNode>();
        Register<CatchNode>();

        // Function nodes
        Register<FunctionNode>();
        Register<ChangeNode>();
        Register<SwitchNode>();
        Register<DelayNode>();
        Register<TemplateNode>();
    }

    /// <inheritdoc />
    public void Register<TNode>() where TNode : class, INode, new()
    {
        var instance = new TNode();
        var definition = instance.Definition;
        
        _nodeFactories[definition.Type] = () => new TNode();
        _definitions[definition.Type] = definition;
    }

    /// <inheritdoc />
    public void Register(string type, Func<INode> factory)
    {
        var instance = factory();
        _nodeFactories[type] = factory;
        _definitions[type] = instance.Definition;
    }

    /// <inheritdoc />
    public IEnumerable<NodeDefinition> GetAllDefinitions()
    {
        return _definitions.Values;
    }

    /// <inheritdoc />
    public NodeDefinition? GetDefinition(string type)
    {
        return _definitions.GetValueOrDefault(type);
    }

    /// <inheritdoc />
    public INode? CreateNode(string type)
    {
        if (_nodeFactories.TryGetValue(type, out var factory))
        {
            return factory();
        }
        return null;
    }

    /// <inheritdoc />
    public bool IsRegistered(string type)
    {
        return _nodeFactories.ContainsKey(type);
    }
}
