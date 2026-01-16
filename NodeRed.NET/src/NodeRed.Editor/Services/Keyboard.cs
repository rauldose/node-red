// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-client/src/js/ui/keyboard.js
// ============================================================
// TRANSLATION: JavaScript keyboard module to C# service
// ============================================================

namespace NodeRed.Editor.Services;

/// <summary>
/// Keyboard shortcut management service.
/// Translated from RED.keyboard module.
/// </summary>
public class Keyboard
{
    private readonly EditorState _state;
    private readonly Dictionary<string, Action> _handlers = new();
    private readonly Dictionary<string, List<KeyBinding>> _actionToKeyMap = new();
    private readonly Dictionary<string, string> _keyToActionMap = new();
    private bool _handlersActive = true;

    // Key code mappings (translated from keyMap in keyboard.js)
    private static readonly Dictionary<string, int> KeyMap = new()
    {
        ["left"] = 37,
        ["up"] = 38,
        ["right"] = 39,
        ["down"] = 40,
        ["escape"] = 27,
        ["enter"] = 13,
        ["backspace"] = 8,
        ["delete"] = 46,
        ["space"] = 32,
        ["tab"] = 9
    };

    public Keyboard(EditorState state)
    {
        _state = state;
        InitializeDefaultBindings();
    }

    /// <summary>
    /// Initialize default keyboard bindings.
    /// Translated from init() in keyboard.js
    /// </summary>
    private void InitializeDefaultBindings()
    {
        // Core editing shortcuts
        AddBinding("ctrl+c", "core:copy-selection-to-internal-clipboard");
        AddBinding("ctrl+x", "core:cut-selection-to-internal-clipboard");
        AddBinding("ctrl+v", "core:paste-from-internal-clipboard");
        AddBinding("ctrl+z", "core:undo");
        AddBinding("ctrl+shift+z", "core:redo");
        AddBinding("ctrl+y", "core:redo");
        AddBinding("ctrl+a", "core:select-all-nodes");
        AddBinding("delete", "core:delete-selection");
        AddBinding("backspace", "core:delete-selection");

        // Navigation
        AddBinding("ctrl+f", "core:search");
        AddBinding("escape", "core:cancel");
        AddBinding("space", "core:toggle-sidebar");

        // Node operations
        AddBinding("ctrl+d", "core:deploy");
        AddBinding("ctrl+shift+d", "core:show-deploy-dialog");
        AddBinding("ctrl+e", "core:show-export-dialog");
        AddBinding("ctrl+i", "core:show-import-dialog");

        // Groups
        AddBinding("ctrl+shift+g", "core:group-selection");
        AddBinding("ctrl+shift+u", "core:ungroup-selection");

        // Quick add
        AddBinding("ctrl+shift+p", "core:show-action-list");

        // Arrow key navigation
        AddBinding("left", "core:move-selection-left");
        AddBinding("right", "core:move-selection-right");
        AddBinding("up", "core:move-selection-up");
        AddBinding("down", "core:move-selection-down");
        AddBinding("shift+left", "core:step-selection-left");
        AddBinding("shift+right", "core:step-selection-right");
        AddBinding("shift+up", "core:step-selection-up");
        AddBinding("shift+down", "core:step-selection-down");
    }

    /// <summary>
    /// Add a keyboard binding.
    /// Translated from add() in keyboard.js
    /// </summary>
    public void AddBinding(string key, string action, string scope = "*")
    {
        var normalizedKey = NormalizeKey(key);
        var binding = new KeyBinding
        {
            Key = normalizedKey,
            Action = action,
            Scope = scope
        };

        if (!_actionToKeyMap.ContainsKey(action))
        {
            _actionToKeyMap[action] = new List<KeyBinding>();
        }
        _actionToKeyMap[action].Add(binding);
        _keyToActionMap[$"{scope}:{normalizedKey}"] = action;
    }

    /// <summary>
    /// Register an action handler.
    /// Translated from addHandler() in keyboard.js
    /// </summary>
    public void AddHandler(string action, Action handler)
    {
        _handlers[action] = handler;
    }

    /// <summary>
    /// Handle a key event.
    /// Translated from handleKeyEvent() in keyboard.js
    /// </summary>
    public bool HandleKeyEvent(string key, bool ctrl, bool shift, bool alt, string scope = "*")
    {
        if (!_handlersActive) return false;

        var modifiers = new List<string>();
        if (ctrl) modifiers.Add("ctrl");
        if (shift) modifiers.Add("shift");
        if (alt) modifiers.Add("alt");

        var normalizedKey = string.Join("+", modifiers.Concat(new[] { key.ToLowerInvariant() }));

        // Try scope-specific binding first
        if (_keyToActionMap.TryGetValue($"{scope}:{normalizedKey}", out var action))
        {
            if (_handlers.TryGetValue(action, out var handler))
            {
                handler();
                return true;
            }
        }

        // Try global binding
        if (_keyToActionMap.TryGetValue($"*:{normalizedKey}", out action))
        {
            if (_handlers.TryGetValue(action, out var handler))
            {
                handler();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get the key binding for an action.
    /// Translated from getShortcut() in keyboard.js
    /// </summary>
    public string? GetShortcut(string action)
    {
        if (_actionToKeyMap.TryGetValue(action, out var bindings) && bindings.Count > 0)
        {
            return FormatKeyDisplay(bindings[0].Key);
        }
        return null;
    }

    /// <summary>
    /// Get all shortcuts.
    /// Translated from getShortcuts() in keyboard.js
    /// </summary>
    public Dictionary<string, string> GetAllShortcuts()
    {
        var result = new Dictionary<string, string>();
        foreach (var kvp in _actionToKeyMap)
        {
            if (kvp.Value.Count > 0)
            {
                result[kvp.Key] = FormatKeyDisplay(kvp.Value[0].Key);
            }
        }
        return result;
    }

    /// <summary>
    /// Remove a binding.
    /// Translated from remove() in keyboard.js
    /// </summary>
    public void RemoveBinding(string key, string scope = "*")
    {
        var normalizedKey = NormalizeKey(key);
        var lookupKey = $"{scope}:{normalizedKey}";
        
        if (_keyToActionMap.TryGetValue(lookupKey, out var action))
        {
            _keyToActionMap.Remove(lookupKey);
            if (_actionToKeyMap.TryGetValue(action, out var bindings))
            {
                bindings.RemoveAll(b => b.Key == normalizedKey && b.Scope == scope);
            }
        }
    }

    /// <summary>
    /// Enable keyboard handlers.
    /// </summary>
    public void Enable() => _handlersActive = true;

    /// <summary>
    /// Disable keyboard handlers.
    /// </summary>
    public void Disable() => _handlersActive = false;

    private string NormalizeKey(string key)
    {
        return key.ToLowerInvariant().Replace(" ", "");
    }

    private string FormatKeyDisplay(string key)
    {
        var parts = key.Split('+');
        var formatted = parts.Select(p => p switch
        {
            "ctrl" => "Ctrl",
            "shift" => "Shift",
            "alt" => "Alt",
            "delete" => "Del",
            "backspace" => "Backspace",
            "escape" => "Esc",
            "enter" => "Enter",
            "space" => "Space",
            _ => p.Length == 1 ? p.ToUpperInvariant() : char.ToUpper(p[0]) + p[1..]
        });
        return string.Join("+", formatted);
    }
}

/// <summary>
/// Represents a keyboard binding.
/// </summary>
public class KeyBinding
{
    public string Key { get; set; } = "";
    public string Action { get; set; } = "";
    public string Scope { get; set; } = "*";
}
