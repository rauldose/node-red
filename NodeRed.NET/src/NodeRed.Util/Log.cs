// ============================================================
// INSPIRED BY: packages/node_modules/@node-red/util/lib/log.js
// ============================================================
// This implementation is inspired by Node-RED's logging system but uses
// Microsoft.Extensions.Logging abstractions for better .NET integration.
// Core concepts maintained:
// - Multiple log levels (Fatal, Error, Warn, Info, Debug, Trace, Audit, Metric)
// - Custom log handlers
// - Metrics and audit logging support
// - Message formatting similar to Node-RED
// ============================================================

/*!
 * Original work Copyright JS Foundation and other contributors, http://js.foundation
 * Modified work Copyright 2026 NodeRed.NET Contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace NodeRed.Util;

/// <summary>
/// Logging utilities inspired by @node-red/util/log.js
/// </summary>
public static class Log
{
    /// <summary>
    /// Log level constants matching Node-RED's levels
    /// </summary>
    public const int FATAL = 10;
    public const int ERROR = 20;
    public const int WARN = 30;
    public const int INFO = 40;
    public const int DEBUG = 50;
    public const int TRACE = 60;
    public const int AUDIT = 98;
    public const int METRIC = 99;

    private static readonly Dictionary<int, string> LevelNames = new()
    {
        { 10, "fatal" },
        { 20, "error" },
        { 30, "warn" },
        { 40, "info" },
        { 50, "debug" },
        { 60, "trace" },
        { 98, "audit" },
        { 99, "metric" }
    };

    private static readonly Dictionary<int, ConsoleColor> LevelColors = new()
    {
        { 10, ConsoleColor.Red },
        { 20, ConsoleColor.Red },
        { 30, ConsoleColor.Yellow },
        { 40, ConsoleColor.White },
        { 50, ConsoleColor.Cyan },
        { 60, ConsoleColor.Gray },
        { 98, ConsoleColor.White },
        { 99, ConsoleColor.White }
    };

    private static readonly List<ILogHandler> _logHandlers = new();
    private static bool _metricsEnabled = false;
    private static bool _verbose = false;

    /// <summary>
    /// Initialize the logging system
    /// </summary>
    /// <param name="settings">Logging settings</param>
    public static void Init(LogSettings? settings = null)
    {
        _metricsEnabled = false;
        _logHandlers.Clear();
        _verbose = settings?.Verbose ?? false;

        if (settings?.Handlers?.Count > 0)
        {
            foreach (var handler in settings.Handlers)
            {
                AddHandler(handler);
                _metricsEnabled = _metricsEnabled || handler.MetricsEnabled;
            }
        }
        else
        {
            // Default console handler
            AddHandler(new ConsoleLogHandler());
        }
    }

    /// <summary>
    /// Add a log handler
    /// </summary>
    public static void AddHandler(ILogHandler handler)
    {
        _logHandlers.Add(handler);
    }

    /// <summary>
    /// Remove a log handler
    /// </summary>
    public static void RemoveHandler(ILogHandler handler)
    {
        _logHandlers.Remove(handler);
    }

    /// <summary>
    /// Log a message object
    /// </summary>
    public static void LogMessage(LogMessage msg)
    {
        msg.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var handler in _logHandlers.ToList())
        {
            try
            {
                handler.Handle(msg);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in log handler: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Log a message at INFO level
    /// </summary>
    public static void Info(string message)
    {
        LogMessage(new LogMessage { Level = INFO, Message = message });
    }

    /// <summary>
    /// Log a message at WARN level
    /// </summary>
    public static void Warn(string message)
    {
        LogMessage(new LogMessage { Level = WARN, Message = message });
    }

    /// <summary>
    /// Log a message at ERROR level
    /// </summary>
    public static void Error(string message)
    {
        LogMessage(new LogMessage { Level = ERROR, Message = message });
    }

    /// <summary>
    /// Log a message at TRACE level
    /// </summary>
    public static void Trace(string message)
    {
        LogMessage(new LogMessage { Level = TRACE, Message = message });
    }

    /// <summary>
    /// Log a message at DEBUG level
    /// </summary>
    public static void Debug(string message)
    {
        LogMessage(new LogMessage { Level = DEBUG, Message = message });
    }

    /// <summary>
    /// Check if metrics are enabled
    /// </summary>
    public static bool MetricsEnabled => _metricsEnabled;

    /// <summary>
    /// Log an audit event
    /// </summary>
    public static void Audit(AuditMessage msg)
    {
        msg.Level = AUDIT;
        LogMessage(msg);
    }

    internal static string GetLevelName(int level) => LevelNames.TryGetValue(level, out var name) ? name : "unknown";
    internal static ConsoleColor GetLevelColor(int level) => LevelColors.TryGetValue(level, out var color) ? color : ConsoleColor.White;
    internal static bool IsVerbose => _verbose;
}

/// <summary>
/// Log message structure
/// </summary>
public class LogMessage
{
    public int Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? Id { get; set; }
    public long Timestamp { get; set; }
}

/// <summary>
/// Audit message structure
/// </summary>
public class AuditMessage : LogMessage
{
    public string? User { get; set; }
    public string? Path { get; set; }
    public string? Ip { get; set; }
}

/// <summary>
/// Log settings
/// </summary>
public class LogSettings
{
    public bool Verbose { get; set; }
    public List<ILogHandler> Handlers { get; set; } = new();
}

/// <summary>
/// Interface for log handlers
/// </summary>
public interface ILogHandler
{
    int LogLevel { get; }
    bool MetricsEnabled { get; }
    bool AuditEnabled { get; }
    void Handle(LogMessage message);
}

/// <summary>
/// Default console log handler inspired by Node-RED's consoleLogger
/// </summary>
public class ConsoleLogHandler : ILogHandler
{
    private static readonly string[] Months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

    public int LogLevel { get; set; } = Log.INFO;
    public bool MetricsEnabled { get; set; } = false;
    public bool AuditEnabled { get; set; } = false;

    public void Handle(LogMessage message)
    {
        if (!ShouldReportMessage(message.Level))
        {
            return;
        }

        var timestamp = DateTime.Now;
        var time = $"{timestamp.Hour:D2}:{timestamp.Minute:D2}:{timestamp.Second:D2}";
        var date = $"{timestamp.Day} {Months[timestamp.Month - 1]} {time}";
        
        var levelName = Log.GetLevelName(message.Level);
        var prefix = !string.IsNullOrEmpty(message.Type) 
            ? $"[{message.Type}:{message.Name ?? message.Id}] " 
            : "";
        
        var logLine = $"{date} - [{levelName}] {prefix}{message.Message}";

        var color = Log.GetLevelColor(message.Level);
        var previousColor = Console.ForegroundColor;
        try
        {
            if (color != ConsoleColor.White)
            {
                Console.ForegroundColor = color;
            }
            Console.WriteLine(logLine);
        }
        finally
        {
            Console.ForegroundColor = previousColor;
        }
    }

    private bool ShouldReportMessage(int msgLevel)
    {
        return (msgLevel == Log.METRIC && MetricsEnabled) ||
               (msgLevel == Log.AUDIT && AuditEnabled) ||
               msgLevel <= LogLevel;
    }
}
