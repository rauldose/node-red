using System.Collections.Concurrent;

namespace NodeRed.Editor.Services;

/// <summary>
/// Translated from: @node-red/editor-client/src/js/events.js
/// Event emitter for editor-wide event handling
/// </summary>
public class Events
{
    private readonly ConcurrentDictionary<string, List<Delegate>> _listeners = new();
    private readonly ConcurrentDictionary<string, List<Delegate>> _onceListeners = new();

    /// <summary>
    /// Subscribe to an event
    /// </summary>
    public void On(string eventName, Action handler)
    {
        if (!_listeners.ContainsKey(eventName))
        {
            _listeners[eventName] = new List<Delegate>();
        }
        _listeners[eventName].Add(handler);
    }

    /// <summary>
    /// Subscribe to an event with a parameter
    /// </summary>
    public void On<T>(string eventName, Action<T> handler)
    {
        if (!_listeners.ContainsKey(eventName))
        {
            _listeners[eventName] = new List<Delegate>();
        }
        _listeners[eventName].Add(handler);
    }

    /// <summary>
    /// Subscribe to an event once (automatically unsubscribed after first call)
    /// </summary>
    public void Once(string eventName, Action handler)
    {
        if (!_onceListeners.ContainsKey(eventName))
        {
            _onceListeners[eventName] = new List<Delegate>();
        }
        _onceListeners[eventName].Add(handler);
    }

    /// <summary>
    /// Subscribe to an event once with a parameter
    /// </summary>
    public void Once<T>(string eventName, Action<T> handler)
    {
        if (!_onceListeners.ContainsKey(eventName))
        {
            _onceListeners[eventName] = new List<Delegate>();
        }
        _onceListeners[eventName].Add(handler);
    }

    /// <summary>
    /// Unsubscribe from an event
    /// </summary>
    public void Off(string eventName, Delegate? handler = null)
    {
        if (handler == null)
        {
            _listeners.TryRemove(eventName, out _);
            _onceListeners.TryRemove(eventName, out _);
        }
        else
        {
            if (_listeners.TryGetValue(eventName, out var handlers))
            {
                handlers.Remove(handler);
            }
            if (_onceListeners.TryGetValue(eventName, out var onceHandlers))
            {
                onceHandlers.Remove(handler);
            }
        }
    }

    /// <summary>
    /// Emit an event
    /// </summary>
    public void Emit(string eventName)
    {
        InvokeHandlers(eventName, null);
    }

    /// <summary>
    /// Emit an event with data
    /// </summary>
    public void Emit<T>(string eventName, T data)
    {
        InvokeHandlers(eventName, data);
    }

    private void InvokeHandlers(string eventName, object? data)
    {
        // Regular listeners
        if (_listeners.TryGetValue(eventName, out var handlers))
        {
            foreach (var handler in handlers.ToList())
            {
                try
                {
                    if (data == null && handler is Action action)
                    {
                        action();
                    }
                    else
                    {
                        handler.DynamicInvoke(data);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Event handler error for '{eventName}': {ex.Message}");
                }
            }
        }

        // Once listeners
        if (_onceListeners.TryGetValue(eventName, out var onceHandlers))
        {
            var handlersToRemove = onceHandlers.ToList();
            onceHandlers.Clear();

            foreach (var handler in handlersToRemove)
            {
                try
                {
                    if (data == null && handler is Action action)
                    {
                        action();
                    }
                    else
                    {
                        handler.DynamicInvoke(data);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Once handler error for '{eventName}': {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Get number of listeners for an event
    /// </summary>
    public int ListenerCount(string eventName)
    {
        var count = 0;
        if (_listeners.TryGetValue(eventName, out var handlers))
        {
            count += handlers.Count;
        }
        if (_onceListeners.TryGetValue(eventName, out var onceHandlers))
        {
            count += onceHandlers.Count;
        }
        return count;
    }

    /// <summary>
    /// Get all event names with listeners
    /// </summary>
    public IEnumerable<string> EventNames()
    {
        return _listeners.Keys.Union(_onceListeners.Keys).Distinct();
    }

    /// <summary>
    /// Remove all listeners
    /// </summary>
    public void RemoveAllListeners()
    {
        _listeners.Clear();
        _onceListeners.Clear();
    }
}
