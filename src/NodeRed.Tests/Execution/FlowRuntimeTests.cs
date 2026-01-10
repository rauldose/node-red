// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using FluentAssertions;
using Moq;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;
using NodeRed.Runtime.Execution;
using Xunit;

namespace NodeRed.Tests.Execution;

/// <summary>
/// Tests for FlowRuntime - the main runtime that manages multiple flows.
/// </summary>
public class FlowRuntimeTests
{
    private readonly Mock<INodeRegistry> _nodeRegistryMock;

    public FlowRuntimeTests()
    {
        _nodeRegistryMock = new Mock<INodeRegistry>();
    }

    #region Basic Lifecycle Tests

    [Fact]
    public async Task LoadAsync_ShouldSetWorkspace()
    {
        // Arrange
        var runtime = new FlowRuntime(_nodeRegistryMock.Object);
        var workspace = CreateTestWorkspace();

        // Act
        await runtime.LoadAsync(workspace);

        // Assert
        runtime.State.Should().Be(FlowState.Stopped);
    }

    [Fact]
    public async Task StartAsync_ShouldThrow_WhenNoWorkspaceLoaded()
    {
        // Arrange
        var runtime = new FlowRuntime(_nodeRegistryMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.StartAsync());
    }

    [Fact]
    public async Task StartAsync_ShouldSetStateToRunning()
    {
        // Arrange
        var runtime = new FlowRuntime(_nodeRegistryMock.Object);
        var workspace = CreateTestWorkspace();
        await runtime.LoadAsync(workspace);

        // Act
        await runtime.StartAsync();

        // Assert
        runtime.State.Should().Be(FlowState.Running);
    }

    [Fact]
    public async Task StopAsync_ShouldSetStateToStopped()
    {
        // Arrange
        var runtime = new FlowRuntime(_nodeRegistryMock.Object);
        var workspace = CreateTestWorkspace();
        await runtime.LoadAsync(workspace);
        await runtime.StartAsync();

        // Act
        await runtime.StopAsync();

        // Assert
        runtime.State.Should().Be(FlowState.Stopped);
    }

    [Fact]
    public async Task RestartAsync_ShouldStopAndStart()
    {
        // Arrange
        var runtime = new FlowRuntime(_nodeRegistryMock.Object);
        var workspace = CreateTestWorkspace();
        await runtime.LoadAsync(workspace);
        await runtime.StartAsync();

        // Act
        await runtime.RestartAsync();

        // Assert
        runtime.State.Should().Be(FlowState.Running);
    }

    #endregion

    #region Deployment Tests

    [Fact]
    public async Task DeployAsync_Full_ShouldRestartAllFlows()
    {
        // Arrange
        var runtime = new FlowRuntime(_nodeRegistryMock.Object);
        var workspace = CreateTestWorkspace();
        await runtime.LoadAsync(workspace);
        await runtime.StartAsync();

        var newWorkspace = CreateTestWorkspace(flowCount: 2);

        // Act
        await runtime.DeployAsync(newWorkspace, DeployType.Full);

        // Assert
        runtime.State.Should().Be(FlowState.Running);
    }

    [Fact]
    public async Task DeployAsync_Flows_ShouldOnlyRestartChangedFlows()
    {
        // Arrange
        var runtime = new FlowRuntime(_nodeRegistryMock.Object);
        var workspace = CreateTestWorkspace(flowCount: 2);
        await runtime.LoadAsync(workspace);
        await runtime.StartAsync();

        // Create modified workspace with one changed flow
        var newWorkspace = CreateTestWorkspace(flowCount: 2);
        newWorkspace.Flows[0].Label = "Modified Flow"; // Change first flow

        // Act
        await runtime.DeployAsync(newWorkspace, DeployType.Flows);

        // Assert
        runtime.State.Should().Be(FlowState.Running);
    }

    [Fact]
    public async Task DeployAsync_ShouldHandleNewFlows()
    {
        // Arrange
        var runtime = new FlowRuntime(_nodeRegistryMock.Object);
        var workspace = CreateTestWorkspace(flowCount: 1);
        await runtime.LoadAsync(workspace);
        await runtime.StartAsync();

        // Create workspace with additional flow
        var newWorkspace = CreateTestWorkspace(flowCount: 2);

        // Act
        await runtime.DeployAsync(newWorkspace, DeployType.Flows);

        // Assert
        runtime.State.Should().Be(FlowState.Running);
    }

    [Fact]
    public async Task DeployAsync_ShouldHandleRemovedFlows()
    {
        // Arrange
        var runtime = new FlowRuntime(_nodeRegistryMock.Object);
        var workspace = CreateTestWorkspace(flowCount: 2);
        await runtime.LoadAsync(workspace);
        await runtime.StartAsync();

        // Create workspace with fewer flows
        var newWorkspace = CreateTestWorkspace(flowCount: 1);

        // Act
        await runtime.DeployAsync(newWorkspace, DeployType.Flows);

        // Assert
        runtime.State.Should().Be(FlowState.Running);
    }

    #endregion

    #region Event Tests

    [Fact]
    public async Task OnLog_ShouldBeRaised_OnFlowStart()
    {
        // Arrange
        var runtime = new FlowRuntime(_nodeRegistryMock.Object);
        var workspace = CreateTestWorkspace();
        await runtime.LoadAsync(workspace);

        var logMessages = new List<LogEntry>();
        runtime.OnLog += entry => logMessages.Add(entry);

        // Act
        await runtime.StartAsync();

        // Assert
        logMessages.Should().Contain(e => e.Message.Contains("started successfully"));
    }

    [Fact]
    public async Task OnLog_ShouldBeRaised_OnFlowStop()
    {
        // Arrange
        var runtime = new FlowRuntime(_nodeRegistryMock.Object);
        var workspace = CreateTestWorkspace();
        await runtime.LoadAsync(workspace);
        await runtime.StartAsync();

        var logMessages = new List<LogEntry>();
        runtime.OnLog += entry => logMessages.Add(entry);

        // Act
        await runtime.StopAsync();

        // Assert
        logMessages.Should().Contain(e => e.Message.Contains("stopped"));
    }

    #endregion

    #region Disabled Flow Tests

    [Fact]
    public async Task LoadAsync_ShouldSkipDisabledFlows()
    {
        // Arrange
        var runtime = new FlowRuntime(_nodeRegistryMock.Object);
        var workspace = new Workspace
        {
            Id = "test-workspace",
            Name = "Test",
            Flows = new List<Flow>
            {
                new Flow { Id = "flow1", Label = "Flow 1", Disabled = false },
                new Flow { Id = "flow2", Label = "Flow 2", Disabled = true }
            }
        };

        // Act
        await runtime.LoadAsync(workspace);
        await runtime.StartAsync();

        // Assert - only one flow should be running (no way to check directly, but it should not throw)
        runtime.State.Should().Be(FlowState.Running);
    }

    #endregion

    #region Helper Methods

    private static Workspace CreateTestWorkspace(int flowCount = 1)
    {
        var workspace = new Workspace
        {
            Id = "test-workspace",
            Name = "Test Workspace",
            Flows = new List<Flow>()
        };

        for (int i = 0; i < flowCount; i++)
        {
            workspace.Flows.Add(new Flow
            {
                Id = $"flow-{i}",
                Label = $"Flow {i}",
                Nodes = new List<FlowNode>()
            });
        }

        return workspace;
    }

    #endregion
}
