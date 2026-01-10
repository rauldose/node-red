// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Core.Entities;

/// <summary>
/// Comprehensive diagnostics report for the Node-RED runtime.
/// </summary>
public class DiagnosticsReport
{
    /// <summary>
    /// Report type identifier.
    /// </summary>
    public string Report { get; set; } = "diagnostics";

    /// <summary>
    /// Scope of the report (e.g., "admin", "user").
    /// </summary>
    public string Scope { get; set; } = "user";

    /// <summary>
    /// Time information.
    /// </summary>
    public TimeInfo Time { get; set; } = new();

    /// <summary>
    /// Internationalization information.
    /// </summary>
    public IntlInfo Intl { get; set; } = new();

    /// <summary>
    /// .NET runtime information.
    /// </summary>
    public DotNetInfo DotNet { get; set; } = new();

    /// <summary>
    /// Operating system information.
    /// </summary>
    public OsInfo Os { get; set; } = new();

    /// <summary>
    /// Node-RED runtime information.
    /// </summary>
    public RuntimeInfo Runtime { get; set; } = new();
}

/// <summary>
/// Time information for diagnostics.
/// </summary>
public class TimeInfo
{
    /// <summary>
    /// UTC time string.
    /// </summary>
    public string Utc { get; set; } = string.Empty;

    /// <summary>
    /// Local time string.
    /// </summary>
    public string Local { get; set; } = string.Empty;

    /// <summary>
    /// Uptime in seconds.
    /// </summary>
    public double UptimeSeconds { get; set; }
}

/// <summary>
/// Internationalization information.
/// </summary>
public class IntlInfo
{
    /// <summary>
    /// Current locale.
    /// </summary>
    public string Locale { get; set; } = string.Empty;

    /// <summary>
    /// Current time zone.
    /// </summary>
    public string TimeZone { get; set; } = string.Empty;
}

/// <summary>
/// .NET runtime information.
/// </summary>
public class DotNetInfo
{
    /// <summary>
    /// .NET version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Framework description.
    /// </summary>
    public string FrameworkDescription { get; set; } = string.Empty;

    /// <summary>
    /// Runtime identifier.
    /// </summary>
    public string RuntimeIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Process architecture.
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// Memory usage information.
    /// </summary>
    public MemoryUsage MemoryUsage { get; set; } = new();
}

/// <summary>
/// Memory usage information.
/// </summary>
public class MemoryUsage
{
    /// <summary>
    /// Working set in bytes.
    /// </summary>
    public long WorkingSet { get; set; }

    /// <summary>
    /// Private memory in bytes.
    /// </summary>
    public long PrivateMemory { get; set; }

    /// <summary>
    /// Managed memory (GC heap) in bytes.
    /// </summary>
    public long ManagedMemory { get; set; }

    /// <summary>
    /// GC total allocated bytes.
    /// </summary>
    public long TotalAllocatedBytes { get; set; }
}

/// <summary>
/// Operating system information.
/// </summary>
public class OsInfo
{
    /// <summary>
    /// OS description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// OS platform.
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// OS version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// OS architecture.
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// Total physical memory in bytes.
    /// </summary>
    public long TotalMemory { get; set; }

    /// <summary>
    /// Available physical memory in bytes.
    /// </summary>
    public long AvailableMemory { get; set; }

    /// <summary>
    /// Number of processor cores.
    /// </summary>
    public int ProcessorCount { get; set; }

    /// <summary>
    /// Whether running in a container.
    /// </summary>
    public bool IsContainerized { get; set; }

    /// <summary>
    /// Whether running in WSL.
    /// </summary>
    public bool IsWsl { get; set; }

    /// <summary>
    /// Machine name.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;
}

/// <summary>
/// Node-RED runtime information.
/// </summary>
public class RuntimeInfo
{
    /// <summary>
    /// Runtime version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Whether the runtime is started.
    /// </summary>
    public bool IsStarted { get; set; }

    /// <summary>
    /// Flow state information.
    /// </summary>
    public FlowsInfo Flows { get; set; } = new();

    /// <summary>
    /// Loaded modules/nodes.
    /// </summary>
    public Dictionary<string, string> Modules { get; set; } = new();

    /// <summary>
    /// Runtime settings.
    /// </summary>
    public RuntimeSettings Settings { get; set; } = new();

    /// <summary>
    /// Runtime metrics.
    /// </summary>
    public RuntimeMetrics Metrics { get; set; } = new();
}

/// <summary>
/// Flow state information.
/// </summary>
public class FlowsInfo
{
    /// <summary>
    /// Current flow state.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Whether flows are started.
    /// </summary>
    public bool Started { get; set; }

    /// <summary>
    /// Number of active flows.
    /// </summary>
    public int ActiveFlows { get; set; }

    /// <summary>
    /// Total number of nodes.
    /// </summary>
    public int TotalNodes { get; set; }
}

/// <summary>
/// Runtime settings information.
/// </summary>
public class RuntimeSettings
{
    /// <summary>
    /// Whether settings are available.
    /// </summary>
    public bool Available { get; set; }

    /// <summary>
    /// Whether editor is disabled.
    /// </summary>
    public bool DisableEditor { get; set; }

    /// <summary>
    /// Flow file name.
    /// </summary>
    public string FlowFile { get; set; } = string.Empty;

    /// <summary>
    /// Whether admin auth is enabled.
    /// </summary>
    public string AdminAuth { get; set; } = "UNSET";

    /// <summary>
    /// HTTP admin root path.
    /// </summary>
    public string HttpAdminRoot { get; set; } = "/";

    /// <summary>
    /// HTTP node root path.
    /// </summary>
    public string HttpNodeRoot { get; set; } = "/";

    /// <summary>
    /// Debug max length.
    /// </summary>
    public int DebugMaxLength { get; set; } = 1000;

    /// <summary>
    /// Context storage modules.
    /// </summary>
    public Dictionary<string, ContextStorageInfo> ContextStorage { get; set; } = new();
}

/// <summary>
/// Context storage module information.
/// </summary>
public class ContextStorageInfo
{
    /// <summary>
    /// Module name.
    /// </summary>
    public string Module { get; set; } = string.Empty;
}

/// <summary>
/// Runtime performance metrics.
/// </summary>
public class RuntimeMetrics
{
    /// <summary>
    /// Total messages processed since startup.
    /// </summary>
    public long MessagesProcessed { get; set; }

    /// <summary>
    /// Messages processed per second (average).
    /// </summary>
    public double MessagesPerSecond { get; set; }

    /// <summary>
    /// Total errors since startup.
    /// </summary>
    public long ErrorCount { get; set; }

    /// <summary>
    /// Average message processing time in milliseconds.
    /// </summary>
    public double AverageProcessingTimeMs { get; set; }

    /// <summary>
    /// Number of active node instances.
    /// </summary>
    public int ActiveNodeInstances { get; set; }
}
