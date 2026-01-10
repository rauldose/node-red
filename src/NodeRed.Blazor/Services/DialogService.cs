// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Blazor.Services;

/// <summary>
/// Dialog result returned when a dialog is closed
/// </summary>
public class DialogResult
{
    public bool Confirmed { get; set; }
    public object? Data { get; set; }

    public static DialogResult Ok(object? data = null) => new() { Confirmed = true, Data = data };
    public static DialogResult Cancel() => new() { Confirmed = false };
}

/// <summary>
/// Configuration for a dialog
/// </summary>
public class DialogOptions
{
    public string Title { get; set; } = "";
    public string? Width { get; set; }
    public bool Modal { get; set; } = true;
    public bool ShowCloseButton { get; set; } = true;
    public string ConfirmText { get; set; } = "Done";
    public string CancelText { get; set; } = "Cancel";
    public bool ShowConfirmButton { get; set; } = true;
    public bool ShowCancelButton { get; set; } = true;
}

/// <summary>
/// Represents an open dialog instance
/// </summary>
public class DialogInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DialogType { get; set; } = "";
    public DialogOptions Options { get; set; } = new();
    public object? Parameters { get; set; }
    public TaskCompletionSource<DialogResult> TaskCompletionSource { get; set; } = new();
}

/// <summary>
/// Service for managing dialogs in a centralized way
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Event raised when dialogs change
    /// </summary>
    event Action? OnChange;

    /// <summary>
    /// Gets the currently open dialogs
    /// </summary>
    IReadOnlyList<DialogInstance> OpenDialogs { get; }

    /// <summary>
    /// Shows a confirmation dialog
    /// </summary>
    Task<bool> ConfirmAsync(string message, string title = "Confirm");

    /// <summary>
    /// Shows an alert dialog
    /// </summary>
    Task AlertAsync(string message, string title = "Alert");

    /// <summary>
    /// Shows the import dialog
    /// </summary>
    Task<DialogResult> ShowImportDialogAsync();

    /// <summary>
    /// Shows the export dialog with the given JSON
    /// </summary>
    Task<DialogResult> ShowExportDialogAsync(string json);

    /// <summary>
    /// Shows the flow properties dialog
    /// </summary>
    Task<DialogResult> ShowFlowPropertiesDialogAsync(FlowPropertiesData data);

    /// <summary>
    /// Shows the group properties dialog
    /// </summary>
    Task<DialogResult> ShowGroupPropertiesDialogAsync(GroupPropertiesData data);

    /// <summary>
    /// Shows the search dialog
    /// </summary>
    Task<DialogResult> ShowSearchDialogAsync();

    /// <summary>
    /// Shows the settings dialog
    /// </summary>
    Task<DialogResult> ShowSettingsDialogAsync(SettingsData data);

    /// <summary>
    /// Shows the keyboard shortcuts dialog
    /// </summary>
    Task<DialogResult> ShowKeyboardShortcutsDialogAsync();

    /// <summary>
    /// Shows the palette management dialog
    /// </summary>
    Task<DialogResult> ShowPaletteDialogAsync();

    /// <summary>
    /// Shows a custom dialog
    /// </summary>
    Task<DialogResult> ShowDialogAsync(string dialogType, object? parameters = null, DialogOptions? options = null);

    /// <summary>
    /// Closes a dialog with a result
    /// </summary>
    void Close(string dialogId, DialogResult result);

    /// <summary>
    /// Closes all open dialogs
    /// </summary>
    void CloseAll();
}

/// <summary>
/// Data for flow properties dialog
/// </summary>
public class FlowPropertiesData
{
    public string FlowId { get; set; } = "";
    public string Label { get; set; } = "";
    public string Info { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Data for group properties dialog
/// </summary>
public class GroupPropertiesData
{
    public string GroupId { get; set; } = "";
    public string Name { get; set; } = "";
    public string FillColor { get; set; } = "rgba(255, 204, 204, 0.3)";
    public string StrokeColor { get; set; } = "#FF9999";
}

/// <summary>
/// Data for settings dialog
/// </summary>
public class SettingsData
{
    public bool ShowGrid { get; set; } = true;
    public bool SnapToGrid { get; set; } = true;
    public int GridSize { get; set; } = 20;
}

/// <summary>
/// Implementation of the dialog service
/// </summary>
public class DialogService : IDialogService
{
    private readonly List<DialogInstance> _dialogs = new();
    private readonly object _lock = new();

    public event Action? OnChange;

    public IReadOnlyList<DialogInstance> OpenDialogs
    {
        get
        {
            lock (_lock)
            {
                return _dialogs.ToList().AsReadOnly();
            }
        }
    }

    public async Task<bool> ConfirmAsync(string message, string title = "Confirm")
    {
        var result = await ShowDialogAsync("confirm", message, new DialogOptions
        {
            Title = title,
            ConfirmText = "OK",
            CancelText = "Cancel"
        });
        return result.Confirmed;
    }

    public async Task AlertAsync(string message, string title = "Alert")
    {
        await ShowDialogAsync("alert", message, new DialogOptions
        {
            Title = title,
            ConfirmText = "OK",
            ShowCancelButton = false
        });
    }

    public Task<DialogResult> ShowImportDialogAsync()
    {
        return ShowDialogAsync("import", null, new DialogOptions
        {
            Title = "Import nodes",
            ConfirmText = "Import",
            Width = "600px"
        });
    }

    public Task<DialogResult> ShowExportDialogAsync(string json)
    {
        return ShowDialogAsync("export", json, new DialogOptions
        {
            Title = "Export nodes",
            ShowConfirmButton = false,
            CancelText = "Close",
            Width = "600px"
        });
    }

    public Task<DialogResult> ShowFlowPropertiesDialogAsync(FlowPropertiesData data)
    {
        return ShowDialogAsync("flowProperties", data, new DialogOptions
        {
            Title = "Edit flow properties",
            Width = "500px"
        });
    }

    public Task<DialogResult> ShowGroupPropertiesDialogAsync(GroupPropertiesData data)
    {
        return ShowDialogAsync("groupProperties", data, new DialogOptions
        {
            Title = "Edit Group",
            Width = "450px"
        });
    }

    public Task<DialogResult> ShowSearchDialogAsync()
    {
        return ShowDialogAsync("search", null, new DialogOptions
        {
            Title = "Search flows",
            ShowConfirmButton = false,
            ShowCancelButton = false,
            Width = "500px"
        });
    }

    public Task<DialogResult> ShowSettingsDialogAsync(SettingsData data)
    {
        return ShowDialogAsync("settings", data, new DialogOptions
        {
            Title = "Settings",
            Width = "500px"
        });
    }

    public Task<DialogResult> ShowKeyboardShortcutsDialogAsync()
    {
        return ShowDialogAsync("keyboardShortcuts", null, new DialogOptions
        {
            Title = "Keyboard Shortcuts",
            ShowConfirmButton = false,
            CancelText = "Close",
            Width = "500px"
        });
    }

    public Task<DialogResult> ShowPaletteDialogAsync()
    {
        return ShowDialogAsync("palette", null, new DialogOptions
        {
            Title = "Manage palette",
            ShowConfirmButton = false,
            CancelText = "Close",
            Width = "700px"
        });
    }

    public Task<DialogResult> ShowDialogAsync(string dialogType, object? parameters = null, DialogOptions? options = null)
    {
        var dialog = new DialogInstance
        {
            DialogType = dialogType,
            Parameters = parameters,
            Options = options ?? new DialogOptions()
        };

        lock (_lock)
        {
            _dialogs.Add(dialog);
        }

        OnChange?.Invoke();
        return dialog.TaskCompletionSource.Task;
    }

    public void Close(string dialogId, DialogResult result)
    {
        DialogInstance? dialog;
        lock (_lock)
        {
            dialog = _dialogs.FirstOrDefault(d => d.Id == dialogId);
            if (dialog != null)
            {
                _dialogs.Remove(dialog);
            }
        }

        if (dialog != null)
        {
            dialog.TaskCompletionSource.TrySetResult(result);
        }

        OnChange?.Invoke();
    }

    public void CloseAll()
    {
        List<DialogInstance> dialogsToClose;
        lock (_lock)
        {
            dialogsToClose = _dialogs.ToList();
            _dialogs.Clear();
        }

        foreach (var dialog in dialogsToClose)
        {
            dialog.TaskCompletionSource.TrySetResult(DialogResult.Cancel());
        }

        OnChange?.Invoke();
    }
}
