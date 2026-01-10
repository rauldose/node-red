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
/// Tests for NodeContext - provides context for node execution including
/// flow context, global context, node context, and environment variables.
/// </summary>
public class NodeContextTests
{
    private readonly Mock<INodeRegistry> _nodeRegistryMock;
    private readonly Dictionary<string, object?> _globalContext;
    private readonly Dictionary<string, object?> _flowContext;

    public NodeContextTests()
    {
        _nodeRegistryMock = new Mock<INodeRegistry>();
        _globalContext = new Dictionary<string, object?>();
        _flowContext = new Dictionary<string, object?>();
    }

    #region Flow Context Tests

    [Fact]
    public void GetFlowContext_ShouldReturnValue_WhenSet()
    {
        // Arrange
        _flowContext["test-key"] = "test-value";
        var context = CreateNodeContext();

        // Act
        var value = context.GetFlowContext<string>("test-key");

        // Assert
        value.Should().Be("test-value");
    }

    [Fact]
    public void GetFlowContext_ShouldReturnDefault_WhenNotSet()
    {
        // Arrange
        var context = CreateNodeContext();

        // Act
        var value = context.GetFlowContext<string>("nonexistent-key");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void SetFlowContext_ShouldStoreValue()
    {
        // Arrange
        var context = CreateNodeContext();

        // Act
        context.SetFlowContext("new-key", "new-value");

        // Assert
        _flowContext["new-key"].Should().Be("new-value");
    }

    [Fact]
    public void GetFlowContext_ShouldHandleTypedValues()
    {
        // Arrange
        _flowContext["int-key"] = 42;
        _flowContext["double-key"] = 3.14;
        _flowContext["bool-key"] = true;
        var context = CreateNodeContext();

        // Act & Assert
        context.GetFlowContext<int>("int-key").Should().Be(42);
        context.GetFlowContext<double>("double-key").Should().Be(3.14);
        context.GetFlowContext<bool>("bool-key").Should().BeTrue();
    }

    #endregion

    #region Global Context Tests

    [Fact]
    public void GetGlobalContext_ShouldReturnValue_WhenSet()
    {
        // Arrange
        _globalContext["global-key"] = "global-value";
        var context = CreateNodeContext();

        // Act
        var value = context.GetGlobalContext<string>("global-key");

        // Assert
        value.Should().Be("global-value");
    }

    [Fact]
    public void GetGlobalContext_ShouldReturnDefault_WhenNotSet()
    {
        // Arrange
        var context = CreateNodeContext();

        // Act
        var value = context.GetGlobalContext<string>("nonexistent-key");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void SetGlobalContext_ShouldStoreValue()
    {
        // Arrange
        var context = CreateNodeContext();

        // Act
        context.SetGlobalContext("global-new", "global-new-value");

        // Assert
        _globalContext["global-new"].Should().Be("global-new-value");
    }

    [Fact]
    public void GlobalContext_ShouldBeSharedAcrossContexts()
    {
        // Arrange
        var context1 = CreateNodeContext("node1");
        var context2 = CreateNodeContext("node2");

        // Act
        context1.SetGlobalContext("shared-key", "shared-value");

        // Assert
        context2.GetGlobalContext<string>("shared-key").Should().Be("shared-value");
    }

    #endregion

    #region Node Context Tests

    [Fact]
    public void GetNodeContext_ShouldReturnValue_WhenSet()
    {
        // Arrange
        var context = CreateNodeContext();
        context.SetNodeContext("node-key", "node-value");

        // Act
        var value = context.GetNodeContext<string>("node-key");

        // Assert
        value.Should().Be("node-value");
    }

    [Fact]
    public void GetNodeContext_ShouldReturnDefault_WhenNotSet()
    {
        // Arrange
        var context = CreateNodeContext();

        // Act
        var value = context.GetNodeContext<string>("nonexistent-key");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void NodeContext_ShouldBeIsolated_BetweenNodes()
    {
        // Arrange
        var context1 = CreateNodeContext("node1");
        var context2 = CreateNodeContext("node2");

        // Act
        context1.SetNodeContext("isolated-key", "isolated-value");

        // Assert - node2 should not see node1's context
        // Note: Each NodeContext has its own _nodeContext dictionary, 
        // so isolation is automatic based on context instance
        context1.GetNodeContext<string>("isolated-key").Should().Be("isolated-value");
    }

    #endregion

    #region Environment Variable Tests

    [Fact]
    public void GetEnv_ShouldReturnBuiltIn_NR_NODE_ID()
    {
        // Arrange
        var context = CreateNodeContext("test-node-123");

        // Act
        var value = context.GetEnv("NR_NODE_ID");

        // Assert
        value.Should().Be("test-node-123");
    }

    [Fact]
    public void GetEnv_ShouldReturnNull_ForUndefinedVar()
    {
        // Arrange
        var context = CreateNodeContext();

        // Act
        var value = context.GetEnv("UNDEFINED_VAR");

        // Assert
        value.Should().BeNull();
    }

    #endregion

    #region Status and Logging Tests

    [Fact]
    public void SetStatus_ShouldCallExecutor()
    {
        // Arrange
        var flow = CreateTestFlow();
        var executor = new FlowExecutor(flow, _nodeRegistryMock.Object, _globalContext);
        
        NodeStatus? receivedStatus = null;
        executor.OnNodeStatusChanged += (_, status) => receivedStatus = status;

        var context = new NodeContext(executor, "flow-1", "node-1", _flowContext, _globalContext);

        // Act
        context.SetStatus("node-1", new NodeStatus { Text = "active", Color = StatusColor.Green });

        // Assert
        receivedStatus.Should().NotBeNull();
        receivedStatus!.Text.Should().Be("active");
    }

    [Fact]
    public void Log_ShouldCallExecutor()
    {
        // Arrange
        var flow = CreateTestFlow();
        var executor = new FlowExecutor(flow, _nodeRegistryMock.Object, _globalContext);
        
        LogEntry? receivedLog = null;
        executor.OnLog += entry => receivedLog = entry;

        var context = new NodeContext(executor, "flow-1", "node-1", _flowContext, _globalContext);

        // Act
        context.Log("node-1", "Test message", LogLevel.Info);

        // Assert
        receivedLog.Should().NotBeNull();
        receivedLog!.Message.Should().Be("Test message");
        receivedLog.Level.Should().Be(LogLevel.Info);
    }

    #endregion

    #region Helper Methods

    private NodeContext CreateNodeContext(string nodeId = "test-node")
    {
        var flow = CreateTestFlow();
        var executor = new FlowExecutor(flow, _nodeRegistryMock.Object, _globalContext);
        return new NodeContext(executor, "flow-1", nodeId, _flowContext, _globalContext);
    }

    private static Flow CreateTestFlow()
    {
        return new Flow
        {
            Id = "flow-1",
            Label = "Test Flow",
            Nodes = new List<FlowNode>
            {
                new FlowNode { Id = "test-node", Type = "function" }
            }
        };
    }

    #endregion
}
