// ============================================================
// SOURCE: packages/node_modules/@node-red/util/lib/hooks.js
// LINES: 1-261
// ============================================================
// ORIGINAL CODE:
// ------------------------------------------------------------
// const VALID_HOOKS = [
//    "onSend", "preRoute", "preDeliver", "postDeliver",
//    "onReceive", "postReceive", "onComplete",
//    "preInstall", "postInstall", "preUninstall", "postUninstall"
// ]
//
// let states = { }
// let hooks = { }
// let labelledHooks = { }
//
// function add(hookId, callback) { ... }
// function remove(hookId) { ... }
// function trigger(hookId, payload, done) { ... }
// function clear() { ... }
// function has(hookId) { ... }
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace NodeRed.Util
{
    /// <summary>
    /// Hook item representing a single hook callback in the linked list.
    /// </summary>
    internal class HookItem
    {
        public Func<object, Task<object?>> Callback { get; set; } = null!;
        public string Location { get; set; } = string.Empty;
        public HookItem? PreviousHook { get; set; }
        public HookItem? NextHook { get; set; }
        public bool Removed { get; set; }
    }

    /// <summary>
    /// Runtime hooks engine.
    /// Provides a hook system for intercepting and modifying runtime behavior.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/util/lib/hooks.js
    /// 
    /// The following hooks can be used:
    /// 
    /// Message sending:
    /// - onSend - passed an array of SendEvent objects
    /// - preRoute - passed a SendEvent
    /// - preDeliver - passed a SendEvent (message has been cloned if needed)
    /// - postDeliver - passed a SendEvent (message dispatched for async delivery)
    /// - onReceive - passed a ReceiveEvent when a node is about to receive
    /// - postReceive - passed a ReceiveEvent after message given to input handler
    /// - onComplete - passed a CompleteEvent when node has completed
    /// 
    /// Module install hooks:
    /// - preInstall, postInstall, preUninstall, postUninstall
    /// </remarks>
    public static class Hooks
    {
        /// <summary>
        /// List of valid hook names.
        /// </summary>
        private static readonly HashSet<string> ValidHooks = new()
        {
            // Message Routing Path
            "onSend",
            "preRoute",
            "preDeliver",
            "postDeliver",
            "onReceive",
            "postReceive",
            "onComplete",
            // Module install hooks
            "preInstall",
            "postInstall",
            "preUninstall",
            "postUninstall"
        };

        /// <summary>
        /// Flags for what hooks have handlers registered.
        /// </summary>
        private static readonly Dictionary<string, bool> _states = new();

        /// <summary>
        /// Doubly-LinkedList of hooks by id. Points to head of list.
        /// </summary>
        private static readonly Dictionary<string, HookItem> _hooks = new();

        /// <summary>
        /// Hooks organized by label.
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, HookItem>> _labelledHooks = new();

        /// <summary>
        /// Lock object for thread safety.
        /// </summary>
        private static readonly object _lock = new();

        /// <summary>
        /// Register a handler to a named hook.
        /// </summary>
        /// <param name="hookId">The name of the hook to attach to (format: "hookName" or "hookName.label").</param>
        /// <param name="callback">The callback function for the hook. Returns null to continue, false to halt, or an error.</param>
        /// <param name="callerFilePath">Automatically captured caller file path.</param>
        /// <param name="callerLineNumber">Automatically captured caller line number.</param>
        public static void Add(
            string hookId,
            Func<object, Task<object?>> callback,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            var parts = hookId.Split('.', 2);
            var id = parts[0];
            var label = parts.Length > 1 ? parts[1] : null;

            if (!ValidHooks.Contains(id))
            {
                throw new ArgumentException($"Invalid hook '{id}'");
            }

            lock (_lock)
            {
                if (label != null &&
                    _labelledHooks.TryGetValue(label, out var labelHooks) &&
                    labelHooks.ContainsKey(id))
                {
                    throw new InvalidOperationException($"Hook {hookId} already registered");
                }

                // Get location of calling code
                var callModule = string.IsNullOrEmpty(callerFilePath)
                    ? "unknown:0:0"
                    : $"{callerFilePath}:{callerLineNumber}";

                Log.Debug($"Adding hook '{hookId}' from {callModule}");

                var hookItem = new HookItem
                {
                    Callback = callback,
                    Location = callModule,
                    PreviousHook = null,
                    NextHook = null
                };

                if (!_hooks.TryGetValue(id, out var tailItem))
                {
                    _hooks[id] = hookItem;
                }
                else
                {
                    // Find the tail of the linked list
                    while (tailItem.NextHook != null)
                    {
                        tailItem = tailItem.NextHook;
                    }
                    tailItem.NextHook = hookItem;
                    hookItem.PreviousHook = tailItem;
                }

                if (label != null)
                {
                    if (!_labelledHooks.TryGetValue(label, out labelHooks))
                    {
                        labelHooks = new Dictionary<string, HookItem>();
                        _labelledHooks[label] = labelHooks;
                    }
                    labelHooks[id] = hookItem;
                }

                _states[id] = true;
            }
        }

        /// <summary>
        /// Overload for synchronous callbacks.
        /// </summary>
        public static void Add(
            string hookId,
            Func<object, object?> callback,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            Add(hookId, payload => Task.FromResult(callback(payload)), callerFilePath, callerLineNumber);
        }

        /// <summary>
        /// Remove a handler from a named hook.
        /// </summary>
        /// <param name="hookId">The name of the hook event to remove - must be "name.label" or "*.label".</param>
        public static void Remove(string hookId)
        {
            var parts = hookId.Split('.', 2);
            var id = parts[0];
            var label = parts.Length > 1 ? parts[1] : null;

            if (label == null)
            {
                throw new ArgumentException($"Cannot remove hook without label: {hookId}");
            }

            Log.Debug($"Removing hook '{hookId}'");

            lock (_lock)
            {
                if (!_labelledHooks.TryGetValue(label, out var labelHooks))
                {
                    return;
                }

                if (id == "*")
                {
                    // Remove all hooks for this label
                    foreach (var kvp in labelHooks)
                    {
                        RemoveHook(kvp.Key, kvp.Value);
                    }
                    _labelledHooks.Remove(label);
                }
                else if (labelHooks.TryGetValue(id, out var hookItem))
                {
                    RemoveHook(id, hookItem);
                    labelHooks.Remove(id);
                    if (labelHooks.Count == 0)
                    {
                        _labelledHooks.Remove(label);
                    }
                }
            }
        }

        /// <summary>
        /// Remove a specific hook item from the linked list.
        /// </summary>
        private static void RemoveHook(string id, HookItem hookItem)
        {
            var previousHook = hookItem.PreviousHook;
            var nextHook = hookItem.NextHook;

            if (previousHook != null)
            {
                previousHook.NextHook = nextHook;
            }
            else
            {
                if (nextHook != null)
                {
                    _hooks[id] = nextHook;
                }
                else
                {
                    _hooks.Remove(id);
                }
            }

            if (nextHook != null)
            {
                nextHook.PreviousHook = previousHook;
            }

            hookItem.Removed = true;

            if (previousHook == null && nextHook == null)
            {
                _hooks.Remove(id);
                _states.Remove(id);
            }
        }

        /// <summary>
        /// Trigger a hook with the given payload.
        /// </summary>
        /// <param name="hookId">The hook to trigger.</param>
        /// <param name="payload">The payload to pass to hook handlers.</param>
        /// <returns>A task that completes when all handlers have been invoked.</returns>
        public static async Task TriggerAsync(string hookId, object payload)
        {
            HookItem? hookItem;
            lock (_lock)
            {
                _hooks.TryGetValue(hookId, out hookItem);
            }

            if (hookItem == null)
            {
                return;
            }

            await InvokeStackAsync(hookItem, payload);
        }

        /// <summary>
        /// Trigger a hook with a callback for completion.
        /// </summary>
        /// <param name="hookId">The hook to trigger.</param>
        /// <param name="payload">The payload to pass to hook handlers.</param>
        /// <param name="done">Callback when complete. Receives error/false for halt, or null for success.</param>
        public static void Trigger(string hookId, object payload, Action<object?> done)
        {
            HookItem? hookItem;
            lock (_lock)
            {
                _hooks.TryGetValue(hookId, out hookItem);
            }

            if (hookItem == null)
            {
                done(null);
                return;
            }

            InvokeStack(hookItem, payload, done);
        }

        /// <summary>
        /// Invoke the hook stack asynchronously.
        /// </summary>
        private static async Task InvokeStackAsync(HookItem? hookItem, object payload)
        {
            while (hookItem != null)
            {
                if (hookItem.Removed)
                {
                    hookItem = hookItem.NextHook;
                    continue;
                }

                try
                {
                    var result = await hookItem.Callback(payload);

                    if (result is bool boolResult && boolResult == false)
                    {
                        // Halting the flow
                        return;
                    }

                    if (result != null && result is not bool)
                    {
                        // Error result
                        var error = result is Exception ex ? ex : new Exception(result.ToString());
                        throw error;
                    }

                    hookItem = hookItem.NextHook;
                }
                catch (Exception ex)
                {
                    var error = ex;
                    throw new HookException(hookId: "", error);
                }
            }
        }

        /// <summary>
        /// Invoke the hook stack with callback.
        /// </summary>
        private static void InvokeStack(HookItem? hookItem, object payload, Action<object?> done)
        {
            void CallNextHook(object? err)
            {
                if (hookItem == null || err != null)
                {
                    done(err);
                    return;
                }

                if (hookItem.Removed)
                {
                    hookItem = hookItem.NextHook;
                    CallNextHook(null);
                    return;
                }

                var callback = hookItem.Callback;

                try
                {
                    var resultTask = callback(payload);

                    resultTask.ContinueWith(task =>
                    {
                        if (task.IsFaulted)
                        {
                            done(task.Exception?.InnerException ?? task.Exception);
                            return;
                        }

                        var result = task.Result;

                        if (result is bool boolResult && boolResult == false)
                        {
                            // Halting the flow
                            done(false);
                            return;
                        }

                        if (result == null)
                        {
                            hookItem = hookItem.NextHook;
                            CallNextHook(null);
                        }
                        else
                        {
                            done(result);
                        }
                    });
                }
                catch (Exception ex)
                {
                    done(ex);
                }
            }

            CallNextHook(null);
        }

        /// <summary>
        /// Clear all hooks.
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _hooks.Clear();
                _labelledHooks.Clear();
                _states.Clear();
            }
        }

        /// <summary>
        /// Check if a hook has any handlers registered.
        /// </summary>
        /// <param name="hookId">The hook to check (format: "hookName" or "hookName.label").</param>
        /// <returns>True if handlers are registered.</returns>
        public static bool Has(string hookId)
        {
            var parts = hookId.Split('.', 2);
            var id = parts[0];
            var label = parts.Length > 1 ? parts[1] : null;

            lock (_lock)
            {
                if (label != null)
                {
                    return _labelledHooks.TryGetValue(label, out var labelHooks) &&
                           labelHooks.ContainsKey(id);
                }

                return _states.ContainsKey(id) && _states[id];
            }
        }
    }

    /// <summary>
    /// Exception thrown when a hook encounters an error.
    /// </summary>
    public class HookException : Exception
    {
        public string HookId { get; }

        public HookException(string hookId, Exception innerException)
            : base($"Hook '{hookId}' error: {innerException.Message}", innerException)
        {
            HookId = hookId;
        }
    }
}
// ------------------------------------------------------------
// MAPPING NOTES:
// - VALID_HOOKS array → ValidHooks HashSet
// - states object → _states Dictionary
// - hooks object → _hooks Dictionary (linked list heads)
// - labelledHooks object → _labelledHooks Dictionary
// - HookItem object → HookItem class
// - add(hookId, callback) → Add(hookId, callback)
// - remove(hookId) → Remove(hookId)
// - trigger(hookId, payload, done) → Trigger(hookId, payload, done) and TriggerAsync
// - clear() → Clear()
// - has(hookId) → Has(hookId)
// - new Error().stack → [CallerFilePath] and [CallerLineNumber] attributes
// - Promise-based async → Task-based async
// - Callback pattern → Action<object?> callback and Task
// ============================================================
