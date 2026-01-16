namespace NodeRed.Editor.Services;

/// <summary>
/// Translated from: @node-red/editor-client/src/js/runtime.js
/// Runtime state and connection management
/// </summary>
public class Runtime
{
    private readonly Events _events;
    private bool _connected;
    private string _state = "disconnected";
    private Dictionary<string, object> _settings = new();
    private string? _version;

    public Runtime(Events events)
    {
        _events = events;
    }

    /// <summary>
    /// Whether the editor is connected to the runtime
    /// </summary>
    public bool Connected => _connected;

    /// <summary>
    /// Current runtime state
    /// </summary>
    public string State => _state;

    /// <summary>
    /// Runtime version
    /// </summary>
    public string? Version => _version;

    /// <summary>
    /// Set the connection state
    /// </summary>
    public void SetConnected(bool connected)
    {
        var wasConnected = _connected;
        _connected = connected;
        _state = connected ? "connected" : "disconnected";

        if (connected && !wasConnected)
        {
            _events.Emit("runtime:connected");
        }
        else if (!connected && wasConnected)
        {
            _events.Emit("runtime:disconnected");
        }
    }

    /// <summary>
    /// Set the runtime state
    /// </summary>
    public void SetState(string state)
    {
        var previousState = _state;
        _state = state;
        
        if (previousState != state)
        {
            _events.Emit("runtime:state", new { previous = previousState, current = state });
        }
    }

    /// <summary>
    /// Set runtime settings
    /// </summary>
    public void SetSettings(Dictionary<string, object> settings)
    {
        _settings = settings;
        
        if (settings.TryGetValue("version", out var version))
        {
            _version = version?.ToString();
        }

        _events.Emit("runtime:settings", settings);
    }

    /// <summary>
    /// Get a runtime setting
    /// </summary>
    public T? GetSetting<T>(string key, T? defaultValue = default)
    {
        if (_settings.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
                return typedValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Check if the runtime is in a specific state
    /// </summary>
    public bool IsState(string state)
    {
        return _state.Equals(state, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if flows are running
    /// </summary>
    public bool FlowsRunning => _state == "connected" || _state == "running";

    /// <summary>
    /// Check if flows are stopped
    /// </summary>
    public bool FlowsStopped => _state == "stopped";
}
