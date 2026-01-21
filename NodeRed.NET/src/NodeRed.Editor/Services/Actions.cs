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
        // Edit actions
        Add("core:undo", () => history.Undo());
        Add("core:redo", () => history.Redo());
        Add("core:copy-selection-to-internal-clipboard", () => 
        {
            var selection = _state.Nodes.GetSelectedNodes().ToList();
            if (selection.Any())
            {
                clipboard.CopySelection(selection);
            }
        });
        Add("core:cut-selection-to-internal-clipboard", () => 
        {
            var selection = _state.Nodes.GetSelectedNodes().ToList();
            if (selection.Any())
            {
                clipboard.CopySelection(selection);
                foreach (var node in selection)
                {
                    _state.Nodes.Remove(node);
                }
            }
        });
        Add("core:paste-from-internal-clipboard", () => 
        {
            // Paste uses Import() which is already implemented in Clipboard
            _state.Events.Emit("editor:paste", null);
        });
        
        // Selection actions
        Add("core:select-all-nodes", () => 
        {
            var activeWorkspace = _state.Workspaces.Active();
            _state.Nodes.SelectAll(activeWorkspace);
            _state.Events.Emit("view:selection-changed", null);
        });
        Add("core:select-none", () => 
        {
            _state.Nodes.ClearSelection();
            _state.Events.Emit("view:selection-changed", null);
        });
        Add("core:delete-selection", () => 
        {
            var selection = _state.Nodes.GetSelectedNodes().ToList();
            if (selection.Any())
            {
                _state.Nodes.DeleteSelection();
                history.Push(new HistoryEvent
                {
                    Type = HistoryEventType.Delete,
                    NodeIds = selection.Select(n => n.Id).ToList()
                });
                _state.Events.Emit("nodes:delete", null);
            }
        });
        
        // Flow actions
        Add("core:show-import-dialog", () => 
        {
            _state.Events.Emit("editor:open-import-dialog", null);
        });
        Add("core:show-export-dialog", () => 
        {
            _state.Events.Emit("editor:open-export-dialog", null);
        });
        
        // Group actions
        Add("core:group-selection", () => 
        {
            var selection = _state.Nodes.GetSelectedNodes().ToList();
            if (selection.Count >= 2)
            {
                groupManager.CreateGroup(selection);
                _state.Events.Emit("groups:add", null);
            }
        });
        Add("core:ungroup-selection", () => 
        {
            var selection = _state.Nodes.GetSelectedNodes().ToList();
            groupManager.UngroupSelection(selection);
            _state.Events.Emit("groups:remove", null);
        });
        
        // Subflow actions
        Add("core:create-subflow", () => 
        {
            var selection = _state.Nodes.GetSelectedNodes().ToList();
            subflowManager.CreateSubflow(selection);
            _state.Events.Emit("subflows:add", null);
        });
        
        // View actions - use simple zoom approach
        Add("core:zoom-in", () => 
        {
            _state.Events.Emit("view:zoom-in", null);
        });
        Add("core:zoom-out", () => 
        {
            _state.Events.Emit("view:zoom-out", null);
        });
        Add("core:zoom-reset", () => 
        {
            _state.Events.Emit("view:zoom-reset", null);
        });
        
        // Deploy action
        Add("core:deploy-flows", () => 
        {
            _state.Events.Emit("deploy:start", null);
        });
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
