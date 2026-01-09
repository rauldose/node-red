// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Interfaces;
using NodeRed.Runtime.Nodes.SDK.Common;
using NodeRed.Runtime.Nodes.SDK.Function;
using NodeRed.Runtime.Nodes.SDK.Network;
using NodeRed.Runtime.Nodes.SDK.Parser;
using NodeRed.Runtime.Nodes.SDK.Sequence;
using NodeRed.Runtime.Nodes.SDK.Storage;
using NodeRed.Runtime.Nodes.SDK.Database;

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
        Register<CompleteNode>();
        Register<StatusNode>();
        Register<LinkInNode>();
        Register<LinkOutNode>();
        Register<LinkCallNode>();
        Register<JunctionNode>();
        Register<GlobalConfigNode>();
        Register<UnknownNode>();

        // Function nodes
        Register<FunctionNode>();
        Register<ChangeNode>();
        Register<SwitchNode>();
        Register<DelayNode>();
        Register<TemplateNode>();
        Register<RangeNode>();
        Register<TriggerNode>();
        Register<ExecNode>();
        Register<RbeNode>();

        // Network nodes
        Register<HttpInNode>();
        Register<HttpResponseNode>();
        Register<HttpRequestNode>();
        Register<HttpProxyNode>();
        Register<MqttInNode>();
        Register<MqttOutNode>();
        Register<WebSocketInNode>();
        Register<WebSocketOutNode>();
        Register<TcpInNode>();
        Register<TcpOutNode>();
        Register<UdpInNode>();
        Register<UdpOutNode>();
        Register<TlsConfigNode>();

        // Parser nodes
        Register<JsonNode>();
        Register<XmlNode>();
        Register<CsvNode>();
        Register<HtmlNode>();
        Register<YamlNode>();

        // Sequence nodes
        Register<SplitNode>();
        Register<JoinNode>();
        Register<SortNode>();
        Register<BatchNode>();

        // Storage nodes
        Register<FileNode>();
        Register<FileInNode>();
        Register<WatchNode>();

        // Database nodes
        Register<SqlServerNode>();
        Register<PostgresNode>();
        Register<MySqlNode>();
        Register<SqliteNode>();
        Register<MongoDbNode>();
        Register<RedisNode>();
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
