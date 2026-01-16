// ============================================================
// SOURCE: packages/node_modules/@node-red/nodes/core/function/10-function.js
// TRANSLATION: Function node - executes JavaScript/C# code
// ============================================================
// ORIGINAL CODE (key sections):
// ------------------------------------------------------------
// function FunctionNode(n) {
//     RED.nodes.createNode(this,n);
//     node.func = n.func;
//     node.outputs = n.outputs;
//     ...
//     this.on("input", function(msg, send, done) { ... });
// }
// RED.nodes.registerType("function", FunctionNode);
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace NodeRed.Nodes.Core.Function;

/// <summary>
/// Function node configuration
/// SOURCE: FunctionNode constructor parameters
/// </summary>
public class FunctionNodeConfig : NodeConfig
{
    [JsonPropertyName("func")]
    public string Func { get; set; } = "return msg;";
    
    [JsonPropertyName("outputs")]
    public int Outputs { get; set; } = 1;
    
    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 0;
    
    [JsonPropertyName("initialize")]
    public string? Initialize { get; set; }
    
    [JsonPropertyName("finalize")]
    public string? Finalize { get; set; }
    
    [JsonPropertyName("libs")]
    public List<FunctionLibrary>? Libs { get; set; }
}

/// <summary>
/// External library reference for function node
/// </summary>
public class FunctionLibrary
{
    [JsonPropertyName("var")]
    public string? Var { get; set; }
    
    [JsonPropertyName("module")]
    public string? Module { get; set; }
}

/// <summary>
/// Globals available within function node execution
/// </summary>
public class FunctionGlobals
{
    public FlowMessage msg { get; set; } = new();
    public FunctionContext context { get; set; } = new();
    public FunctionContext flow { get; set; } = new();
    public FunctionContext global { get; set; } = new();
    public FunctionNode node { get; set; } = null!;
    
    public void Log(string message) => node?.Log(message);
    public void Warn(string message) => node?.Warn(message);
    public void Error(string message) => node?.Error(message);
}

/// <summary>
/// Context accessor for function nodes
/// </summary>
public class FunctionContext
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object?> _store = new();
    
    public object? Get(string key) => _store.TryGetValue(key, out var val) ? val : null;
    public void Set(string key, object? value) => _store[key] = value;
    public IEnumerable<string> Keys() => _store.Keys;
}

/// <summary>
/// Function node - Executes custom C# code
/// SOURCE: packages/node_modules/@node-red/nodes/core/function/10-function.js
/// 
/// NOTE: The original Node-RED function node uses JavaScript with vm.runInContext.
/// This C# translation uses Roslyn scripting for dynamic code execution.
/// For production use, consider security implications of dynamic code execution.
/// 
/// MAPPING NOTES:
/// - node.func (JavaScript) → C# script execution
/// - vm.Script → CSharpScript
/// - sandbox → FunctionGlobals
/// - context/flow/global → ConcurrentDictionary stores
/// </summary>
public class FunctionNode : BaseNode
{
    private readonly string _func;
    private readonly int _outputs;
    private readonly int _timeout;
    private readonly string? _initialize;
    private readonly string? _finalize;
    private Script<object?>? _compiledScript;
    private bool _initialized;
    
    /// <summary>
    /// Constructor - equivalent to function FunctionNode(n)
    /// SOURCE: Lines 93-535 of 10-function.js
    /// </summary>
    public FunctionNode(NodeConfig config) : base(config)
    {
        var funcConfig = config as FunctionNodeConfig ?? new FunctionNodeConfig();
        
        _func = funcConfig.Func ?? "return msg;";
        _outputs = funcConfig.Outputs > 0 ? funcConfig.Outputs : 1;
        _timeout = funcConfig.Timeout * 1000;
        _initialize = funcConfig.Initialize?.Trim();
        _finalize = funcConfig.Finalize?.Trim();
        
        // Pre-compile the script if possible
        try
        {
            CompileScript();
        }
        catch (Exception ex)
        {
            Error($"Script compilation error: {ex.Message}");
        }
        
        // Register input handler
        // SOURCE: Lines 343-350 - node.on("input", function(msg, send, done))
        OnInput(async (msg, send, done) =>
        {
            try
            {
                // Initialize on first message if needed
                if (!_initialized && !string.IsNullOrEmpty(_initialize))
                {
                    await RunInitializeAsync();
                    _initialized = true;
                }
                
                var result = await ExecuteFunctionAsync(msg);
                
                if (result != null)
                {
                    SendResults(result, send, msg.MsgId);
                }
                
                done(null);
            }
            catch (Exception ex)
            {
                Error($"Function error: {ex.Message}", msg);
                done(ex);
            }
        });
    }
    
    /// <summary>
    /// Compile the function script
    /// </summary>
    private void CompileScript()
    {
        // Wrap the user code to return a value
        var wrappedCode = $@"
// User function code
{_func}
";
        
        var options = ScriptOptions.Default
            .AddReferences(typeof(object).Assembly)
            .AddReferences(typeof(JsonSerializer).Assembly)
            .AddImports("System", "System.Collections.Generic", "System.Linq", "System.Text.Json");
        
        _compiledScript = CSharpScript.Create<object?>(wrappedCode, options, typeof(FunctionGlobals));
    }
    
    /// <summary>
    /// Run the initialize script
    /// SOURCE: Lines 356-376 - if (iniScript)
    /// </summary>
    private async Task RunInitializeAsync()
    {
        if (string.IsNullOrEmpty(_initialize))
            return;
        
        try
        {
            var globals = new FunctionGlobals
            {
                node = this,
                context = new FunctionContext(),
                flow = new FunctionContext(),
                global = new FunctionContext()
            };
            
            var options = ScriptOptions.Default
                .AddReferences(typeof(object).Assembly)
                .AddImports("System");
            
            await CSharpScript.RunAsync(_initialize, options, globals);
        }
        catch (Exception ex)
        {
            Error($"Initialize error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Execute the function with the given message
    /// SOURCE: Lines 418-486 - processMessage function
    /// </summary>
    private async Task<object?> ExecuteFunctionAsync(FlowMessage msg)
    {
        if (_compiledScript == null)
        {
            // Fallback: just return the message as-is
            return msg;
        }
        
        var globals = new FunctionGlobals
        {
            msg = msg,
            node = this,
            context = new FunctionContext(),
            flow = new FunctionContext(),
            global = new FunctionContext()
        };
        
        // Copy node context
        foreach (var kv in Context)
        {
            globals.context.Set(kv.Key, kv.Value);
        }
        
        // Copy flow context
        foreach (var kv in FlowContext)
        {
            globals.flow.Set(kv.Key, kv.Value);
        }
        
        // Copy global context
        foreach (var kv in GlobalContext)
        {
            globals.global.Set(kv.Key, kv.Value);
        }
        
        try
        {
            ScriptState<object?>? state;
            
            if (_timeout > 0)
            {
                using var cts = new CancellationTokenSource(_timeout);
                state = await _compiledScript.RunAsync(globals, cts.Token);
            }
            else
            {
                state = await _compiledScript.RunAsync(globals);
            }
            
            // Sync context changes back
            foreach (var key in globals.context.Keys())
            {
                Context[key] = globals.context.Get(key);
            }
            
            return state.ReturnValue ?? globals.msg;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Function execution timed out after {_timeout}ms");
        }
    }
    
    /// <summary>
    /// Send the function results
    /// SOURCE: Lines 25-61 - function sendResults(...)
    /// </summary>
    private void SendResults(object? results, Action<object?> send, string msgId)
    {
        if (results == null)
            return;
        
        // Handle array of outputs
        if (results is object?[] outputs)
        {
            // Multi-output: each element goes to corresponding output
            send(outputs);
        }
        else if (results is FlowMessage msg)
        {
            // Single message
            msg.MsgId = msgId;
            send(new object?[] { msg });
        }
        else if (results is IEnumerable<FlowMessage> msgs)
        {
            // Array of messages to first output
            foreach (var m in msgs)
            {
                m.MsgId = msgId;
            }
            send(new object?[] { msgs.ToArray() });
        }
        else
        {
            // Treat as payload
            var outMsg = new FlowMessage { Payload = results, MsgId = msgId };
            send(new object?[] { outMsg });
        }
    }
    
    /// <summary>
    /// Close handler - run finalize script
    /// SOURCE: Lines 488-506 - node.on("close")
    /// </summary>
    public override async Task CloseAsync(bool removed = false)
    {
        if (!string.IsNullOrEmpty(_finalize))
        {
            try
            {
                var globals = new FunctionGlobals
                {
                    node = this,
                    context = new FunctionContext(),
                    flow = new FunctionContext(),
                    global = new FunctionContext()
                };
                
                var options = ScriptOptions.Default
                    .AddReferences(typeof(object).Assembly)
                    .AddImports("System");
                
                await CSharpScript.RunAsync(_finalize, options, globals);
            }
            catch (Exception ex)
            {
                Error($"Finalize error: {ex.Message}");
            }
        }
        
        await base.CloseAsync(removed);
    }
    
    /// <summary>
    /// Register the function node type
    /// SOURCE: Lines 536-542 - RED.nodes.registerType("function", FunctionNode)
    /// </summary>
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("function", config => new FunctionNode(config));
    }
}
