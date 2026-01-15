// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/comms.js
// ============================================================
// ORIGINAL CODE (lines 1-100):
// ------------------------------------------------------------
// var ws;
// var reconnectAttempts = 0;
// var active = false;
// var errornotification;
// var subscriptions = {};
// var pendingAuth;
// function connectWS() { ... }
// function subscribe(topic, callback) { ... }
// function unsubscribe(topic, callback) { ... }
// function publish(topic, payload) { ... }
// ------------------------------------------------------------
// TRANSLATION:
// - WebSocket → SignalR HubConnection
// - subscriptions object → ConcurrentDictionary
// - callbacks → Action<string, object?>
// ============================================================

using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;

namespace NodeRed.Editor.Services;

/// <summary>
/// Interface for editor communications - translated from RED.comms
/// </summary>
public interface IEditorComms
{
    Task ConnectAsync();
    Task DisconnectAsync();
    IDisposable Subscribe(string topic, Action<string, object?> callback);
    void Unsubscribe(string topic, Action<string, object?>? callback = null);
    Task PublishAsync(string topic, object? payload);
    bool IsConnected { get; }
    event Action<bool>? OnConnectionStateChanged;
}

/// <summary>
/// SignalR-based implementation of editor communications
/// Translated from: packages/node_modules/@node-red/editor-client/src/js/comms.js
/// </summary>
public class EditorComms : IEditorComms, IAsyncDisposable
{
    // ============================================================
    // MAPPING NOTES:
    // - var ws → _hubConnection (SignalR)
    // - var reconnectAttempts → _reconnectAttempts
    // - var active → _active
    // - var subscriptions = {} → _subscriptions (ConcurrentDictionary)
    // - connectWS() → ConnectAsync()
    // - ws.onmessage → _hubConnection.On()
    // ============================================================

    private HubConnection? _hubConnection;
    private int _reconnectAttempts = 0;
    private bool _active = false;
    private readonly ConcurrentDictionary<string, List<Action<string, object?>>> _subscriptions = new();
    private readonly object _subscriptionLock = new();
    private readonly string _hubUrl;
    private CancellationTokenSource? _reconnectCts;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    public event Action<bool>? OnConnectionStateChanged;

    public EditorComms(string hubUrl = "/comms")
    {
        _hubUrl = hubUrl;
    }

    /// <summary>
    /// Connect to the communications hub.
    /// Translated from connectWS() function.
    /// </summary>
    public async Task ConnectAsync()
    {
        if (_active) return;

        _active = true;
        _reconnectCts = new CancellationTokenSource();

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        // Handle incoming messages - translated from ws.onmessage
        _hubConnection.On<string, object?>("ReceiveMessage", (topic, payload) =>
        {
            HandleMessage(topic, payload);
        });

        // Handle reconnection events
        _hubConnection.Reconnecting += error =>
        {
            _reconnectAttempts++;
            OnConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _reconnectAttempts = 0;
            OnConnectionStateChanged?.Invoke(true);
            return Task.CompletedTask;
        };

        _hubConnection.Closed += async error =>
        {
            OnConnectionStateChanged?.Invoke(false);
            
            if (_active && _reconnectCts?.IsCancellationRequested != true)
            {
                // Attempt manual reconnection
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(_reconnectAttempts * 2, 30)));
                _reconnectAttempts++;
                
                try
                {
                    await _hubConnection.StartAsync(_reconnectCts!.Token);
                    _reconnectAttempts = 0;
                    OnConnectionStateChanged?.Invoke(true);
                }
                catch
                {
                    // Reconnection failed, will retry on next cycle
                }
            }
        };

        try
        {
            await _hubConnection.StartAsync();
            _reconnectAttempts = 0;
            OnConnectionStateChanged?.Invoke(true);
        }
        catch
        {
            _reconnectAttempts++;
            OnConnectionStateChanged?.Invoke(false);
        }
    }

    /// <summary>
    /// Disconnect from the communications hub.
    /// </summary>
    public async Task DisconnectAsync()
    {
        _active = false;
        _reconnectCts?.Cancel();
        
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }

    /// <summary>
    /// Subscribe to a topic pattern.
    /// Translated from subscribe(topic, callback) function.
    /// 
    /// ORIGINAL CODE:
    /// function subscribe(topic, callback) {
    ///     if (subscriptions[topic] == null) {
    ///         subscriptions[topic] = [];
    ///     }
    ///     subscriptions[topic].push(callback);
    /// }
    /// </summary>
    public IDisposable Subscribe(string topic, Action<string, object?> callback)
    {
        _subscriptions.AddOrUpdate(
            topic,
            _ => new List<Action<string, object?>> { callback },
            (_, list) =>
            {
                lock (_subscriptionLock) { list.Add(callback); }
                return list;
            }
        );

        // Return a disposable for easy cleanup
        return new Subscription(this, topic, callback);
    }

    /// <summary>
    /// Unsubscribe from a topic.
    /// Translated from unsubscribe(topic, callback) function.
    /// </summary>
    public void Unsubscribe(string topic, Action<string, object?>? callback = null)
    {
        if (callback == null)
        {
            _subscriptions.TryRemove(topic, out _);
        }
        else if (_subscriptions.TryGetValue(topic, out var list))
        {
            lock (_subscriptionLock) 
            { 
                list.Remove(callback); 
                if (list.Count == 0)
                {
                    _subscriptions.TryRemove(topic, out _);
                }
            }
        }
    }

    /// <summary>
    /// Publish a message to a topic.
    /// Translated from publish(topic, payload) function.
    /// </summary>
    public async Task PublishAsync(string topic, object? payload)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("SendMessage", topic, payload);
        }
    }

    /// <summary>
    /// Handle incoming messages and route to subscribers.
    /// Translated from ws.onmessage handler.
    /// 
    /// ORIGINAL CODE:
    /// ws.onmessage = function(event) {
    ///     var msg = JSON.parse(event.data);
    ///     for (var t in subscriptions) {
    ///         if (subscriptions.hasOwnProperty(t)) {
    ///             var re = new RegExp("^"+t.replace(/([\[\]\?\(\)\\\\$\^\*\.|])/g,"\\$1").replace(/\+/g,"[^/]+").replace(/#/g,".*")+"$");
    ///             if (re.test(msg.topic)) {
    ///                 var subscribers = subscriptions[t];
    ///                 for (var i=0; i<subscribers.length; i++) {
    ///                     subscribers[i](msg.topic, msg.data);
    ///                 }
    ///             }
    ///         }
    ///     }
    /// }
    /// </summary>
    private void HandleMessage(string topic, object? payload)
    {
        foreach (var subscription in _subscriptions)
        {
            var pattern = subscription.Key;
            
            // Convert MQTT-style wildcards to regex
            // + matches exactly one level
            // # matches zero or more levels
            var regexPattern = "^" + 
                System.Text.RegularExpressions.Regex.Escape(pattern)
                    .Replace("\\+", "[^/]+")
                    .Replace("\\#", ".*") + 
                "$";

            if (System.Text.RegularExpressions.Regex.IsMatch(topic, regexPattern))
            {
                List<Action<string, object?>> callbacksCopy;
                lock (_subscriptionLock) { callbacksCopy = subscription.Value.ToList(); }
                
                foreach (var callback in callbacksCopy)
                {
                    try
                    {
                        callback(topic, payload);
                    }
                    catch
                    {
                        // Ignore callback errors
                    }
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    /// <summary>
    /// Helper class for subscription cleanup
    /// </summary>
    private class Subscription : IDisposable
    {
        private readonly EditorComms _comms;
        private readonly string _topic;
        private readonly Action<string, object?> _callback;

        public Subscription(EditorComms comms, string topic, Action<string, object?> callback)
        {
            _comms = comms;
            _topic = topic;
            _callback = callback;
        }

        public void Dispose()
        {
            _comms.Unsubscribe(_topic, _callback);
        }
    }
}

/// <summary>
/// Factory for creating EditorComms instances
/// </summary>
public static class EditorCommsFactory
{
    public static IEditorComms Create(string hubUrl = "/comms")
    {
        return new EditorComms(hubUrl);
    }
}
