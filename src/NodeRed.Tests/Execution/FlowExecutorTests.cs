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
/// Tests for FlowExecutor - the core flow execution engine.
/// </summary>
public class FlowExecutorTests
{
    private readonly Mock<INodeRegistry> _nodeRegistryMock;
    private readonly Dictionary<string, object?> _globalContext;

    public FlowExecutorTests()
    {
        _nodeRegistryMock = new Mock<INodeRegistry>();
        _globalContext = new Dictionary<string, object?>();
    }

    #region Basic Flow Execution Tests

    [Fact]
    public async Task InitializeAsync_ShouldCreateNodes_ForAllEnabledNodes()
    {
        // Arrange
        var flow = CreateTestFlow(new FlowNode[]
        {
            CreateNode("n1", "inject"),
            CreateNode("n2", "debug")
        });

        var mockNode = new Mock<INode>();
        mockNode.Setup(n => n.InitializeAsync(It.IsAny<FlowNode>(), It.IsAny<INodeContext>()))
            .Returns(Task.CompletedTask);

        _nodeRegistryMock.Setup(r => r.CreateNode(It.IsAny<string>()))
            .Returns(mockNode.Object);

        var executor = CreateExecutor(flow);

        // Act
        await executor.InitializeAsync();

        // Assert
        _nodeRegistryMock.Verify(r => r.CreateNode("inject"), Times.Once);
        _nodeRegistryMock.Verify(r => r.CreateNode("debug"), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_ShouldSkipDisabledNodes()
    {
        // Arrange
        var flow = CreateTestFlow(new FlowNode[]
        {
            CreateNode("n1", "inject"),
            CreateNode("n2", "debug", disabled: true)
        });

        var mockNode = new Mock<INode>();
        mockNode.Setup(n => n.InitializeAsync(It.IsAny<FlowNode>(), It.IsAny<INodeContext>()))
            .Returns(Task.CompletedTask);

        _nodeRegistryMock.Setup(r => r.CreateNode("inject"))
            .Returns(mockNode.Object);

        var executor = CreateExecutor(flow);

        // Act
        await executor.InitializeAsync();

        // Assert
        _nodeRegistryMock.Verify(r => r.CreateNode("inject"), Times.Once);
        _nodeRegistryMock.Verify(r => r.CreateNode("debug"), Times.Never);
    }

    [Fact]
    public async Task StopAsync_ShouldCloseAllNodes()
    {
        // Arrange
        var flow = CreateTestFlow(new FlowNode[]
        {
            CreateNode("n1", "inject"),
            CreateNode("n2", "debug")
        });

        var mockNode1 = new Mock<INode>();
        var mockNode2 = new Mock<INode>();
        mockNode1.Setup(n => n.InitializeAsync(It.IsAny<FlowNode>(), It.IsAny<INodeContext>()))
            .Returns(Task.CompletedTask);
        mockNode2.Setup(n => n.InitializeAsync(It.IsAny<FlowNode>(), It.IsAny<INodeContext>()))
            .Returns(Task.CompletedTask);

        _nodeRegistryMock.SetupSequence(r => r.CreateNode(It.IsAny<string>()))
            .Returns(mockNode1.Object)
            .Returns(mockNode2.Object);

        var executor = CreateExecutor(flow);
        await executor.InitializeAsync();

        // Act
        await executor.StopAsync();

        // Assert
        mockNode1.Verify(n => n.CloseAsync(), Times.Once);
        mockNode2.Verify(n => n.CloseAsync(), Times.Once);
    }

    #endregion

    #region Message Routing Tests

    [Fact]
    public void RouteMessage_ShouldDeliverMessage_ToConnectedNodes()
    {
        // Arrange
        var receivedMessages = new List<NodeMessage>();
        var flow = CreateTestFlow(new FlowNode[]
        {
            CreateNode("n1", "inject", wires: new[] { new[] { "n2" } }),
            CreateNode("n2", "debug")
        });

        var mockSourceNode = new Mock<INode>();
        var mockTargetNode = new Mock<INode>();
        
        mockSourceNode.Setup(n => n.InitializeAsync(It.IsAny<FlowNode>(), It.IsAny<INodeContext>()))
            .Returns(Task.CompletedTask);
        mockTargetNode.Setup(n => n.InitializeAsync(It.IsAny<FlowNode>(), It.IsAny<INodeContext>()))
            .Returns(Task.CompletedTask);
        mockTargetNode.Setup(n => n.OnInputAsync(It.IsAny<NodeMessage>(), It.IsAny<int>()))
            .Callback<NodeMessage, int>((msg, _) => receivedMessages.Add(msg))
            .Returns(Task.CompletedTask);

        _nodeRegistryMock.SetupSequence(r => r.CreateNode(It.IsAny<string>()))
            .Returns(mockSourceNode.Object)
            .Returns(mockTargetNode.Object);

        var executor = CreateExecutor(flow);
        executor.InitializeAsync().Wait();

        // Act
        executor.RouteMessage("n1", 0, new NodeMessage { Payload = "test" });

        // Wait for async routing
        Thread.Sleep(100);

        // Assert
        receivedMessages.Should().ContainSingle();
        receivedMessages[0].Payload.Should().Be("test");
    }

    [Fact]
    public void RouteMessage_ShouldCloneMessage_ForEachTarget()
    {
        // Arrange
        var receivedMessages = new List<NodeMessage>();
        var flow = CreateTestFlow(new FlowNode[]
        {
            CreateNode("n1", "inject", wires: new[] { new[] { "n2", "n3" } }),
            CreateNode("n2", "debug"),
            CreateNode("n3", "debug")
        });

        var mockNodes = new List<Mock<INode>>();
        for (int i = 0; i < 3; i++)
        {
            var mock = new Mock<INode>();
            mock.Setup(n => n.InitializeAsync(It.IsAny<FlowNode>(), It.IsAny<INodeContext>()))
                .Returns(Task.CompletedTask);
            mock.Setup(n => n.OnInputAsync(It.IsAny<NodeMessage>(), It.IsAny<int>()))
                .Callback<NodeMessage, int>((msg, _) => receivedMessages.Add(msg))
                .Returns(Task.CompletedTask);
            mockNodes.Add(mock);
        }

        var sequence = _nodeRegistryMock.SetupSequence(r => r.CreateNode(It.IsAny<string>()));
        foreach (var mock in mockNodes)
        {
            sequence = sequence.Returns(mock.Object);
        }

        var executor = CreateExecutor(flow);
        executor.InitializeAsync().Wait();

        // Act
        executor.RouteMessage("n1", 0, new NodeMessage { Payload = "original" });

        // Wait for async routing
        Thread.Sleep(100);

        // Assert - should have 2 cloned messages (to n2 and n3)
        receivedMessages.Should().HaveCount(2);
        receivedMessages.All(m => m.Payload?.ToString() == "original").Should().BeTrue();
    }

    #endregion

    #region Catch Node Tests

    [Fact]
    public async Task HandleNodeError_ShouldTriggerCatchNodes()
    {
        // Arrange
        var errorReceived = false;
        var flow = CreateTestFlow(new FlowNode[]
        {
            CreateNode("n1", "function"),
            CreateNode("catch1", "catch", config: new Dictionary<string, object?> { { "scope", "all" } })
        });

        var mockFunctionNode = new Mock<INode>();
        var mockCatchNode = new Mock<INode>();
        
        mockFunctionNode.Setup(n => n.InitializeAsync(It.IsAny<FlowNode>(), It.IsAny<INodeContext>()))
            .Returns(Task.CompletedTask);
        
        mockCatchNode.Setup(n => n.InitializeAsync(It.IsAny<FlowNode>(), It.IsAny<INodeContext>()))
            .Returns(Task.CompletedTask);
        mockCatchNode.Setup(n => n.Config).Returns(flow.Nodes[1]);
        mockCatchNode.Setup(n => n.OnInputAsync(It.IsAny<NodeMessage>(), It.IsAny<int>()))
            .Callback<NodeMessage, int>((msg, _) => 
            {
                errorReceived = true;
                msg.Properties.Should().ContainKey("error");
            })
            .Returns(Task.CompletedTask);

        _nodeRegistryMock.SetupSequence(r => r.CreateNode(It.IsAny<string>()))
            .Returns(mockFunctionNode.Object)
            .Returns(mockCatchNode.Object);

        var executor = CreateExecutor(flow);
        await executor.InitializeAsync();

        // Act
        executor.HandleNodeError("n1", new Exception("Test error"));

        // Wait for async processing
        await Task.Delay(100);

        // Assert
        errorReceived.Should().BeTrue();
    }

    #endregion

    #region Status Node Tests

    [Fact]
    public async Task UpdateNodeStatus_ShouldTriggerStatusNodes()
    {
        // Arrange
        var statusReceived = false;
        var flow = CreateTestFlow(new FlowNode[]
        {
            CreateNode("n1", "inject"),
            CreateNode("status1", "status", config: new Dictionary<string, object?> { { "scope", "all" } })
        });

        var mockInjectNode = new Mock<INode>();
        var mockStatusNode = new Mock<INode>();
        
        mockInjectNode.Setup(n => n.InitializeAsync(It.IsAny<FlowNode>(), It.IsAny<INodeContext>()))
            .Returns(Task.CompletedTask);
        mockInjectNode.Setup(n => n.Config).Returns(flow.Nodes[0]);
        
        mockStatusNode.Setup(n => n.InitializeAsync(It.IsAny<FlowNode>(), It.IsAny<INodeContext>()))
            .Returns(Task.CompletedTask);
        mockStatusNode.Setup(n => n.Config).Returns(flow.Nodes[1]);
        mockStatusNode.Setup(n => n.Definition).Returns(new NodeDefinition { Type = "status", DisplayName = "status" });
        mockStatusNode.Setup(n => n.OnInputAsync(It.IsAny<NodeMessage>(), It.IsAny<int>()))
            .Callback<NodeMessage, int>((msg, _) => statusReceived = true)
            .Returns(Task.CompletedTask);

        _nodeRegistryMock.SetupSequence(r => r.CreateNode(It.IsAny<string>()))
            .Returns(mockInjectNode.Object)
            .Returns(mockStatusNode.Object);

        var executor = CreateExecutor(flow);
        await executor.InitializeAsync();

        // Act
        executor.UpdateNodeStatus("n1", new NodeStatus { Text = "running", Color = StatusColor.Green });

        // Wait for async processing
        await Task.Delay(100);

        // Assert
        statusReceived.Should().BeTrue();
    }

    [Fact]
    public void UpdateNodeStatus_ShouldRaiseOnNodeStatusChangedEvent()
    {
        // Arrange
        var flow = CreateTestFlow(new FlowNode[] { CreateNode("n1", "inject") });
        var executor = CreateExecutor(flow);
        
        NodeStatus? receivedStatus = null;
        string? receivedNodeId = null;
        executor.OnNodeStatusChanged += (nodeId, status) =>
        {
            receivedNodeId = nodeId;
            receivedStatus = status;
        };

        // Act
        executor.UpdateNodeStatus("n1", new NodeStatus { Text = "active", Color = StatusColor.Blue });

        // Assert
        receivedNodeId.Should().Be("n1");
        receivedStatus.Should().NotBeNull();
        receivedStatus!.Text.Should().Be("active");
        receivedStatus.Color.Should().Be(StatusColor.Blue);
    }

    #endregion

    #region Node Status Tests

    [Fact]
    public void GetNodeStatus_ShouldReturnStatus_WhenSet()
    {
        // Arrange
        var flow = CreateTestFlow(new FlowNode[] { CreateNode("n1", "inject") });
        var executor = CreateExecutor(flow);
        
        var expectedStatus = new NodeStatus { Text = "test", Color = StatusColor.Yellow };
        executor.UpdateNodeStatus("n1", expectedStatus);

        // Act
        var status = executor.GetNodeStatus("n1");

        // Assert
        status.Should().NotBeNull();
        status!.Text.Should().Be("test");
        status.Color.Should().Be(StatusColor.Yellow);
    }

    [Fact]
    public void GetNodeStatus_ShouldReturnNull_WhenNotSet()
    {
        // Arrange
        var flow = CreateTestFlow(new FlowNode[] { CreateNode("n1", "inject") });
        var executor = CreateExecutor(flow);

        // Act
        var status = executor.GetNodeStatus("unknown-node");

        // Assert
        status.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private FlowExecutor CreateExecutor(Flow flow, List<Subflow>? subflows = null)
    {
        return new FlowExecutor(flow, _nodeRegistryMock.Object, _globalContext, subflows);
    }

    private static Flow CreateTestFlow(FlowNode[] nodes)
    {
        return new Flow
        {
            Id = "test-flow",
            Label = "Test Flow",
            Nodes = nodes.ToList()
        };
    }

    private static FlowNode CreateNode(
        string id, 
        string type, 
        bool disabled = false,
        string[][]? wires = null,
        Dictionary<string, object?>? config = null)
    {
        return new FlowNode
        {
            Id = id,
            Type = type,
            Name = id,
            Disabled = disabled,
            FlowId = "test-flow",
            Wires = wires?.Select(w => w.ToList()).ToList() ?? new List<List<string>>(),
            Config = config ?? new Dictionary<string, object?>()
        };
    }

    #endregion
}
