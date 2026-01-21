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
    private bool _flowsRunningState = false;

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
    public bool FlowsRunning => _state == "connected" || _state == "running" || _flowsRunningState;

    /// <summary>
    /// Check if flows are stopped
    /// </summary>
    public bool FlowsStopped => _state == "stopped" || !_flowsRunningState;

    /// <summary>
    /// Start all flows.
    /// Translated from runtime.startFlows in runtime.js
    /// </summary>
    public async Task StartFlowsAsync()
    {
        if (_flowsRunningState)
        {
            return; // Already running
        }

        _flowsRunningState = true;
        SetState("starting");
        
        _events.Emit("runtime:flows:starting");
        
        // Simulate startup delay
        await Task.Delay(100);
        
        SetState("running");
        _events.Emit("runtime:flows:started");
    }

    /// <summary>
    /// Stop all flows.
    /// Translated from runtime.stopFlows in runtime.js
    /// </summary>
    public async Task StopFlowsAsync()
    {
        if (!_flowsRunningState)
        {
            return; // Already stopped
        }

        _events.Emit("runtime:flows:stopping");
        SetState("stopping");
        
        // Simulate shutdown delay
        await Task.Delay(100);
        
        _flowsRunningState = false;
        SetState("stopped");
        _events.Emit("runtime:flows:stopped");
    }

    /// <summary>
    /// Restart flows (optionally a specific flow).
    /// Translated from runtime.restartFlows in runtime.js
    /// </summary>
    public async Task RestartFlowsAsync(string? flowId = null)
    {
        if (flowId != null)
        {
            // Restart specific flow
            _events.Emit("runtime:flow:restarting", new { flowId });
            await Task.Delay(50);
            _events.Emit("runtime:flow:restarted", new { flowId });
        }
        else
        {
            // Restart all flows
            await StopFlowsAsync();
            await StartFlowsAsync();
        }
    }

    /// <summary>
    /// Get flow status information.
    /// </summary>
    public FlowStatus GetFlowStatus()
    {
        return new FlowStatus
        {
            State = _state,
            Running = _flowsRunningState,
            Connected = _connected
        };
    }

    /// <summary>
    /// Get status of a specific flow.
    /// </summary>
    public FlowStatus GetFlowStatus(string flowId)
    {
        return new FlowStatus
        {
            FlowId = flowId,
            State = _state,
            Running = _flowsRunningState,
            Connected = _connected
        };
    }
}

/// <summary>
/// Flow status information.
/// </summary>
public class FlowStatus
{
    public string? FlowId { get; set; }
    public string State { get; set; } = "unknown";
    public bool Running { get; set; }
    public bool Connected { get; set; }
}
