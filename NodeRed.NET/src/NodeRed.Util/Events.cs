// ============================================================
// SOURCE: packages/node_modules/@node-red/util/lib/events.js
// LINES: 1-80
// ============================================================
// ORIGINAL CODE:
// ------------------------------------------------------------
// const events = new (require("events")).EventEmitter();
//
// const deprecatedEvents = {
//     "nodes-stopped": "flows:stopped",
//     "nodes-started": "flows:started"
// }
//
// function wrapEventFunction(obj,func) {
//     events["_"+func] = events[func];
//     return function(eventName, listener) {
//         if (deprecatedEvents.hasOwnProperty(eventName)) {
//             const log = require("@node-red/util").log;
//             const stack = (new Error().stack).split("\n");
//             let location = "(unknown)"
//             if (stack.length > 2) {
//                 location = stack[2].split("(")[1].slice(0,-1);
//             }
//             log.warn(`[RED.events] Deprecated use of "${eventName}" event from "${location}". Use "${deprecatedEvents[eventName]}" instead.`)
//         }
//         return events["_"+func].call(events,eventName,listener)
//     }
// }
//
// events.on = wrapEventFunction(events,"on");
// events.once = wrapEventFunction(events,"once");
// events.addListener = events.on;
//
// module.exports = events;
// ------------------------------------------------------------
// TRANSLATION:
// ------------------------------------------------------------

// Copyright JS Foundation and other contributors, http://js.foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NodeRed.Util
{
    /// <summary>
    /// Runtime events emitter.
    /// Provides an event aggregator pattern for runtime events within Node-RED.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/util/lib/events.js
    /// </remarks>
    public class Events
    {
        /// <summary>
        /// Singleton instance of the events emitter.
        /// </summary>
        public static Events Instance { get; } = new Events();

        /// <summary>
        /// Map of deprecated event names to their replacements.
        /// </summary>
        private static readonly Dictionary<string, string> DeprecatedEvents = new()
        {
            { "nodes-stopped", "flows:stopped" },
            { "nodes-started", "flows:started" }
        };

        /// <summary>
        /// Storage for event listeners by event name.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<EventHandler<EventArgs>>> _listeners = new();

        /// <summary>
        /// Storage for one-time event listeners by event name.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<EventHandler<EventArgs>>> _onceListeners = new();

        /// <summary>
        /// Generic event args that can hold any data.
        /// </summary>
        public class NodeRedEventArgs : EventArgs
        {
            public string EventName { get; set; } = string.Empty;
            public object? Data { get; set; }
            public bool Retain { get; set; }
        }

        /// <summary>
        /// Private constructor to enforce singleton pattern.
        /// </summary>
        private Events()
        {
        }

        /// <summary>
        /// Register an event listener for a runtime event.
        /// </summary>
        /// <param name="eventName">The name of the event to listen to.</param>
        /// <param name="listener">The callback action for the event.</param>
        /// <param name="callerFilePath">Automatically captured caller file path.</param>
        /// <param name="callerLineNumber">Automatically captured caller line number.</param>
        public void On(
            string eventName,
            EventHandler<EventArgs> listener,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            CheckDeprecatedEvent(eventName, callerFilePath, callerLineNumber);

            _listeners.AddOrUpdate(
                eventName,
                _ => new List<EventHandler<EventArgs>> { listener },
                (_, existingList) =>
                {
                    lock (existingList)
                    {
                        existingList.Add(listener);
                    }
                    return existingList;
                });
        }

        /// <summary>
        /// Register a one-time event listener for a runtime event.
        /// The listener will be removed after it is invoked once.
        /// </summary>
        /// <param name="eventName">The name of the event to listen to.</param>
        /// <param name="listener">The callback action for the event.</param>
        /// <param name="callerFilePath">Automatically captured caller file path.</param>
        /// <param name="callerLineNumber">Automatically captured caller line number.</param>
        public void Once(
            string eventName,
            EventHandler<EventArgs> listener,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            CheckDeprecatedEvent(eventName, callerFilePath, callerLineNumber);

            _onceListeners.AddOrUpdate(
                eventName,
                _ => new List<EventHandler<EventArgs>> { listener },
                (_, existingList) =>
                {
                    lock (existingList)
                    {
                        existingList.Add(listener);
                    }
                    return existingList;
                });
        }

        /// <summary>
        /// Alias for On() method.
        /// </summary>
        public void AddListener(
            string eventName,
            EventHandler<EventArgs> listener,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            On(eventName, listener, callerFilePath, callerLineNumber);
        }

        /// <summary>
        /// Remove an event listener.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="listener">The listener to remove.</param>
        public void RemoveListener(string eventName, EventHandler<EventArgs> listener)
        {
            if (_listeners.TryGetValue(eventName, out var listenerList))
            {
                lock (listenerList)
                {
                    listenerList.Remove(listener);
                }
            }

            if (_onceListeners.TryGetValue(eventName, out var onceList))
            {
                lock (onceList)
                {
                    onceList.Remove(listener);
                }
            }
        }

        /// <summary>
        /// Remove all listeners for a specific event or all events.
        /// </summary>
        /// <param name="eventName">The name of the event, or null to remove all listeners.</param>
        public void RemoveAllListeners(string? eventName = null)
        {
            if (eventName == null)
            {
                _listeners.Clear();
                _onceListeners.Clear();
            }
            else
            {
                _listeners.TryRemove(eventName, out _);
                _onceListeners.TryRemove(eventName, out _);
            }
        }

        /// <summary>
        /// Emit an event to all of its registered listeners.
        /// </summary>
        /// <param name="eventName">The name of the event to emit.</param>
        /// <param name="args">The event arguments to pass.</param>
        /// <returns>Whether the event had listeners or not.</returns>
        public bool Emit(string eventName, EventArgs? args = null)
        {
            args ??= EventArgs.Empty;
            bool hadListeners = false;

            // Invoke regular listeners
            if (_listeners.TryGetValue(eventName, out var listenerList))
            {
                List<EventHandler<EventArgs>> listenersCopy;
                lock (listenerList)
                {
                    listenersCopy = new List<EventHandler<EventArgs>>(listenerList);
                }

                foreach (var listener in listenersCopy)
                {
                    try
                    {
                        listener(this, args);
                        hadListeners = true;
                    }
                    catch (Exception)
                    {
                        // In Node.js EventEmitter, errors in listeners don't stop other listeners
                        // They would be emitted as 'error' events
                    }
                }
            }

            // Invoke and remove once listeners
            if (_onceListeners.TryRemove(eventName, out var onceList))
            {
                foreach (var listener in onceList)
                {
                    try
                    {
                        listener(this, args);
                        hadListeners = true;
                    }
                    catch (Exception)
                    {
                        // Same error handling as above
                    }
                }
            }

            return hadListeners;
        }

        /// <summary>
        /// Emit an event with specific data.
        /// </summary>
        /// <param name="eventName">The name of the event to emit.</param>
        /// <param name="data">The data to pass with the event.</param>
        /// <returns>Whether the event had listeners or not.</returns>
        public bool Emit(string eventName, object? data)
        {
            return Emit(eventName, new NodeRedEventArgs { EventName = eventName, Data = data });
        }

        /// <summary>
        /// Get the count of listeners for a specific event.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <returns>The number of listeners.</returns>
        public int ListenerCount(string eventName)
        {
            int count = 0;

            if (_listeners.TryGetValue(eventName, out var listenerList))
            {
                lock (listenerList)
                {
                    count += listenerList.Count;
                }
            }

            if (_onceListeners.TryGetValue(eventName, out var onceList))
            {
                lock (onceList)
                {
                    count += onceList.Count;
                }
            }

            return count;
        }

        /// <summary>
        /// Check if an event name is deprecated and log a warning if so.
        /// </summary>
        private void CheckDeprecatedEvent(string eventName, string callerFilePath, int callerLineNumber)
        {
            if (DeprecatedEvents.TryGetValue(eventName, out var replacement))
            {
                string location = string.IsNullOrEmpty(callerFilePath)
                    ? "(unknown)"
                    : $"{callerFilePath}:{callerLineNumber}";

                // Log warning about deprecated event
                // Note: In the full implementation, this would use Log.Warn
                Console.WriteLine($"[RED.events] Deprecated use of \"{eventName}\" event from \"{location}\". Use \"{replacement}\" instead.");
            }
        }
    }
}
// ------------------------------------------------------------
// MAPPING NOTES:
// - EventEmitter → Custom Events class with ConcurrentDictionary
// - events.on(eventName, listener) → On(eventName, listener)
// - events.once(eventName, listener) → Once(eventName, listener)
// - events.addListener → AddListener (alias for On)
// - events.emit(eventName, ...args) → Emit(eventName, args)
// - events.removeListener → RemoveListener
// - events.removeAllListeners → RemoveAllListeners
// - deprecatedEvents object → DeprecatedEvents Dictionary
// - wrapEventFunction → CheckDeprecatedEvent method
// - new Error().stack → [CallerFilePath] and [CallerLineNumber] attributes
// ============================================================
