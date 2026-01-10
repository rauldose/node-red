// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Services;

/// <summary>
/// Diagnostics service for system health reporting.
/// </summary>
public class DiagnosticsService : IDiagnosticsService
{
    private readonly IFlowRuntime? _flowRuntime;
    private readonly DateTimeOffset _startTime;
    private long _messagesProcessed;
    private long _errorCount;
    private double _totalProcessingTime;
    private int _activeFlows;
    private int _activeNodes;
    private readonly object _metricsLock = new();

    /// <summary>
    /// Creates a new diagnostics service.
    /// </summary>
    /// <param name="flowRuntime">Optional flow runtime for flow stats.</param>
    public DiagnosticsService(IFlowRuntime? flowRuntime = null)
    {
        _flowRuntime = flowRuntime;
        _startTime = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc />
    public Task<DiagnosticsReport> GetReportAsync(string scope = "user")
    {
        var now = DateTimeOffset.UtcNow;
        var process = Process.GetCurrentProcess();

        var report = new DiagnosticsReport
        {
            Scope = scope,
            Time = new TimeInfo
            {
                Utc = now.UtcDateTime.ToString("R"),
                Local = now.LocalDateTime.ToString("F"),
                UptimeSeconds = (now - _startTime).TotalSeconds
            },
            Intl = new IntlInfo
            {
                Locale = System.Globalization.CultureInfo.CurrentCulture.Name,
                TimeZone = TimeZoneInfo.Local.Id
            },
            DotNet = new DotNetInfo
            {
                Version = Environment.Version.ToString(),
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                MemoryUsage = new MemoryUsage
                {
                    WorkingSet = process.WorkingSet64,
                    PrivateMemory = process.PrivateMemorySize64,
                    ManagedMemory = GC.GetTotalMemory(false),
                    TotalAllocatedBytes = GC.GetTotalAllocatedBytes(false)
                }
            },
            Os = new OsInfo
            {
                Description = RuntimeInformation.OSDescription,
                Platform = GetPlatformName(),
                Version = Environment.OSVersion.VersionString,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                TotalMemory = GetTotalMemory(),
                AvailableMemory = GetAvailableMemory(),
                ProcessorCount = Environment.ProcessorCount,
                IsContainerized = IsRunningInContainer(),
                IsWsl = IsRunningInWsl(),
                MachineName = Environment.MachineName
            },
            Runtime = new RuntimeInfo
            {
                Version = GetRuntimeVersion(),
                IsStarted = IsFlowRuntimeStarted(),
                Flows = new FlowsInfo
                {
                    State = GetFlowState(),
                    Started = IsFlowRuntimeStarted(),
                    ActiveFlows = _activeFlows,
                    TotalNodes = _activeNodes
                },
                Modules = GetLoadedModules(),
                Settings = new RuntimeSettings
                {
                    Available = true,
                    DisableEditor = false,
                    FlowFile = "flows.json",
                    AdminAuth = "UNSET",
                    HttpAdminRoot = "/",
                    HttpNodeRoot = "/",
                    DebugMaxLength = 1000,
                    ContextStorage = new Dictionary<string, ContextStorageInfo>
                    {
                        ["memory"] = new ContextStorageInfo { Module = "memory" }
                    }
                },
                Metrics = GetMetricsInternal()
            }
        };

        return Task.FromResult(report);
    }

    /// <inheritdoc />
    public Task<RuntimeMetrics> GetMetricsAsync()
    {
        return Task.FromResult(GetMetricsInternal());
    }

    /// <inheritdoc />
    public void RecordMessageProcessed(double processingTimeMs)
    {
        lock (_metricsLock)
        {
            _messagesProcessed++;
            _totalProcessingTime += processingTimeMs;
        }
    }

    /// <inheritdoc />
    public void RecordError()
    {
        Interlocked.Increment(ref _errorCount);
    }

    /// <inheritdoc />
    public void SetFlowStats(int flowCount, int nodeCount)
    {
        _activeFlows = flowCount;
        _activeNodes = nodeCount;
    }

    private RuntimeMetrics GetMetricsInternal()
    {
        lock (_metricsLock)
        {
            var uptimeSeconds = (DateTimeOffset.UtcNow - _startTime).TotalSeconds;
            return new RuntimeMetrics
            {
                MessagesProcessed = _messagesProcessed,
                MessagesPerSecond = uptimeSeconds > 0 ? _messagesProcessed / uptimeSeconds : 0,
                ErrorCount = _errorCount,
                AverageProcessingTimeMs = _messagesProcessed > 0 ? _totalProcessingTime / _messagesProcessed : 0,
                ActiveNodeInstances = _activeNodes
            };
        }
    }

    private static string GetPlatformName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "darwin";
        return "unknown";
    }

    private static string GetRuntimeVersion()
    {
        // Return the NodeRed.Runtime version
        var assembly = typeof(DiagnosticsService).Assembly;
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "1.0.0";
    }

    private bool IsFlowRuntimeStarted()
    {
        if (_flowRuntime == null)
            return false;
        return _flowRuntime.State == FlowState.Running;
    }

    private string GetFlowState()
    {
        if (_flowRuntime == null)
            return "unknown";
        return _flowRuntime.State == FlowState.Running ? "started" : "stopped";
    }

    private static Dictionary<string, string> GetLoadedModules()
    {
        // Return built-in node types
        return new Dictionary<string, string>
        {
            ["node-red"] = "1.0.0",
            ["@node-red/nodes"] = "1.0.0"
        };
    }

    private static bool IsRunningInContainer()
    {
        // Check for Docker environment
        if (File.Exists("/.dockerenv"))
            return true;

        try
        {
            if (File.Exists("/proc/self/cgroup"))
            {
                var content = File.ReadAllText("/proc/self/cgroup");
                if (content.Contains("docker") || content.Contains("kubepod") || content.Contains("lxc"))
                    return true;
            }
        }
        catch
        {
            // Ignore - not running on Linux
        }

        return false;
    }

    private static bool IsRunningInWsl()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return false;

        try
        {
            var release = File.Exists("/proc/version") ? File.ReadAllText("/proc/version") : "";
            if (release.Contains("microsoft", StringComparison.OrdinalIgnoreCase) ||
                release.Contains("WSL", StringComparison.OrdinalIgnoreCase))
            {
                return !IsRunningInContainer();
            }
        }
        catch
        {
            // Ignore
        }

        return false;
    }

    private static long GetTotalMemory()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (File.Exists("/proc/meminfo"))
                {
                    var lines = File.ReadAllLines("/proc/meminfo");
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("MemTotal:"))
                        {
                            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                                return kb * 1024;
                        }
                    }
                }
            }
            else
            {
                // For Windows and macOS, use GC info as approximation
                return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            }
        }
        catch
        {
            // Ignore
        }

        return 0;
    }

    private static long GetAvailableMemory()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (File.Exists("/proc/meminfo"))
                {
                    var lines = File.ReadAllLines("/proc/meminfo");
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("MemAvailable:"))
                        {
                            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                                return kb * 1024;
                        }
                    }
                }
            }
            else
            {
                var gcInfo = GC.GetGCMemoryInfo();
                return gcInfo.TotalAvailableMemoryBytes - GC.GetTotalMemory(false);
            }
        }
        catch
        {
            // Ignore
        }

        return 0;
    }
}
