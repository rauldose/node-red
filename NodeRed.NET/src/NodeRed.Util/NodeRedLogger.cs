// ============================================================
// INSPIRED BY: @node-red/util/lib/log.js
// REFERENCE: NODE-RED-ARCHITECTURE-ANALYSIS.md - Logging section
// ============================================================
// Uses Microsoft.Extensions.Logging for integration with .NET ecosystem
// while maintaining Node-RED's log level semantics
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

using Microsoft.Extensions.Logging;

namespace NodeRed.Util;

/// <summary>
/// Log level constants matching Node-RED's levels
/// Reference: NODE-RED-ARCHITECTURE-ANALYSIS.md - Logging System
/// </summary>
public static class NodeRedLogLevel
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
/// Log message structure matching Node-RED's message format
/// </summary>
public class NodeRedLogMessage
{
    public int Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? Id { get; set; }
    public long Timestamp { get; set; }
    public Exception? Exception { get; set; }
}

/// <summary>
/// Audit message structure for audit logging
/// </summary>
public class AuditLogMessage : NodeRedLogMessage
{
    public string? User { get; set; }
    public string? Path { get; set; }
    public string? IpAddress { get; set; }
    public Dictionary<string, object>? AdditionalData { get; set; }
}

/// <summary>
/// Logging service that bridges Node-RED logging concepts with .NET ILogger
/// </summary>
public class NodeRedLogger
{
    private readonly ILogger<NodeRedLogger> _logger;
    private bool _metricsEnabled;
    private bool _auditEnabled;

    public NodeRedLogger(ILogger<NodeRedLogger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Configure logging settings
    /// </summary>
    public void Configure(bool metricsEnabled = false, bool auditEnabled = false)
    {
        _metricsEnabled = metricsEnabled;
        _auditEnabled = auditEnabled;
    }

    /// <summary>
    /// Check if metrics are enabled
    /// </summary>
    public bool MetricsEnabled => _metricsEnabled;

    /// <summary>
    /// Check if audit is enabled
    /// </summary>
    public bool AuditEnabled => _auditEnabled;

    /// <summary>
    /// Log a message at FATAL level
    /// </summary>
    public void Fatal(string message, string? type = null, string? name = null, string? id = null)
    {
        LogMessage(new NodeRedLogMessage
        {
            Level = NodeRedLogLevel.Fatal,
            Message = message,
            Type = type,
            Name = name,
            Id = id
        });
    }

    /// <summary>
    /// Log a message at ERROR level
    /// Maps to: log.error() from Node-RED
    /// </summary>
    public void Error(string message, Exception? exception = null, string? type = null, string? name = null, string? id = null)
    {
        LogMessage(new NodeRedLogMessage
        {
            Level = NodeRedLogLevel.Error,
            Message = message,
            Exception = exception,
            Type = type,
            Name = name,
            Id = id
        });
    }

    /// <summary>
    /// Log a message at WARN level
    /// Maps to: log.warn() from Node-RED
    /// </summary>
    public void Warn(string message, string? type = null, string? name = null, string? id = null)
    {
        LogMessage(new NodeRedLogMessage
        {
            Level = NodeRedLogLevel.Warn,
            Message = message,
            Type = type,
            Name = name,
            Id = id
        });
    }

    /// <summary>
    /// Log a message at INFO level
    /// Maps to: log.info() from Node-RED
    /// </summary>
    public void Info(string message, string? type = null, string? name = null, string? id = null)
    {
        LogMessage(new NodeRedLogMessage
        {
            Level = NodeRedLogLevel.Info,
            Message = message,
            Type = type,
            Name = name,
            Id = id
        });
    }

    /// <summary>
    /// Log a message at DEBUG level
    /// Maps to: log.debug() from Node-RED
    /// </summary>
    public void Debug(string message, string? type = null, string? name = null, string? id = null)
    {
        LogMessage(new NodeRedLogMessage
        {
            Level = NodeRedLogLevel.Debug,
            Message = message,
            Type = type,
            Name = name,
            Id = id
        });
    }

    /// <summary>
    /// Log a message at TRACE level
    /// Maps to: log.trace() from Node-RED
    /// </summary>
    public void Trace(string message, string? type = null, string? name = null, string? id = null)
    {
        LogMessage(new NodeRedLogMessage
        {
            Level = NodeRedLogLevel.Trace,
            Message = message,
            Type = type,
            Name = name,
            Id = id
        });
    }

    /// <summary>
    /// Log an audit event
    /// Maps to: log.audit() from Node-RED
    /// </summary>
    public void Audit(AuditLogMessage auditMessage)
    {
        if (!_auditEnabled)
            return;

        auditMessage.Level = NodeRedLogLevel.Audit;
        LogMessage(auditMessage);
    }

    /// <summary>
    /// Log a metric (if metrics are enabled)
    /// </summary>
    public void Metric(string message, Dictionary<string, object>? metrics = null)
    {
        if (!_metricsEnabled)
            return;

        var metricsJson = metrics != null ? System.Text.Json.JsonSerializer.Serialize(metrics) : string.Empty;
        LogMessage(new NodeRedLogMessage
        {
            Level = NodeRedLogLevel.Metric,
            Message = $"{message} {metricsJson}"
        });
    }

    private void LogMessage(NodeRedLogMessage msg)
    {
        msg.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var prefix = !string.IsNullOrEmpty(msg.Type)
            ? $"[{msg.Type}:{msg.Name ?? msg.Id}] "
            : "";

        var logMessage = $"{prefix}{msg.Message}";

        // Map Node-RED levels to Microsoft.Extensions.Logging levels
        switch (msg.Level)
        {
            case NodeRedLogLevel.Fatal:
            case NodeRedLogLevel.Error:
                if (msg.Exception != null)
                    _logger.LogError(msg.Exception, logMessage);
                else
                    _logger.LogError(logMessage);
                break;
            case NodeRedLogLevel.Warn:
                _logger.LogWarning(logMessage);
                break;
            case NodeRedLogLevel.Info:
                _logger.LogInformation(logMessage);
                break;
            case NodeRedLogLevel.Debug:
                _logger.LogDebug(logMessage);
                break;
            case NodeRedLogLevel.Trace:
                _logger.LogTrace(logMessage);
                break;
            case NodeRedLogLevel.Audit:
                // Audit logs as Information level with [AUDIT] prefix
                if (msg is AuditLogMessage auditMsg)
                {
                    var auditPrefix = $"[AUDIT] User:{auditMsg.User} Path:{auditMsg.Path} IP:{auditMsg.IpAddress} ";
                    _logger.LogInformation(auditPrefix + logMessage);
                }
                break;
            case NodeRedLogLevel.Metric:
                // Metrics as Debug level with [METRIC] prefix
                _logger.LogDebug($"[METRIC] {logMessage}");
                break;
        }
    }
}
