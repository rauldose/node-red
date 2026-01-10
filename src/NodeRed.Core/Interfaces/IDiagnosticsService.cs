// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;

namespace NodeRed.Core.Interfaces;

/// <summary>
/// Interface for diagnostics service.
/// </summary>
public interface IDiagnosticsService
{
    /// <summary>
    /// Gets a diagnostics report.
    /// </summary>
    /// <param name="scope">Report scope ("user" or "admin").</param>
    /// <returns>Diagnostics report.</returns>
    Task<DiagnosticsReport> GetReportAsync(string scope = "user");

    /// <summary>
    /// Gets runtime metrics.
    /// </summary>
    /// <returns>Runtime metrics.</returns>
    Task<RuntimeMetrics> GetMetricsAsync();

    /// <summary>
    /// Registers a message processing event for metrics.
    /// </summary>
    /// <param name="processingTimeMs">Processing time in milliseconds.</param>
    void RecordMessageProcessed(double processingTimeMs);

    /// <summary>
    /// Registers an error event for metrics.
    /// </summary>
    void RecordError();

    /// <summary>
    /// Sets the current flow count and node count.
    /// </summary>
    /// <param name="flowCount">Number of active flows.</param>
    /// <param name="nodeCount">Number of active nodes.</param>
    void SetFlowStats(int flowCount, int nodeCount);
}
