// ============================================================
// SOURCE: packages/node_modules/@node-red/nodes/core/common/*.js
// TRANSLATION: Additional common nodes - Complete, Catch, Status, Link, Comment
// ============================================================

using System.Text.Json;
using System.Text.Json.Serialization;
using NodeRed.Util;

namespace NodeRed.Nodes.Core.Common;

#region Complete Node
/// <summary>
/// Complete node configuration
/// SOURCE: 24-complete.js
/// </summary>
public class CompleteNodeConfig : NodeConfig
{
    [JsonPropertyName("scope")]
    public List<string>? Scope { get; set; }
    
    [JsonPropertyName("uncaught")]
    public bool Uncaught { get; set; }
}

/// <summary>
/// Complete node - Triggers when a node completes handling a message
/// SOURCE: packages/node_modules/@node-red/nodes/core/common/24-complete.js
/// </summary>
public class CompleteNode : BaseNode
{
    private readonly HashSet<string> _scope;
    private readonly bool _uncaught;
    
    public CompleteNode(NodeConfig config) : base(config)
    {
        var completeConfig = config as CompleteNodeConfig ?? new CompleteNodeConfig();
        _scope = new HashSet<string>(completeConfig.Scope ?? new List<string>());
        _uncaught = completeConfig.Uncaught;
        
        // Listen for node complete events
        Events.Instance.On("node:complete", (sender, args) =>
        {
            if (args is Events.NodeRedEventArgs nrArgs && nrArgs.Data is Dictionary<string, object> data &&
                data.TryGetValue("nodeId", out var nodeId) &&
                data.TryGetValue("msg", out var msg))
            {
                var id = nodeId?.ToString() ?? "";
                if (_scope.Count == 0 || _scope.Contains(id))
                {
                    if (msg is FlowMessage flowMsg)
                    {
                        _ = ReceiveAsync(flowMsg.Clone());
                    }
                }
            }
        });
        
        OnInput(async (msg, send, done) =>
        {
            send(msg);
            done(null);
        });
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("complete", config => new CompleteNode(config));
    }
}
#endregion

#region Catch Node
/// <summary>
/// Catch node configuration
/// SOURCE: 25-catch.js
/// </summary>
public class CatchNodeConfig : NodeConfig
{
    [JsonPropertyName("scope")]
    public List<string>? Scope { get; set; }
    
    [JsonPropertyName("uncaught")]
    public bool Uncaught { get; set; }
}

/// <summary>
/// Catch node - Catches errors from other nodes
/// SOURCE: packages/node_modules/@node-red/nodes/core/common/25-catch.js
/// </summary>
public class CatchNode : BaseNode
{
    private readonly HashSet<string> _scope;
    private readonly bool _uncaught;
    
    public CatchNode(NodeConfig config) : base(config)
    {
        var catchConfig = config as CatchNodeConfig ?? new CatchNodeConfig();
        _scope = new HashSet<string>(catchConfig.Scope ?? new List<string>());
        _uncaught = catchConfig.Uncaught;
        
        // Listen for node error events
        Events.Instance.On("node:error", (sender, args) =>
        {
            if (args is Events.NodeRedEventArgs nrArgs && nrArgs.Data is Dictionary<string, object> data &&
                data.TryGetValue("nodeId", out var nodeId) &&
                data.TryGetValue("error", out var error) &&
                data.TryGetValue("msg", out var msg))
            {
                var id = nodeId?.ToString() ?? "";
                var isCaught = data.TryGetValue("caught", out var c) && c is true;
                
                if ((_uncaught && !isCaught) || (!_uncaught && (_scope.Count == 0 || _scope.Contains(id))))
                {
                    var errorMsg = msg is FlowMessage flowMsg ? flowMsg.Clone() : new FlowMessage();
                    
                    // Add error info to message
                    errorMsg.AdditionalProperties ??= new Dictionary<string, JsonElement>();
                    var errorInfo = new { message = error?.ToString(), source = new { id, type = "node" } };
                    errorMsg.AdditionalProperties["error"] = JsonSerializer.Deserialize<JsonElement>(
                        JsonSerializer.Serialize(errorInfo));
                    
                    _ = ReceiveAsync(errorMsg);
                }
            }
        });
        
        OnInput(async (msg, send, done) =>
        {
            send(msg);
            done(null);
        });
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("catch", config => new CatchNode(config));
    }
}
#endregion

#region Status Node
/// <summary>
/// Status node configuration
/// SOURCE: 25-status.js
/// </summary>
public class StatusNodeConfig : NodeConfig
{
    [JsonPropertyName("scope")]
    public List<string>? Scope { get; set; }
}

/// <summary>
/// Status node - Reports node status changes
/// SOURCE: packages/node_modules/@node-red/nodes/core/common/25-status.js
/// </summary>
public class StatusNode : BaseNode
{
    private readonly HashSet<string> _scope;
    
    public StatusNode(NodeConfig config) : base(config)
    {
        var statusConfig = config as StatusNodeConfig ?? new StatusNodeConfig();
        _scope = new HashSet<string>(statusConfig.Scope ?? new List<string>());
        
        // Listen for node status events
        Events.Instance.On("node:status", (sender, args) =>
        {
            if (args is Events.NodeRedEventArgs nrArgs && nrArgs.Data is Dictionary<string, object> data &&
                data.TryGetValue("nodeId", out var nodeId) &&
                data.TryGetValue("status", out var status))
            {
                var id = nodeId?.ToString() ?? "";
                if (_scope.Count == 0 || _scope.Contains(id))
                {
                    var msg = new FlowMessage
                    {
                        Payload = status,
                        AdditionalProperties = new Dictionary<string, JsonElement>()
                    };
                    
                    var statusInfo = new { source = new { id, type = "node" }, status };
                    msg.AdditionalProperties["status"] = JsonSerializer.Deserialize<JsonElement>(
                        JsonSerializer.Serialize(statusInfo));
                    
                    _ = ReceiveAsync(msg);
                }
            }
        });
        
        OnInput(async (msg, send, done) =>
        {
            send(msg);
            done(null);
        });
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("status", config => new StatusNode(config));
    }
}
#endregion

#region Link Nodes
/// <summary>
/// Link In node configuration
/// SOURCE: 60-link.js
/// </summary>
public class LinkInNodeConfig : NodeConfig
{
    [JsonPropertyName("links")]
    public List<string>? Links { get; set; }
}

/// <summary>
/// Link In node - Receives messages from Link Out nodes
/// SOURCE: packages/node_modules/@node-red/nodes/core/common/60-link.js
/// </summary>
public class LinkInNode : BaseNode
{
    private static readonly Dictionary<string, LinkInNode> _linkInNodes = new();
    
    public LinkInNode(NodeConfig config) : base(config)
    {
        _linkInNodes[Id] = this;
        
        OnInput(async (msg, send, done) =>
        {
            send(msg);
            done(null);
        });
    }
    
    public static LinkInNode? GetById(string id)
    {
        return _linkInNodes.TryGetValue(id, out var node) ? node : null;
    }
    
    public override async Task CloseAsync(bool removed = false)
    {
        _linkInNodes.Remove(Id);
        await base.CloseAsync(removed);
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("link in", config => new LinkInNode(config));
    }
}

/// <summary>
/// Link Out node configuration
/// SOURCE: 60-link.js
/// </summary>
public class LinkOutNodeConfig : NodeConfig
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "link"; // "link" or "return"
    
    [JsonPropertyName("links")]
    public List<string>? Links { get; set; }
}

/// <summary>
/// Link Out node - Sends messages to Link In nodes
/// SOURCE: packages/node_modules/@node-red/nodes/core/common/60-link.js
/// </summary>
public class LinkOutNode : BaseNode
{
    private readonly List<string> _links;
    private readonly string _mode;
    
    public LinkOutNode(NodeConfig config) : base(config)
    {
        var linkConfig = config as LinkOutNodeConfig ?? new LinkOutNodeConfig();
        _links = linkConfig.Links ?? new List<string>();
        _mode = linkConfig.Mode ?? "link";
        
        OnInput(async (msg, send, done) =>
        {
            if (_mode == "return")
            {
                // Return mode - send back through link call stack
                // This would require tracking the call stack in msg._linkSource
                send(msg);
            }
            else
            {
                // Link mode - send to linked nodes
                foreach (var linkId in _links)
                {
                    var linkIn = LinkInNode.GetById(linkId);
                    if (linkIn != null)
                    {
                        await linkIn.ReceiveAsync(msg.Clone());
                    }
                }
            }
            done(null);
        });
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("link out", config => new LinkOutNode(config));
    }
}

/// <summary>
/// Link Call node - Calls a link in node and waits for return
/// SOURCE: packages/node_modules/@node-red/nodes/core/common/60-link.js
/// </summary>
public class LinkCallNode : BaseNode
{
    private readonly List<string> _links;
    private readonly int _timeout;
    
    public LinkCallNode(NodeConfig config) : base(config)
    {
        var linkConfig = config as LinkOutNodeConfig ?? new LinkOutNodeConfig();
        _links = linkConfig.Links ?? new List<string>();
        _timeout = config.GetProperty("timeout", 30) * 1000;
        
        OnInput(async (msg, send, done) =>
        {
            // For a full implementation, we'd need to track the call and wait for return
            // This is a simplified version that just forwards to link in nodes
            foreach (var linkId in _links)
            {
                var linkIn = LinkInNode.GetById(linkId);
                if (linkIn != null)
                {
                    var callMsg = msg.Clone();
                    // Add link source for return routing
                    callMsg.AdditionalProperties ??= new Dictionary<string, JsonElement>();
                    callMsg.AdditionalProperties["_linkSource"] = JsonSerializer.Deserialize<JsonElement>(
                        JsonSerializer.Serialize(new { id = Id }));
                    
                    await linkIn.ReceiveAsync(callMsg);
                }
            }
            done(null);
        });
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("link call", config => new LinkCallNode(config));
    }
}
#endregion

#region Comment Node
/// <summary>
/// Comment node - Does nothing, just for documentation
/// SOURCE: packages/node_modules/@node-red/nodes/core/common/90-comment.js
/// </summary>
public class CommentNode : BaseNode
{
    public CommentNode(NodeConfig config) : base(config)
    {
        // Comment node does nothing - it's just for documentation
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("comment", config => new CommentNode(config));
    }
}
#endregion

#region Junction Node
/// <summary>
/// Junction node - Simple pass-through junction point
/// SOURCE: packages/node_modules/@node-red/nodes/core/common/05-junction.js
/// </summary>
public class JunctionNode : BaseNode
{
    public JunctionNode(NodeConfig config) : base(config)
    {
        OnInput(async (msg, send, done) =>
        {
            send(msg);
            done(null);
        });
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("junction", config => new JunctionNode(config));
    }
}
#endregion
