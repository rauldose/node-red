// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Runtime.Services;
using Xunit;

namespace NodeRed.Tests.Services;

/// <summary>
/// Unit tests for DiagnosticsService.
/// </summary>
public class DiagnosticsServiceTests
{
    [Fact]
    public async Task GetReport_ReturnsComprehensiveReport()
    {
        var service = new DiagnosticsService();

        var report = await service.GetReportAsync();

        Assert.NotNull(report);
        Assert.Equal("diagnostics", report.Report);
        Assert.Equal("user", report.Scope);
    }

    [Fact]
    public async Task GetReport_ContainsTimeInfo()
    {
        var service = new DiagnosticsService();

        var report = await service.GetReportAsync();

        Assert.NotNull(report.Time);
        Assert.NotEmpty(report.Time.Utc);
        Assert.NotEmpty(report.Time.Local);
        Assert.True(report.Time.UptimeSeconds >= 0);
    }

    [Fact]
    public async Task GetReport_ContainsDotNetInfo()
    {
        var service = new DiagnosticsService();

        var report = await service.GetReportAsync();

        Assert.NotNull(report.DotNet);
        Assert.NotEmpty(report.DotNet.Version);
        Assert.NotEmpty(report.DotNet.FrameworkDescription);
        Assert.NotEmpty(report.DotNet.Architecture);
    }

    [Fact]
    public async Task GetReport_ContainsOsInfo()
    {
        var service = new DiagnosticsService();

        var report = await service.GetReportAsync();

        Assert.NotNull(report.Os);
        Assert.NotEmpty(report.Os.Description);
        Assert.NotEmpty(report.Os.Platform);
        Assert.True(report.Os.ProcessorCount > 0);
    }

    [Fact]
    public async Task GetReport_ContainsMemoryUsage()
    {
        var service = new DiagnosticsService();

        var report = await service.GetReportAsync();

        Assert.NotNull(report.DotNet.MemoryUsage);
        Assert.True(report.DotNet.MemoryUsage.WorkingSet > 0);
        Assert.True(report.DotNet.MemoryUsage.ManagedMemory > 0);
    }

    [Fact]
    public async Task GetReport_ContainsRuntimeInfo()
    {
        var service = new DiagnosticsService();

        var report = await service.GetReportAsync();

        Assert.NotNull(report.Runtime);
        Assert.NotEmpty(report.Runtime.Version);
        Assert.NotNull(report.Runtime.Settings);
        Assert.NotNull(report.Runtime.Metrics);
    }

    [Fact]
    public async Task GetReport_WithAdminScope_ReturnsAdminScope()
    {
        var service = new DiagnosticsService();

        var report = await service.GetReportAsync("admin");

        Assert.Equal("admin", report.Scope);
    }

    [Fact]
    public void RecordMessageProcessed_UpdatesMetrics()
    {
        var service = new DiagnosticsService();

        service.RecordMessageProcessed(10.5);
        service.RecordMessageProcessed(20.0);
        service.RecordMessageProcessed(15.5);

        var metrics = service.GetMetricsAsync().Result;

        Assert.Equal(3, metrics.MessagesProcessed);
        Assert.True(metrics.AverageProcessingTimeMs > 0);
    }

    [Fact]
    public void RecordError_IncrementsErrorCount()
    {
        var service = new DiagnosticsService();

        service.RecordError();
        service.RecordError();

        var metrics = service.GetMetricsAsync().Result;

        Assert.Equal(2, metrics.ErrorCount);
    }

    [Fact]
    public void SetFlowStats_UpdatesFlowInfo()
    {
        var service = new DiagnosticsService();

        service.SetFlowStats(5, 25);

        var report = service.GetReportAsync().Result;

        Assert.Equal(5, report.Runtime.Flows.ActiveFlows);
        Assert.Equal(25, report.Runtime.Flows.TotalNodes);
    }
}
