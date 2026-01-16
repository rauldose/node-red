// Source: @node-red/editor-client/src/js/ui/diagnostics.js + env-var.js + event-log.js
// Translated to C# for NodeRed.NET
using System.Collections.Concurrent;

namespace NodeRed.Editor.Services;

/// <summary>
/// System diagnostics.
/// Translated from RED.diagnostics module.
/// </summary>
public class Diagnostics
{
    private readonly EditorState _state;
    
    public Diagnostics(EditorState state)
    {
        _state = state;
    }
    
    /// <summary>
    /// Get system diagnostics information.
    /// Translated from: diagnostics.get = function()
    /// </summary>
    public async Task<DiagnosticsInfo> GetAsync()
    {
        return await Task.FromResult(new DiagnosticsInfo
        {
            NodeRedVersion = "NodeRed.NET v1.0",
            DotNetVersion = Environment.Version.ToString(),
            Platform = Environment.OSVersion.ToString(),
            Arch = Environment.Is64BitOperatingSystem ? "x64" : "x86",
            Memory = new MemoryInfo
            {
                Total = GC.GetTotalMemory(false),
                Used = GC.GetTotalMemory(false)
            },
            Nodes = 0, // TODO: Get actual node count
            Flows = _state.Workspaces.GetAll().Count()
        });
    }
}

public class DiagnosticsInfo
{
    public string NodeRedVersion { get; set; } = "";
    public string DotNetVersion { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Arch { get; set; } = "";
    public MemoryInfo Memory { get; set; } = new();
    public int Nodes { get; set; }
    public int Flows { get; set; }
}

public class MemoryInfo
{
    public long Total { get; set; }
    public long Used { get; set; }
}

/// <summary>
/// Environment variables management.
/// Translated from RED.env-var module.
/// </summary>
public class EnvVarManager
{
    private readonly Dictionary<string, EnvVar> _envVars = new();
    
    /// <summary>
    /// Get all environment variables.
    /// </summary>
    public IEnumerable<EnvVar> GetAll() => _envVars.Values;
    
    /// <summary>
    /// Get an environment variable by name.
    /// </summary>
    public EnvVar? Get(string name)
    {
        return _envVars.TryGetValue(name, out var envVar) ? envVar : null;
    }
    
    /// <summary>
    /// Set an environment variable.
    /// </summary>
    public void Set(string name, object value, string type = "str")
    {
        _envVars[name] = new EnvVar
        {
            Name = name,
            Value = value,
            Type = type
        };
    }
    
    /// <summary>
    /// Remove an environment variable.
    /// </summary>
    public void Remove(string name)
    {
        _envVars.Remove(name);
    }
    
    /// <summary>
    /// Evaluate environment variable value.
    /// Translated from: envVar.evaluate = function(value, node)
    /// </summary>
    public object? Evaluate(string name, Dictionary<string, object>? context = null)
    {
        if (_envVars.TryGetValue(name, out var envVar))
        {
            return envVar.Value;
        }
        
        // Fall back to system environment variable
        return Environment.GetEnvironmentVariable(name);
    }
}

public class EnvVar
{
    public string Name { get; set; } = "";
    public object Value { get; set; } = "";
    public string Type { get; set; } = "str"; // str, num, bool, json, bin, env, node, flow, global
}

/// <summary>
/// Event log for tracking editor events.
/// Translated from RED.eventLog module.
/// </summary>
public class EventLog
{
    private readonly ConcurrentQueue<EditorEventLogEntry> _entries = new();
    private const int MaxEntries = 1000;
    
    public event Action<EditorEventLogEntry>? OnEntryAdded;
    
    /// <summary>
    /// Add an event log entry.
    /// Translated from: eventLog.log = function(event)
    /// </summary>
    public void Log(string eventType, string message, object? data = null)
    {
        var entry = new EditorEventLogEntry
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Type = eventType,
            Message = message,
            Data = data
        };
        
        _entries.Enqueue(entry);
        
        // Trim old entries
        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _)) { }
        
        OnEntryAdded?.Invoke(entry);
    }
    
    /// <summary>
    /// Get all log entries.
    /// </summary>
    public IEnumerable<EditorEventLogEntry> GetEntries() => _entries.ToArray();
    
    /// <summary>
    /// Clear all log entries.
    /// </summary>
    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }
    
    /// <summary>
    /// Get entries filtered by type.
    /// </summary>
    public IEnumerable<EditorEventLogEntry> GetByType(string type)
    {
        return _entries.Where(e => e.Type == type);
    }
}

public class EditorEventLogEntry
{
    public string Id { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public object? Data { get; set; }
}
