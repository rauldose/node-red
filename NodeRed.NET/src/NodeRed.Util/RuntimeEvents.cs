// ============================================================
// INSPIRED BY: packages/node_modules/@node-red/util/lib/events.js
// ============================================================
// This implementation is inspired by Node-RED's event system but uses
// C# event patterns and delegates instead of Node.js EventEmitter.
// Core concepts maintained:
// - Central event bus for runtime events
// - Support for deprecated event names with warnings
// - Type-safe event handling
// ============================================================

/*!
 * Original work Copyright JS Foundation and other contributors, http://js.foundation
 * Modified work Copyright 2026 NodeRed.NET Contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections.Concurrent;

namespace NodeRed.Util;

/// <summary>
/// Runtime events emitter inspired by @node-red/util/events.js
/// Provides a central event bus for runtime events
/// </summary>
public class RuntimeEvents
{
    private static readonly Lazy<RuntimeEvents> _instance = new(() => new RuntimeEvents());
    
    private readonly ConcurrentDictionary<string, List<Action<object?>>> _eventHandlers = new();
    
    private readonly Dictionary<string, string> _deprecatedEvents = new()
    {
        { "nodes-stopped", "flows:stopped" },
        { "nodes-started", "flows:started" }
    };

    private RuntimeEvents() { }

    /// <summary>
    /// Gets the singleton instance of RuntimeEvents
    /// </summary>
    public static RuntimeEvents Instance => _instance.Value;

    /// <summary>
    /// Register an event listener for a runtime event
    /// Inspired by: events.on from Node-RED
    /// </summary>
    /// <param name="eventName">The name of the event to listen to</param>
    /// <param name="listener">The callback function for the event</param>
    public void On(string eventName, Action<object?> listener)
    {
        CheckDeprecated(eventName);
        
        _eventHandlers.AddOrUpdate(
            eventName,
            _ => new List<Action<object?>> { listener },
            (_, handlers) =>
            {
                handlers.Add(listener);
                return handlers;
            });
    }

    /// <summary>
    /// Register a one-time event listener for a runtime event
    /// </summary>
    /// <param name="eventName">The name of the event to listen to</param>
    /// <param name="listener">The callback function for the event</param>
    public void Once(string eventName, Action<object?> listener)
    {
        CheckDeprecated(eventName);
        
        Action<object?>? wrappedListener = null;
        wrappedListener = (args) =>
        {
            listener(args);
            if (wrappedListener != null)
            {
                Off(eventName, wrappedListener);
            }
        };
        
        On(eventName, wrappedListener);
    }

    /// <summary>
    /// Remove an event listener
    /// </summary>
    /// <param name="eventName">The name of the event</param>
    /// <param name="listener">The callback function to remove</param>
    public void Off(string eventName, Action<object?> listener)
    {
        if (_eventHandlers.TryGetValue(eventName, out var handlers))
        {
            handlers.Remove(listener);
            if (handlers.Count == 0)
            {
                _eventHandlers.TryRemove(eventName, out _);
            }
        }
    }

    /// <summary>
    /// Emit an event to all of its registered listeners
    /// Inspired by: events.emit from Node-RED
    /// </summary>
    /// <param name="eventName">The name of the event to emit</param>
    /// <param name="args">The arguments to pass in the event</param>
    /// <returns>Whether the event had listeners or not</returns>
    public bool Emit(string eventName, object? args = null)
    {
        if (_eventHandlers.TryGetValue(eventName, out var handlers))
        {
            // Create a copy to avoid modification during iteration
            var handlersCopy = handlers.ToList();
            foreach (var handler in handlersCopy)
            {
                try
                {
                    handler(args);
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other handlers
                    Console.Error.WriteLine($"Error in event handler for '{eventName}': {ex.Message}");
                }
            }
            return handlers.Count > 0;
        }
        return false;
    }

    /// <summary>
    /// Remove all listeners for an event, or all listeners if no event specified
    /// </summary>
    /// <param name="eventName">Optional event name to remove listeners for</param>
    public void RemoveAllListeners(string? eventName = null)
    {
        if (eventName == null)
        {
            _eventHandlers.Clear();
        }
        else
        {
            _eventHandlers.TryRemove(eventName, out _);
        }
    }

    private void CheckDeprecated(string eventName)
    {
        if (_deprecatedEvents.TryGetValue(eventName, out var newName))
        {
            // Get call stack for location (simplified version)
            var stackTrace = Environment.StackTrace;
            var location = "(unknown)";
            
            Console.WriteLine($"[WARN] [RuntimeEvents] Deprecated use of \"{eventName}\" event from \"{location}\". Use \"{newName}\" instead.");
        }
    }
}
