// Source: @node-red/editor-client/src/js/ui/actions.js
// Translated to C# for NodeRed.NET
using System.Collections.Concurrent;

namespace NodeRed.Editor.Services;

/// <summary>
/// Action registration and execution system.
/// Translated from RED.actions module.
/// </summary>
public class Actions
{
    private readonly ConcurrentDictionary<string, ActionDefinition> _actions = new();
    private readonly EditorState _state;
    
    public Actions(EditorState state)
    {
        _state = state;
    }
    
    /// <summary>
    /// Add an action to the registry.
    /// Translated from: actions.add = function(name, handler, options)
    /// </summary>
    public void Add(string name, Action handler, ActionOptions? options = null)
    {
        _actions[name] = new ActionDefinition
        {
            Name = name,
            Handler = handler,
            Options = options ?? new ActionOptions()
        };
    }
    
    /// <summary>
    /// Add an action with a parameter.
    /// </summary>
    public void Add<T>(string name, Action<T> handler, ActionOptions? options = null)
    {
        _actions[name] = new ActionDefinition
        {
            Name = name,
            Handler = () => { },
            GenericHandler = (obj) => handler((T)obj!),
            Options = options ?? new ActionOptions()
        };
    }
    
    /// <summary>
    /// Remove an action from the registry.
    /// Translated from: actions.remove = function(name)
    /// </summary>
    public void Remove(string name)
    {
        _actions.TryRemove(name, out _);
    }
    
    /// <summary>
    /// Get an action by name.
    /// Translated from: actions.get = function(name)
    /// </summary>
    public ActionDefinition? Get(string name)
    {
        return _actions.TryGetValue(name, out var action) ? action : null;
    }
    
    /// <summary>
    /// Invoke an action by name.
    /// Translated from: actions.invoke = function(name, args)
    /// </summary>
    public bool Invoke(string name, object? args = null)
    {
        if (!_actions.TryGetValue(name, out var action))
        {
            return false;
        }
        
        try
        {
            if (args != null && action.GenericHandler != null)
            {
                action.GenericHandler(args);
            }
            else
            {
                action.Handler();
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Get list of all registered actions.
    /// Translated from: actions.list = function()
    /// </summary>
    public IEnumerable<ActionDefinition> List()
    {
        return _actions.Values.OrderBy(a => a.Name);
    }
    
    /// <summary>
    /// Initialize default actions.
    /// </summary>
    public void InitializeDefaults(
        Clipboard clipboard,
        History history,
        Keyboard keyboard,
        GroupManager groupManager,
        SubflowManager subflowManager)
    {
        // Edit actions - all placeholders
        Add("core:undo", () => history.Undo());
        Add("core:redo", () => history.Redo());
        Add("core:copy-selection-to-internal-clipboard", () => { /* TODO: clipboard copy */ });
        Add("core:cut-selection-to-internal-clipboard", () => { /* TODO: clipboard cut */ });
        Add("core:paste-from-internal-clipboard", () => { /* TODO: clipboard paste */ });
        
        // Selection actions
        Add("core:select-all-nodes", () => { /* TODO: Select all nodes */ });
        Add("core:select-none", () => { /* TODO: Clear selection */ });
        Add("core:delete-selection", () => { /* TODO: Delete selection */ });
        
        // Flow actions
        Add("core:show-import-dialog", () => { /* Opens import dialog */ });
        Add("core:show-export-dialog", () => { /* Opens export dialog */ });
        
        // Group actions
        Add("core:group-selection", () => { /* TODO: Create group */ });
        Add("core:ungroup-selection", () => { /* TODO: Ungroup */ });
        
        // Subflow actions
        Add("core:create-subflow", () => { /* TODO: Create subflow */ });
        
        // View actions
        Add("core:zoom-in", () => { /* TODO: Zoom in */ });
        Add("core:zoom-out", () => { /* TODO: Zoom out */ });
        Add("core:zoom-reset", () => { /* TODO: Zoom reset */ });
        
        // Deploy action
        Add("core:deploy-flows", () => { /* Triggers deploy */ });
    }
}

public class ActionDefinition
{
    public string Name { get; set; } = "";
    public Action Handler { get; set; } = () => { };
    public Action<object>? GenericHandler { get; set; }
    public ActionOptions Options { get; set; } = new();
}

public class ActionOptions
{
    public string? Category { get; set; }
    public string? Label { get; set; }
    public bool Enabled { get; set; } = true;
}
