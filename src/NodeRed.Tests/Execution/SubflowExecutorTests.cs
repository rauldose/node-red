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
/// Tests for SubflowExecutor - handles subflow runtime execution with node cloning.
/// </summary>
public class SubflowExecutorTests
{
    private readonly Mock<INodeRegistry> _nodeRegistryMock;
    private readonly Mock<FlowExecutor> _parentExecutorMock;
    private readonly Dictionary<string, object?> _globalContext;

    public SubflowExecutorTests()
    {
        _nodeRegistryMock = new Mock<INodeRegistry>();
        _globalContext = new Dictionary<string, object?>();
    }

    #region Subflow Definition Tests

    [Fact]
    public void Subflow_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var subflow = new Subflow();

        // Assert
        subflow.Id.Should().NotBeNullOrEmpty();
        subflow.Name.Should().Be("Subflow");
        subflow.Category.Should().Be("subflows");
        subflow.Inputs.Should().Be(1);
        subflow.Outputs.Should().Be(1);
        subflow.Nodes.Should().BeEmpty();
        subflow.In.Should().BeEmpty();
        subflow.Out.Should().BeEmpty();
        subflow.Env.Should().BeEmpty();
    }

    [Fact]
    public void SubflowPort_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var port = new SubflowPort();

        // Assert
        port.X.Should().Be(0);
        port.Y.Should().Be(0);
        port.Wires.Should().BeEmpty();
    }

    [Fact]
    public void SubflowEnv_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var env = new SubflowEnv();

        // Assert
        env.Name.Should().BeEmpty();
        env.Type.Should().Be("str");
        env.Value.Should().BeNull();
    }

    #endregion

    #region Subflow Instance Tests

    [Fact]
    public async Task InitializeAsync_ShouldCloneNodes_WithNewIds()
    {
        // Arrange
        var subflow = CreateTestSubflow();
        var instance = CreateSubflowInstance(subflow.Id);
        var parentExecutor = CreateMockParentExecutor();

        var createdNodeIds = new List<string>();
        var createdConfigs = new List<FlowNode>();
        
        _nodeRegistryMock.Setup(r => r.CreateNode(It.IsAny<string>()))
            .Returns((string type) => 
            {
                var mockNode = new Mock<INode>();
                FlowNode? capturedConfig = null;
                mockNode.Setup(n => n.InitializeAsync(It.IsAny<FlowNode>(), It.IsAny<INodeContext>()))
                    .Callback<FlowNode, INodeContext>((config, _) => 
                    {
                        capturedConfig = config;
                        createdNodeIds.Add(config.Id);
                        createdConfigs.Add(config);
                    })
                    .Returns(Task.CompletedTask);
                mockNode.Setup(n => n.Config).Returns(() => capturedConfig!);
                return mockNode.Object;
            });

        var executor = new SubflowExecutor(
            subflow, instance, parentExecutor, _nodeRegistryMock.Object, _globalContext);

        // Act
        await executor.InitializeAsync();

        // Assert
        createdNodeIds.Should().NotBeEmpty();
        createdNodeIds.All(id => id.StartsWith(instance.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_ShouldRemapWires()
    {
        // Arrange
        var subflow = CreateTestSubflow();
        // Add a wire between nodes
        subflow.Nodes[0].Wires = new List<List<string>> { new List<string> { subflow.Nodes[1].Id } };

        var instance = CreateSubflowInstance(subflow.Id);
        var parentExecutor = CreateMockParentExecutor();

        var createdConfigs = new List<FlowNode>();
        
        _nodeRegistryMock.Setup(r => r.CreateNode(It.IsAny<string>()))
            .Returns((string type) => 
            {
                var mockNode = new Mock<INode>();
                FlowNode? capturedConfig = null;
                mockNode.Setup(n => n.InitializeAsync(It.IsAny<FlowNode>(), It.IsAny<INodeContext>()))
                    .Callback<FlowNode, INodeContext>((config, _) => 
                    {
                        capturedConfig = config;
                        createdConfigs.Add(config);
                    })
                    .Returns(Task.CompletedTask);
                mockNode.Setup(n => n.Config).Returns(() => capturedConfig!);
                return mockNode.Object;
            });

        var executor = new SubflowExecutor(
            subflow, instance, parentExecutor, _nodeRegistryMock.Object, _globalContext);

        // Act
        await executor.InitializeAsync();

        // Assert
        createdConfigs.Should().HaveCount(2);
        // After RemapWires, wires should reference remapped IDs
        var firstNode = createdConfigs[0];
        if (firstNode.Wires.Count > 0 && firstNode.Wires[0].Count > 0)
        {
            // After remapping, the wire should point to the remapped ID
            firstNode.Wires[0][0].Should().StartWith(instance.Id);
        }
    }

    #endregion

    #region Environment Variable Tests

    [Fact]
    public void GetEnv_ShouldReturnTemplateEnvVar()
    {
        // Arrange
        var subflow = CreateTestSubflow();
        subflow.Env.Add(new SubflowEnv { Name = "TEST_VAR", Type = "str", Value = "test-value" });

        var instance = CreateSubflowInstance(subflow.Id);
        var parentExecutor = CreateMockParentExecutor();

        var executor = new SubflowExecutor(
            subflow, instance, parentExecutor, _nodeRegistryMock.Object, _globalContext);

        // Act
        var value = executor.GetEnv("TEST_VAR");

        // Assert
        value.Should().Be("test-value");
    }

    [Fact]
    public void GetEnv_ShouldReturnNull_ForUndefinedVar()
    {
        // Arrange
        var subflow = CreateTestSubflow();
        var instance = CreateSubflowInstance(subflow.Id);
        var parentExecutor = CreateMockParentExecutor();

        var executor = new SubflowExecutor(
            subflow, instance, parentExecutor, _nodeRegistryMock.Object, _globalContext);

        // Act
        var value = executor.GetEnv("UNDEFINED_VAR");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void GetEnv_ShouldHandleNumericType()
    {
        // Arrange
        var subflow = CreateTestSubflow();
        subflow.Env.Add(new SubflowEnv { Name = "NUM_VAR", Type = "num", Value = "42" });

        var instance = CreateSubflowInstance(subflow.Id);
        var parentExecutor = CreateMockParentExecutor();

        var executor = new SubflowExecutor(
            subflow, instance, parentExecutor, _nodeRegistryMock.Object, _globalContext);

        // Act
        var value = executor.GetEnv("NUM_VAR");

        // Assert
        value.Should().Be(42.0);
    }

    [Fact]
    public void GetEnv_ShouldHandleBooleanType()
    {
        // Arrange
        var subflow = CreateTestSubflow();
        subflow.Env.Add(new SubflowEnv { Name = "BOOL_VAR", Type = "bool", Value = "true" });

        var instance = CreateSubflowInstance(subflow.Id);
        var parentExecutor = CreateMockParentExecutor();

        var executor = new SubflowExecutor(
            subflow, instance, parentExecutor, _nodeRegistryMock.Object, _globalContext);

        // Act
        var value = executor.GetEnv("BOOL_VAR");

        // Assert
        value.Should().Be(true);
    }

    #endregion

    #region Message Routing Tests

    [Fact]
    public async Task OnInputAsync_ShouldRouteToInternalNodes()
    {
        // Arrange
        var subflow = CreateTestSubflow();
        subflow.In.Add(new SubflowPort
        {
            Wires = new List<SubflowWire>
            {
                new SubflowWire { Id = subflow.Nodes[0].Id, Port = 0 }
            }
        });

        var instance = CreateSubflowInstance(subflow.Id);
        var parentExecutor = CreateMockParentExecutor();

        var receivedMessage = false;
        
        _nodeRegistryMock.Setup(r => r.CreateNode(It.IsAny<string>()))
            .Returns((string type) => 
            {
                var mockNode = new Mock<INode>();
                FlowNode? capturedConfig = null;
                mockNode.Setup(n => n.InitializeAsync(It.IsAny<FlowNode>(), It.IsAny<INodeContext>()))
                    .Callback<FlowNode, INodeContext>((config, _) => capturedConfig = config)
                    .Returns(Task.CompletedTask);
                mockNode.Setup(n => n.Config).Returns(() => capturedConfig!);
                mockNode.Setup(n => n.OnInputAsync(It.IsAny<NodeMessage>(), It.IsAny<int>()))
                    .Callback<NodeMessage, int>((_, _) => receivedMessage = true)
                    .Returns(Task.CompletedTask);
                return mockNode.Object;
            });

        var executor = new SubflowExecutor(
            subflow, instance, parentExecutor, _nodeRegistryMock.Object, _globalContext);
        await executor.InitializeAsync();

        // Act
        await executor.OnInputAsync(new NodeMessage { Payload = "test" });

        // Assert
        receivedMessage.Should().BeTrue();
    }

    #endregion

    #region Stop Tests

    [Fact]
    public async Task StopAsync_ShouldCloseAllNodes()
    {
        // Arrange
        var subflow = CreateTestSubflow();
        var instance = CreateSubflowInstance(subflow.Id);
        var parentExecutor = CreateMockParentExecutor();

        var closedCount = 0;
        
        _nodeRegistryMock.Setup(r => r.CreateNode(It.IsAny<string>()))
            .Returns((string type) => 
            {
                var mockNode = new Mock<INode>();
                FlowNode? capturedConfig = null;
                mockNode.Setup(n => n.InitializeAsync(It.IsAny<FlowNode>(), It.IsAny<INodeContext>()))
                    .Callback<FlowNode, INodeContext>((config, _) => capturedConfig = config)
                    .Returns(Task.CompletedTask);
                mockNode.Setup(n => n.Config).Returns(() => capturedConfig!);
                mockNode.Setup(n => n.CloseAsync())
                    .Callback(() => closedCount++)
                    .Returns(Task.CompletedTask);
                return mockNode.Object;
            });

        var executor = new SubflowExecutor(
            subflow, instance, parentExecutor, _nodeRegistryMock.Object, _globalContext);
        await executor.InitializeAsync();

        // Act
        await executor.StopAsync();

        // Assert
        closedCount.Should().Be(2); // 2 nodes in test subflow
    }

    #endregion

    #region Helper Methods

    private static Subflow CreateTestSubflow()
    {
        return new Subflow
        {
            Id = "subflow-1",
            Name = "Test Subflow",
            Inputs = 1,
            Outputs = 1,
            Nodes = new List<FlowNode>
            {
                new FlowNode
                {
                    Id = "sf-n1",
                    Type = "function",
                    Name = "Process",
                    Wires = new List<List<string>>()
                },
                new FlowNode
                {
                    Id = "sf-n2",
                    Type = "function",
                    Name = "Output",
                    Wires = new List<List<string>>()
                }
            }
        };
    }

    private static FlowNode CreateSubflowInstance(string subflowId)
    {
        return new FlowNode
        {
            Id = "instance-1",
            Type = $"subflow:{subflowId}",
            Name = "Subflow Instance",
            FlowId = "parent-flow",
            Wires = new List<List<string>>()
        };
    }

    private FlowExecutor CreateMockParentExecutor()
    {
        var flow = new Flow
        {
            Id = "parent-flow",
            Label = "Parent Flow",
            Nodes = new List<FlowNode>()
        };
        return new FlowExecutor(flow, _nodeRegistryMock.Object, _globalContext);
    }

    #endregion
}
