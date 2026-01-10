// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Core.Interfaces;

/// <summary>
/// Runtime hooks service interface.
/// Equivalent to @node-red/util/lib/hooks.js
/// 
/// The following hooks can be used:
/// 
/// Message sending:
///  - onSend - passed an array of SendEvent objects
///  - preRoute - passed a SendEvent
///  - preDeliver - passed a SendEvent (message has been cloned if needed)
///  - postDeliver - passed a SendEvent (message has been dispatched)
///  - onReceive - passed a ReceiveEvent when a node is about to receive a message
///  - postReceive - passed a ReceiveEvent when the message has been given to the node's input handler
///  - onComplete - passed a CompleteEvent when the node has completed with a message
///
/// Module install hooks:
///  - preInstall - before a module is installed
///  - postInstall - after a module is installed
///  - preUninstall - before a module is uninstalled
///  - postUninstall - after a module is uninstalled
/// </summary>
public interface IHooksService
{
    /// <summary>
    /// Valid hook IDs for message routing.
    /// </summary>
    public static readonly string[] MessageHooks = new[]
    {
        "onSend",
        "preRoute",
        "preDeliver",
        "postDeliver",
        "onReceive",
        "postReceive",
        "onComplete"
    };

    /// <summary>
    /// Valid hook IDs for module installation.
    /// </summary>
    public static readonly string[] ModuleHooks = new[]
    {
        "preInstall",
        "postInstall",
        "preUninstall",
        "postUninstall"
    };

    /// <summary>
    /// Register a handler to a named hook.
    /// </summary>
    /// <param name="hookId">The name of the hook to attach to. Can include a label: "hookName.label"</param>
    /// <param name="callback">The callback function for the hook. Return false to halt the flow.</param>
    void Add(string hookId, Func<object, Task<bool>> callback);

    /// <summary>
    /// Register a synchronous handler to a named hook.
    /// </summary>
    /// <param name="hookId">The name of the hook to attach to. Can include a label: "hookName.label"</param>
    /// <param name="callback">The callback function for the hook. Return false to halt the flow.</param>
    void Add(string hookId, Func<object, bool> callback);

    /// <summary>
    /// Remove a handler from a named hook.
    /// </summary>
    /// <param name="hookId">The hook ID with label: "hookName.label" or "*.label" to remove all hooks with that label</param>
    void Remove(string hookId);

    /// <summary>
    /// Trigger a hook with the given payload.
    /// </summary>
    /// <param name="hookId">The hook ID to trigger</param>
    /// <param name="payload">The payload to pass to hook handlers</param>
    /// <returns>True if all handlers completed successfully, false if halted</returns>
    Task<bool> TriggerAsync(string hookId, object payload);

    /// <summary>
    /// Check if a hook has any registered handlers.
    /// </summary>
    /// <param name="hookId">The hook ID to check. Can include a label.</param>
    /// <returns>True if the hook has handlers</returns>
    bool Has(string hookId);

    /// <summary>
    /// Clear all registered hooks.
    /// </summary>
    void Clear();
}

/// <summary>
/// Event passed to onSend hook.
/// </summary>
public class SendEvent
{
    /// <summary>
    /// The message being sent.
    /// </summary>
    public object? Message { get; set; }

    /// <summary>
    /// The source node ID.
    /// </summary>
    public string SourceNodeId { get; set; } = "";

    /// <summary>
    /// The source port index.
    /// </summary>
    public int SourcePort { get; set; }

    /// <summary>
    /// The target node ID (set after routing).
    /// </summary>
    public string? TargetNodeId { get; set; }

    /// <summary>
    /// Whether the message has been cloned.
    /// </summary>
    public bool Cloned { get; set; }
}

/// <summary>
/// Event passed to onReceive/postReceive hooks.
/// </summary>
public class ReceiveEvent
{
    /// <summary>
    /// The message being received.
    /// </summary>
    public object? Message { get; set; }

    /// <summary>
    /// The target node ID.
    /// </summary>
    public string NodeId { get; set; } = "";
}

/// <summary>
/// Event passed to onComplete hook.
/// </summary>
public class CompleteEvent
{
    /// <summary>
    /// The message that was processed.
    /// </summary>
    public object? Message { get; set; }

    /// <summary>
    /// The node ID that completed processing.
    /// </summary>
    public string NodeId { get; set; } = "";

    /// <summary>
    /// Any error that occurred during processing.
    /// </summary>
    public Exception? Error { get; set; }
}

/// <summary>
/// Event passed to module install/uninstall hooks.
/// </summary>
public class ModuleEvent
{
    /// <summary>
    /// The module name.
    /// </summary>
    public string ModuleName { get; set; } = "";

    /// <summary>
    /// The module version.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// The module directory path.
    /// </summary>
    public string? Path { get; set; }
}
