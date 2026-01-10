// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Microsoft.JSInterop;

namespace NodeRed.Blazor.Services;

/// <summary>
/// Service for clipboard operations using JavaScript interop.
/// Provides copy-to-clipboard functionality for the editor.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copies the specified text to the system clipboard.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    /// <returns>True if the copy was successful, false otherwise.</returns>
    Task<bool> CopyToClipboardAsync(string text);

    /// <summary>
    /// Reads text from the system clipboard.
    /// </summary>
    /// <returns>The clipboard text, or null if unavailable.</returns>
    Task<string?> ReadFromClipboardAsync();
}

/// <summary>
/// Implementation of clipboard service using JavaScript interop.
/// </summary>
public class ClipboardService : IClipboardService
{
    private readonly IJSRuntime _jsRuntime;

    public ClipboardService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc/>
    public async Task<bool> CopyToClipboardAsync(string text)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
            return true;
        }
        catch (Exception)
        {
            // Clipboard API may not be available in all contexts (e.g., non-HTTPS)
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<string?> ReadFromClipboardAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("navigator.clipboard.readText");
        }
        catch (Exception)
        {
            // Clipboard API may not be available or permission denied
            return null;
        }
    }
}
