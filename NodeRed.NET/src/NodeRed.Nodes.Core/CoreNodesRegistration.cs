// ============================================================
// NodeRed.Nodes.Core Registration
// Registers all core nodes with the node type registry
// ============================================================

using NodeRed.Nodes.Core.Common;
using NodeRed.Nodes.Core.Function;
using NodeRed.Nodes.Core.Network;
using NodeRed.Nodes.Core.Parsers;
using NodeRed.Nodes.Core.Sequence;
using NodeRed.Nodes.Core.Storage;

namespace NodeRed.Nodes.Core;

/// <summary>
/// Registers all core nodes with the node type registry.
/// Call RegisterAll() during application startup.
/// 
/// SOURCE: Node-RED registers nodes via RED.nodes.registerType() calls
/// in each node's JS file. This class consolidates all registrations.
/// </summary>
public static class CoreNodesRegistration
{
    private static bool _registered = false;
    
    /// <summary>
    /// Register all core nodes.
    /// This should be called once during application startup.
    /// </summary>
    public static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;
        
        // ============================================================
        // Common nodes (packages/node_modules/@node-red/nodes/core/common)
        // ============================================================
        
        // 05-junction.js
        JunctionNode.Register();
        
        // 20-inject.js
        InjectNode.Register();
        
        // 21-debug.js
        DebugNode.Register();
        
        // 24-complete.js
        CompleteNode.Register();
        
        // 25-catch.js
        CatchNode.Register();
        
        // 25-status.js
        StatusNode.Register();
        
        // 60-link.js
        LinkInNode.Register();
        LinkOutNode.Register();
        LinkCallNode.Register();
        
        // 90-comment.js
        CommentNode.Register();
        
        // ============================================================
        // Function nodes (packages/node_modules/@node-red/nodes/core/function)
        // ============================================================
        
        // 10-function.js
        FunctionNode.Register();
        
        // 10-switch.js
        SwitchNode.Register();
        
        // 15-change.js
        ChangeNode.Register();
        
        // 16-range.js
        RangeNode.Register();
        
        // 80-template.js
        TemplateNode.Register();
        
        // 89-delay.js
        DelayNode.Register();
        
        // 89-trigger.js
        TriggerNode.Register();
        
        // 90-exec.js
        ExecNode.Register();
        
        // ============================================================
        // Parser nodes (packages/node_modules/@node-red/nodes/core/parsers)
        // ============================================================
        
        // 70-JSON.js
        JsonNode.Register();
        
        // 70-CSV.js
        CsvNode.Register();
        
        // 70-XML.js
        XmlNode.Register();
        
        // 70-HTML.js
        HtmlNode.Register();
        
        // 70-YAML.js
        YamlNode.Register();
        
        // ============================================================
        // Sequence nodes (packages/node_modules/@node-red/nodes/core/sequence)
        // ============================================================
        
        // 17-split.js
        SplitNode.Register();
        JoinNode.Register();
        
        // 18-sort.js
        SortNode.Register();
        
        // 19-batch.js
        BatchNode.Register();
        
        // ============================================================
        // Storage nodes (packages/node_modules/@node-red/nodes/core/storage)
        // ============================================================
        
        // 10-file.js
        FileNode.Register();
        FileInNode.Register();
        
        // 23-watch.js
        WatchNode.Register();
        
        // ============================================================
        // Network nodes (packages/node_modules/@node-red/nodes/core/network)
        // ============================================================
        
        // 21-httprequest.js
        HttpRequestNode.Register();
        
        // 21-httpin.js
        HttpInNode.Register();
        HttpResponseNode.Register();
        
        // 31-tcpin.js
        TcpInNode.Register();
        
        // 32-udp.js
        UdpOutNode.Register();
        
        // 22-websocket.js
        WebSocketInNode.Register();
    }
    
    /// <summary>
    /// Get a summary of all registered node types.
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetNodeSummary()
    {
        return new Dictionary<string, string>
        {
            // Common
            ["inject"] = "Triggers flow execution with configurable payload",
            ["debug"] = "Displays messages in debug sidebar and/or console",
            ["complete"] = "Triggers when a node completes handling a message",
            ["catch"] = "Catches errors from other nodes",
            ["status"] = "Reports node status changes",
            ["link in"] = "Receives messages from Link Out nodes",
            ["link out"] = "Sends messages to Link In nodes",
            ["link call"] = "Calls a link in node and waits for return",
            ["comment"] = "Documentation comment (does nothing)",
            ["junction"] = "Simple pass-through junction point",
            
            // Function
            ["function"] = "Executes custom C# code",
            ["switch"] = "Routes messages based on rules",
            ["change"] = "Modifies message properties",
            ["range"] = "Maps input value to output range",
            ["template"] = "Generates output based on template",
            ["delay"] = "Delays messages",
            ["trigger"] = "Creates timed sequences",
            ["exec"] = "Executes system commands",
            
            // Parsers
            ["json"] = "Converts between JSON string and object",
            ["csv"] = "Parses and generates CSV data",
            ["xml"] = "Converts between XML string and object",
            ["html"] = "Extracts elements from HTML",
            ["yaml"] = "Converts between YAML string and object",
            
            // Sequence
            ["split"] = "Splits a message into a sequence",
            ["join"] = "Joins a sequence into a single message",
            ["sort"] = "Sorts array or message sequence",
            ["batch"] = "Groups messages into batches",
            
            // Storage
            ["file"] = "Writes to a file",
            ["file in"] = "Reads from a file",
            ["watch"] = "Watches for file changes",
            
            // Network
            ["http request"] = "Makes HTTP requests",
            ["http in"] = "Receives HTTP requests",
            ["http response"] = "Sends HTTP response",
            ["tcp in"] = "Receives TCP connections",
            ["udp out"] = "Sends UDP datagrams",
            ["websocket in"] = "Receives WebSocket messages"
        };
    }
}
