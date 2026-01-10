// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Blazor.Services;

/// <summary>
/// Notification types matching the JS RED.notify types
/// </summary>
public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error,
    Compact
}

/// <summary>
/// Represents a notification message
/// </summary>
public class Notification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Message { get; set; } = "";
    public NotificationType Type { get; set; } = NotificationType.Info;
    public bool Fixed { get; set; } = false;
    public bool Modal { get; set; } = false;
    public int Timeout { get; set; } = 5000; // milliseconds
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsVisible { get; set; } = true;
    public List<NotificationButton>? Buttons { get; set; }
}

/// <summary>
/// Represents a button in a notification
/// </summary>
public class NotificationButton
{
    public string Text { get; set; } = "";
    public bool IsPrimary { get; set; } = false;
    public Action? OnClick { get; set; }
}

/// <summary>
/// Service for displaying notifications/toasts matching the JS RED.notify functionality
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Event raised when notifications change
    /// </summary>
    event Action? OnChange;

    /// <summary>
    /// Gets the current list of active notifications
    /// </summary>
    IReadOnlyList<Notification> Notifications { get; }

    /// <summary>
    /// Shows a notification message
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="type">The notification type</param>
    /// <param name="timeout">Timeout in milliseconds (0 for no timeout)</param>
    /// <returns>The notification instance</returns>
    Notification Notify(string message, NotificationType type = NotificationType.Info, int timeout = 5000);

    /// <summary>
    /// Shows a success notification
    /// </summary>
    Notification Success(string message, int timeout = 5000);

    /// <summary>
    /// Shows an error notification
    /// </summary>
    Notification Error(string message, int timeout = 0);

    /// <summary>
    /// Shows a warning notification
    /// </summary>
    Notification Warning(string message, int timeout = 8000);

    /// <summary>
    /// Shows a notification with buttons
    /// </summary>
    Notification NotifyWithButtons(string message, NotificationType type, List<NotificationButton> buttons, bool modal = false);

    /// <summary>
    /// Closes a specific notification
    /// </summary>
    void Close(string notificationId);

    /// <summary>
    /// Closes all notifications
    /// </summary>
    void CloseAll();

    /// <summary>
    /// Updates an existing notification
    /// </summary>
    void Update(string notificationId, string message, NotificationType? type = null);
}

/// <summary>
/// Implementation of the notification service
/// </summary>
public class NotificationService : INotificationService, IDisposable
{
    private readonly List<Notification> _notifications = new();
    private readonly object _lock = new();
    private readonly Dictionary<string, Timer> _timers = new();

    public event Action? OnChange;

    public IReadOnlyList<Notification> Notifications
    {
        get
        {
            lock (_lock)
            {
                return _notifications.Where(n => n.IsVisible).ToList().AsReadOnly();
            }
        }
    }

    public Notification Notify(string message, NotificationType type = NotificationType.Info, int timeout = 5000)
    {
        var notification = new Notification
        {
            Message = message,
            Type = type,
            Timeout = timeout,
            Fixed = timeout == 0
        };

        AddNotification(notification);
        return notification;
    }

    public Notification Success(string message, int timeout = 5000)
    {
        return Notify(message, NotificationType.Success, timeout);
    }

    public Notification Error(string message, int timeout = 0)
    {
        return Notify(message, NotificationType.Error, timeout);
    }

    public Notification Warning(string message, int timeout = 8000)
    {
        return Notify(message, NotificationType.Warning, timeout);
    }

    public Notification NotifyWithButtons(string message, NotificationType type, List<NotificationButton> buttons, bool modal = false)
    {
        var notification = new Notification
        {
            Message = message,
            Type = type,
            Buttons = buttons,
            Modal = modal,
            Fixed = true,
            Timeout = 0
        };

        AddNotification(notification);
        return notification;
    }

    public void Close(string notificationId)
    {
        lock (_lock)
        {
            var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
            if (notification != null)
            {
                notification.IsVisible = false;
                _notifications.Remove(notification);
                
                if (_timers.TryGetValue(notificationId, out var timer))
                {
                    timer.Dispose();
                    _timers.Remove(notificationId);
                }
            }
        }
        OnChange?.Invoke();
    }

    public void CloseAll()
    {
        lock (_lock)
        {
            foreach (var timer in _timers.Values)
            {
                timer.Dispose();
            }
            _timers.Clear();
            _notifications.Clear();
        }
        OnChange?.Invoke();
    }

    public void Update(string notificationId, string message, NotificationType? type = null)
    {
        lock (_lock)
        {
            var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
            if (notification != null)
            {
                notification.Message = message;
                if (type.HasValue)
                {
                    notification.Type = type.Value;
                }
            }
        }
        OnChange?.Invoke();
    }

    private void AddNotification(Notification notification)
    {
        lock (_lock)
        {
            // Limit to 5 visible notifications (matching JS behavior)
            while (_notifications.Count >= 5)
            {
                var oldest = _notifications.FirstOrDefault(n => !n.Fixed);
                if (oldest != null)
                {
                    Close(oldest.Id);
                }
                else
                {
                    break;
                }
            }

            _notifications.Add(notification);

            // Set up auto-close timer if timeout is set
            if (notification.Timeout > 0 && !notification.Fixed)
            {
                var timer = new Timer(_ => Close(notification.Id), null, notification.Timeout, Timeout.Infinite);
                _timers[notification.Id] = timer;
            }
        }
        OnChange?.Invoke();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var timer in _timers.Values)
            {
                timer.Dispose();
            }
            _timers.Clear();
        }
    }
}
