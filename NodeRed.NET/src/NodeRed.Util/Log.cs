// ============================================================
// SOURCE: packages/node_modules/@node-red/util/lib/log.js
// LINES: 1-266
// ============================================================
// ORIGINAL CODE:
// ------------------------------------------------------------
// var levels = {
//     off:    1,
//     fatal:  10,
//     error:  20,
//     warn:   30,
//     info:   40,
//     debug:  50,
//     trace:  60,
//     audit:  98,
//     metric: 99
// };
//
// var levelNames = {
//     10: "fatal",
//     20: "error",
//     30: "warn",
//     40: "info",
//     50: "debug",
//     60: "trace",
//     98: "audit",
//     99: "metric"
// };
//
// var LogHandler = function(settings) {
//     this.logLevel  = settings ? levels[settings.level]||levels.info : levels.info;
//     this.metricsOn = settings ? settings.metrics||false : false;
//     this.auditOn = settings ? settings.audit||false : false;
//     metricsEnabled = metricsEnabled || this.metricsOn;
//     this.handler   = (settings && settings.handler) ? settings.handler(settings) : consoleLogger;
//     this.on("log",function(msg) {
//         if (this.shouldReportMessage(msg.level)) {
//             this.handler(msg);
//         }
//     });
// }
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
using System.Text.Json;

namespace NodeRed.Util
{
    /// <summary>
    /// Log levels matching Node-RED's log level values.
    /// </summary>
    public static class LogLevel
    {
        public const int Off = 1;
        public const int Fatal = 10;
        public const int Error = 20;
        public const int Warn = 30;
        public const int Info = 40;
        public const int Debug = 50;
        public const int Trace = 60;
        public const int Audit = 98;
        public const int Metric = 99;
    }

    /// <summary>
    /// Log message structure.
    /// </summary>
    public class LogMessage
    {
        public int Level { get; set; }
        public object? Msg { get; set; }
        public string? Type { get; set; }
        public string? Name { get; set; }
        public string? Id { get; set; }
        public string? Path { get; set; }
        public long Timestamp { get; set; }
        public string? Event { get; set; }
        public object? Value { get; set; }
        public string? User { get; set; }
        public string? Ip { get; set; }
    }

    /// <summary>
    /// Log handler settings.
    /// </summary>
    public class LogHandlerSettings
    {
        public string? Level { get; set; }
        public bool Metrics { get; set; }
        public bool Audit { get; set; }
        public Func<LogHandlerSettings, Action<LogMessage>>? Handler { get; set; }
    }

    /// <summary>
    /// Log handler that processes log messages.
    /// </summary>
    public class LogHandler
    {
        private static readonly Dictionary<string, int> Levels = new(StringComparer.OrdinalIgnoreCase)
        {
            { "off", LogLevel.Off },
            { "fatal", LogLevel.Fatal },
            { "error", LogLevel.Error },
            { "warn", LogLevel.Warn },
            { "info", LogLevel.Info },
            { "debug", LogLevel.Debug },
            { "trace", LogLevel.Trace },
            { "audit", LogLevel.Audit },
            { "metric", LogLevel.Metric }
        };

        public int LogLevelValue { get; }
        public bool MetricsOn { get; }
        public bool AuditOn { get; }
        private readonly Action<LogMessage> _handler;

        public LogHandler(LogHandlerSettings? settings = null)
        {
            if (settings != null)
            {
                LogLevelValue = !string.IsNullOrEmpty(settings.Level) && Levels.TryGetValue(settings.Level, out var level)
                    ? level
                    : LogLevel.Info;
                MetricsOn = settings.Metrics;
                AuditOn = settings.Audit;
                _handler = settings.Handler != null ? settings.Handler(settings) : ConsoleLogger;
            }
            else
            {
                LogLevelValue = LogLevel.Info;
                MetricsOn = false;
                AuditOn = false;
                _handler = ConsoleLogger;
            }

            Log.UpdateMetricsEnabled(MetricsOn);
        }

        public bool ShouldReportMessage(int msgLevel)
        {
            return (msgLevel == LogLevel.Metric && MetricsOn) ||
                   (msgLevel == LogLevel.Audit && AuditOn) ||
                   msgLevel <= LogLevelValue;
        }

        public void HandleLog(LogMessage msg)
        {
            if (ShouldReportMessage(msg.Level))
            {
                _handler(msg);
            }
        }

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

        private static readonly string[] Months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        private static void ConsoleLogger(LogMessage msg)
        {
            var d = DateTime.Now;
            var time = $"{d.Hour:D2}:{d.Minute:D2}:{d.Second:D2}";
            var levelName = LevelNames.TryGetValue(msg.Level, out var name) ? name : "unknown";

            string logLine;
            if (msg.Level == LogLevel.Metric || msg.Level == LogLevel.Audit)
            {
                logLine = $"{d.Day} {Months[d.Month - 1]} {time} - [{levelName}] {JsonSerializer.Serialize(msg)}";
            }
            else
            {
                var typeInfo = !string.IsNullOrEmpty(msg.Type)
                    ? $"[{msg.Type}:{msg.Name ?? msg.Id}] "
                    : "";
                var message = msg.Msg?.ToString() ?? "";
                logLine = $"{d.Day} {Months[d.Month - 1]} {time} - [{levelName}] {typeInfo}{message}";
            }

            if (LevelColors.TryGetValue(msg.Level, out var color))
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(logLine);
                Console.ForegroundColor = originalColor;
            }
            else
            {
                Console.WriteLine(logLine);
            }
        }
    }

    /// <summary>
    /// Logging utilities for Node-RED.
    /// Provides logging functionality with multiple handlers and log levels.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/util/lib/log.js
    /// </remarks>
    public static class Log
    {
        // Log level constants exposed for external use
        public const int FATAL = LogLevel.Fatal;
        public const int ERROR = LogLevel.Error;
        public const int WARN = LogLevel.Warn;
        public const int INFO = LogLevel.Info;
        public const int DEBUG = LogLevel.Debug;
        public const int TRACE = LogLevel.Trace;
        public const int AUDIT = LogLevel.Audit;
        public const int METRIC = LogLevel.Metric;

        private static readonly List<LogHandler> _logHandlers = new();
        private static bool _metricsEnabled = false;
        private static bool _verbose = false;

        /// <summary>
        /// I18n translation function reference.
        /// </summary>
        public static Func<string, object?, string> _ { get; set; } = (key, data) => key;

        /// <summary>
        /// Initialize the logging system with settings.
        /// </summary>
        /// <param name="settings">The logging settings.</param>
        public static void Init(LogSettings settings)
        {
            _metricsEnabled = false;
            _logHandlers.Clear();
            _verbose = settings.Verbose;

            if (settings.Logging != null && settings.Logging.Count > 0)
            {
                foreach (var kvp in settings.Logging)
                {
                    var config = kvp.Value;
                    if (kvp.Key == "console" || config?.Handler != null)
                    {
                        AddHandler(new LogHandler(config));
                    }
                }
            }
            else
            {
                AddHandler(new LogHandler());
            }
        }

        /// <summary>
        /// Add a log handler.
        /// </summary>
        /// <param name="handler">The log handler to add.</param>
        public static void AddHandler(LogHandler handler)
        {
            _logHandlers.Add(handler);
        }

        /// <summary>
        /// Remove a log handler.
        /// </summary>
        /// <param name="handler">The log handler to remove.</param>
        public static void RemoveHandler(LogHandler handler)
        {
            _logHandlers.Remove(handler);
        }

        /// <summary>
        /// Log a message object.
        /// </summary>
        /// <param name="msg">The log message.</param>
        public static void LogMessage(LogMessage msg)
        {
            msg.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var handler in _logHandlers)
            {
                handler.HandleLog(msg);
            }
        }

        /// <summary>
        /// Log a message at INFO level.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        public static void Info(object msg)
        {
            LogMessage(new LogMessage { Level = INFO, Msg = msg });
        }

        /// <summary>
        /// Log a message at WARN level.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        public static void Warn(object msg)
        {
            LogMessage(new LogMessage { Level = WARN, Msg = msg });
        }

        /// <summary>
        /// Log a message at ERROR level.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        public static void Error(object msg)
        {
            LogMessage(new LogMessage { Level = ERROR, Msg = msg });
        }

        /// <summary>
        /// Log a message at TRACE level.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        public static void Trace(object msg)
        {
            LogMessage(new LogMessage { Level = TRACE, Msg = msg });
        }

        /// <summary>
        /// Log a message at DEBUG level.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        public static void Debug(object msg)
        {
            LogMessage(new LogMessage { Level = DEBUG, Msg = msg });
        }

        /// <summary>
        /// Check if metrics are enabled.
        /// </summary>
        /// <returns>True if metrics are enabled.</returns>
        public static bool Metric()
        {
            return _metricsEnabled;
        }

        /// <summary>
        /// Update metrics enabled state.
        /// </summary>
        internal static void UpdateMetricsEnabled(bool enabled)
        {
            _metricsEnabled = _metricsEnabled || enabled;
        }

        /// <summary>
        /// Log an audit event.
        /// </summary>
        /// <param name="msg">The audit message.</param>
        /// <param name="req">Optional HTTP request information.</param>
        public static void Audit(LogMessage msg, HttpRequestInfo? req = null)
        {
            msg.Level = AUDIT;
            if (req != null)
            {
                msg.User = req.User;
                msg.Path = req.Path;
                msg.Ip = req.Ip ?? req.XForwardedFor ?? req.RemoteAddress;
            }
            LogMessage(msg);
        }
    }

    /// <summary>
    /// Log settings for initialization.
    /// </summary>
    public class LogSettings
    {
        public bool Verbose { get; set; }
        public Dictionary<string, LogHandlerSettings?>? Logging { get; set; }
    }

    /// <summary>
    /// HTTP request information for audit logging.
    /// </summary>
    public class HttpRequestInfo
    {
        public string? User { get; set; }
        public string? Path { get; set; }
        public string? Ip { get; set; }
        public string? XForwardedFor { get; set; }
        public string? RemoteAddress { get; set; }
    }
}
// ------------------------------------------------------------
// MAPPING NOTES:
// - levels object → LogLevel static class with constants
// - levelNames object → LevelNames Dictionary
// - levelColours → LevelColors Dictionary (ConsoleColor enum)
// - LogHandler function → LogHandler class
// - LogHandler.prototype.shouldReportMessage → ShouldReportMessage method
// - util.inherits(LogHandler, EventEmitter) → Event handled via HandleLog method
// - consoleLogger function → ConsoleLogger private static method
// - utilLog function → Integrated into ConsoleLogger
// - log.init → Log.Init static method
// - log.addHandler → Log.AddHandler static method
// - log.removeHandler → Log.RemoveHandler static method
// - log.log → Log.LogMessage static method
// - log.info/warn/error/trace/debug → Log.Info/Warn/Error/Trace/Debug
// - log.metric → Log.Metric method
// - log.audit → Log.Audit method
// - log._ → Log._ property (i18n reference)
// - Date.now() → DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
// - chalk color library → Console.ForegroundColor
// ============================================================
