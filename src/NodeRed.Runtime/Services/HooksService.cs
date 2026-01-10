// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Logging;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Services;

/// <summary>
/// Runtime hooks service implementation.
/// Equivalent to @node-red/util/lib/hooks.js
/// 
/// Implements a doubly-linked list of hooks by id, supporting:
/// - Labeled hooks (hookName.label) for targeted removal
/// - Async and sync callbacks
/// - Hook chaining with halt support (return false to stop)
/// </summary>
public class HooksService : IHooksService
{
    private static readonly string[] ValidHooks = IHooksService.MessageHooks
        .Concat(IHooksService.ModuleHooks)
        .ToArray();

    private readonly ILogger<HooksService>? _logger;

    // Doubly-linked list of hooks by id
    // hooks[id] points to head of list
    private readonly Dictionary<string, HookItem?> _hooks = new();

    // Hooks indexed by label for efficient removal
    private readonly Dictionary<string, Dictionary<string, HookItem>> _labelledHooks = new();

    // Flags for what hooks have handlers registered (for fast Has() check)
    private readonly HashSet<string> _states = new();

    /// <summary>
    /// Creates a new HooksService instance.
    /// </summary>
    public HooksService(ILogger<HooksService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Represents a hook handler in the linked list.
    /// </summary>
    private class HookItem
    {
        public Func<object, Task<bool>>? AsyncCallback { get; set; }
        public Func<object, bool>? SyncCallback { get; set; }
        public string Location { get; set; } = "";
        public HookItem? PreviousHook { get; set; }
        public HookItem? NextHook { get; set; }
        public bool Removed { get; set; }
    }

    /// <inheritdoc />
    public void Add(string hookId, Func<object, Task<bool>> callback)
    {
        AddHook(hookId, callback, null);
    }

    /// <inheritdoc />
    public void Add(string hookId, Func<object, bool> callback)
    {
        AddHook(hookId, null, callback);
    }

    private void AddHook(string hookId, Func<object, Task<bool>>? asyncCallback, Func<object, bool>? syncCallback)
    {
        var parts = hookId.Split('.', 2);
        var id = parts[0];
        var label = parts.Length > 1 ? parts[1] : null;

        // Validate hook ID
        if (!ValidHooks.Contains(id))
        {
            throw new ArgumentException($"Invalid hook '{id}'");
        }

        // Check for duplicate labelled hooks
        if (label != null && _labelledHooks.TryGetValue(label, out var labelDict) && labelDict.ContainsKey(id))
        {
            throw new InvalidOperationException($"Hook {hookId} already registered");
        }

        // Get location of calling code (for debugging)
        var callModule = GetCallerLocation();
        _logger?.LogDebug("Adding hook '{HookId}' from {Location}", hookId, callModule);

        var hookItem = new HookItem
        {
            AsyncCallback = asyncCallback,
            SyncCallback = syncCallback,
            Location = callModule,
            PreviousHook = null,
            NextHook = null
        };

        // Add to linked list
        if (!_hooks.TryGetValue(id, out var tailItem) || tailItem == null)
        {
            _hooks[id] = hookItem;
        }
        else
        {
            // Find tail of list
            while (tailItem.NextHook != null)
            {
                tailItem = tailItem.NextHook;
            }
            tailItem.NextHook = hookItem;
            hookItem.PreviousHook = tailItem;
        }

        // Add to labelled hooks if applicable
        if (label != null)
        {
            if (!_labelledHooks.ContainsKey(label))
            {
                _labelledHooks[label] = new Dictionary<string, HookItem>();
            }
            _labelledHooks[label][id] = hookItem;
        }

        _states.Add(id);
    }

    /// <inheritdoc />
    public void Remove(string hookId)
    {
        var parts = hookId.Split('.', 2);
        var id = parts[0];
        var label = parts.Length > 1 ? parts[1] : null;

        if (label == null)
        {
            throw new ArgumentException($"Cannot remove hook without label: {hookId}");
        }

        _logger?.LogDebug("Removing hook '{HookId}'", hookId);

        if (!_labelledHooks.TryGetValue(label, out var labelDict))
        {
            return;
        }

        if (id == "*")
        {
            // Remove all hooks for this label
            foreach (var hookEntry in labelDict.ToList())
            {
                RemoveHookItem(hookEntry.Key, hookEntry.Value);
            }
            _labelledHooks.Remove(label);
        }
        else if (labelDict.TryGetValue(id, out var hookItem))
        {
            RemoveHookItem(id, hookItem);
            labelDict.Remove(id);
            if (labelDict.Count == 0)
            {
                _labelledHooks.Remove(label);
            }
        }
    }

    private void RemoveHookItem(string id, HookItem hookItem)
    {
        var previousHook = hookItem.PreviousHook;
        var nextHook = hookItem.NextHook;

        if (previousHook != null)
        {
            previousHook.NextHook = nextHook;
        }
        else
        {
            _hooks[id] = nextHook;
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

    /// <inheritdoc />
    public async Task<bool> TriggerAsync(string hookId, object payload)
    {
        if (!_hooks.TryGetValue(hookId, out var hookItem) || hookItem == null)
        {
            return true; // No handlers, continue
        }

        return await InvokeStackAsync(hookItem, payload);
    }

    private async Task<bool> InvokeStackAsync(HookItem? hookItem, object payload)
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
                bool result;

                if (hookItem.AsyncCallback != null)
                {
                    result = await hookItem.AsyncCallback(payload);
                }
                else if (hookItem.SyncCallback != null)
                {
                    result = hookItem.SyncCallback(payload);
                }
                else
                {
                    hookItem = hookItem.NextHook;
                    continue;
                }

                // If callback returns false, halt the flow
                if (!result)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in hook handler at {Location}", hookItem.Location);
                throw;
            }

            hookItem = hookItem.NextHook;
        }

        return true;
    }

    /// <inheritdoc />
    public bool Has(string hookId)
    {
        var parts = hookId.Split('.', 2);
        var id = parts[0];
        var label = parts.Length > 1 ? parts[1] : null;

        if (label != null)
        {
            return _labelledHooks.TryGetValue(label, out var labelDict) && labelDict.ContainsKey(id);
        }

        return _states.Contains(id);
    }

    /// <inheritdoc />
    public void Clear()
    {
        _hooks.Clear();
        _labelledHooks.Clear();
        _states.Clear();
    }

    /// <summary>
    /// Gets the caller location for debugging purposes.
    /// </summary>
    private static string GetCallerLocation()
    {
        try
        {
            var stackTrace = new System.Diagnostics.StackTrace(true);
            var frame = stackTrace.GetFrame(3); // Skip GetCallerLocation, AddHook, and Add
            if (frame != null)
            {
                var fileName = frame.GetFileName() ?? "unknown";
                var lineNumber = frame.GetFileLineNumber();
                return $"{fileName}:{lineNumber}";
            }
        }
        catch
        {
            // Ignore errors getting stack trace
        }
        return "unknown:0";
    }
}
