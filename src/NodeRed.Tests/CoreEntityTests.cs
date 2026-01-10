// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using FluentAssertions;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using Xunit;

namespace NodeRed.Tests;

/// <summary>
/// Tests for Core entities - Workspace, Flow, FlowNode, Subflow, NodeMessage, etc.
/// </summary>
public class CoreEntityTests
{
    #region Workspace Tests

    [Fact]
    public void Workspace_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var workspace = new Workspace();

        // Assert
        workspace.Id.Should().NotBeNullOrEmpty();
        workspace.Name.Should().Be("Default Workspace");
        workspace.Flows.Should().BeEmpty();
        workspace.ConfigNodes.Should().BeEmpty();
        workspace.Subflows.Should().BeEmpty();
        workspace.State.Should().Be(FlowState.Stopped);
        workspace.GlobalContext.Should().BeEmpty();
        workspace.Version.Should().Be("1.0");
    }

    [Fact]
    public void Workspace_ShouldAllowAddingFlows()
    {
        // Arrange
        var workspace = new Workspace();
        var flow = new Flow { Id = "flow-1", Label = "Test Flow" };

        // Act
        workspace.Flows.Add(flow);

        // Assert
        workspace.Flows.Should().ContainSingle();
        workspace.Flows[0].Id.Should().Be("flow-1");
    }

    [Fact]
    public void Workspace_ShouldAllowAddingSubflows()
    {
        // Arrange
        var workspace = new Workspace();
        var subflow = new Subflow { Id = "subflow-1", Name = "Test Subflow" };

        // Act
        workspace.Subflows.Add(subflow);

        // Assert
        workspace.Subflows.Should().ContainSingle();
        workspace.Subflows[0].Id.Should().Be("subflow-1");
    }

    #endregion

    #region Flow Tests

    [Fact]
    public void Flow_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var flow = new Flow();

        // Assert
        flow.Id.Should().NotBeNullOrEmpty();
        flow.Label.Should().Be("Flow 1");
        flow.Type.Should().Be("tab");
        flow.Disabled.Should().BeFalse();
        flow.Info.Should().BeEmpty();
        flow.Nodes.Should().BeEmpty();
        flow.Env.Should().BeEmpty();
        flow.Order.Should().Be(0);
    }

    [Fact]
    public void Flow_ShouldSupportDisabling()
    {
        // Arrange
        var flow = new Flow { Disabled = true };

        // Assert
        flow.Disabled.Should().BeTrue();
    }

    [Fact]
    public void Flow_ShouldSupportEnvironmentVariables()
    {
        // Arrange
        var flow = new Flow();
        
        // Act
        flow.Env["MY_VAR"] = "my-value";

        // Assert
        flow.Env.Should().ContainKey("MY_VAR");
        flow.Env["MY_VAR"].Should().Be("my-value");
    }

    #endregion

    #region FlowNode Tests

    [Fact]
    public void FlowNode_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var node = new FlowNode { Type = "inject" };

        // Assert
        node.Id.Should().NotBeNullOrEmpty();
        node.Type.Should().Be("inject");
        node.Name.Should().BeEmpty();
        node.X.Should().Be(0);
        node.Y.Should().Be(0);
        node.Width.Should().Be(120);
        node.Height.Should().Be(30);
        node.Disabled.Should().BeFalse();
        node.Config.Should().BeEmpty();
        node.Wires.Should().BeEmpty();
    }

    [Fact]
    public void FlowNode_Clone_ShouldCreateNewId()
    {
        // Arrange
        var original = new FlowNode 
        { 
            Id = "original-id",
            Type = "inject",
            Name = "Original",
            X = 100,
            Y = 200
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.Id.Should().NotBe(original.Id);
        clone.Type.Should().Be(original.Type);
        clone.Name.Should().Be(original.Name);
        clone.X.Should().Be(original.X + 20); // Offset
        clone.Y.Should().Be(original.Y + 20);
    }

    [Fact]
    public void FlowNode_Clone_ShouldCopyConfig()
    {
        // Arrange
        var original = new FlowNode 
        { 
            Type = "inject",
            Config = new Dictionary<string, object?> 
            { 
                { "topic", "test" },
                { "payload", 123 }
            }
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.Config.Should().HaveCount(2);
        clone.Config["topic"].Should().Be("test");
        clone.Config["payload"].Should().Be(123);
    }

    [Fact]
    public void FlowNode_Clone_ShouldCopyWires()
    {
        // Arrange
        var original = new FlowNode 
        { 
            Type = "inject",
            Wires = new List<List<string>> 
            { 
                new List<string> { "node-1", "node-2" },
                new List<string> { "node-3" }
            }
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.Wires.Should().HaveCount(2);
        clone.Wires[0].Should().Contain("node-1");
        clone.Wires[0].Should().Contain("node-2");
        clone.Wires[1].Should().Contain("node-3");
    }

    [Fact]
    public void FlowNode_Clone_ShouldNotShareReferences()
    {
        // Arrange
        var original = new FlowNode 
        { 
            Type = "inject",
            Config = new Dictionary<string, object?> { { "key", "value" } },
            Wires = new List<List<string>> { new List<string> { "node-1" } }
        };

        // Act
        var clone = original.Clone();
        clone.Config["key"] = "modified";
        clone.Wires[0].Add("node-2");

        // Assert - original should not be modified
        original.Config["key"].Should().Be("value");
        original.Wires[0].Should().HaveCount(1);
    }

    #endregion

    #region NodeMessage Tests

    [Fact]
    public void NodeMessage_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var msg = new NodeMessage();

        // Assert
        msg.Id.Should().NotBeNullOrEmpty();
        msg.Payload.Should().BeNull();
        msg.Topic.Should().BeNull(); // Topic is nullable, not empty by default
        msg.Properties.Should().BeEmpty();
    }

    [Fact]
    public void NodeMessage_Clone_ShouldCreateNewId()
    {
        // Arrange
        var original = new NodeMessage
        {
            Id = "original-id",
            Payload = "test-payload",
            Topic = "test-topic"
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.Id.Should().NotBe(original.Id);
        clone.Payload.Should().Be(original.Payload);
        clone.Topic.Should().Be(original.Topic);
    }

    [Fact]
    public void NodeMessage_Clone_ShouldCopyProperties()
    {
        // Arrange
        var original = new NodeMessage
        {
            Payload = "test",
            Properties = new Dictionary<string, object?>
            {
                { "custom", "value" },
                { "number", 42 }
            }
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.Properties.Should().HaveCount(2);
        clone.Properties["custom"].Should().Be("value");
        clone.Properties["number"].Should().Be(42);
    }

    [Fact]
    public void NodeMessage_Clone_ShouldNotShareReferences()
    {
        // Arrange
        var original = new NodeMessage
        {
            Properties = new Dictionary<string, object?> { { "key", "value" } }
        };

        // Act
        var clone = original.Clone();
        clone.Properties["key"] = "modified";

        // Assert - original should not be modified
        original.Properties["key"].Should().Be("value");
    }

    #endregion

    #region Subflow Tests

    [Fact]
    public void Subflow_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var subflow = new Subflow();

        // Assert
        subflow.Id.Should().NotBeNullOrEmpty();
        subflow.Name.Should().Be("Subflow");
        subflow.Info.Should().BeEmpty();
        subflow.Category.Should().Be("subflows");
        subflow.Color.Should().Be("#DDAA99");
        subflow.Inputs.Should().Be(1);
        subflow.Outputs.Should().Be(1);
        subflow.Nodes.Should().BeEmpty();
        subflow.In.Should().BeEmpty();
        subflow.Out.Should().BeEmpty();
        subflow.Env.Should().BeEmpty();
        subflow.Status.Should().BeFalse();
    }

    [Fact]
    public void Subflow_ShouldSupportEnvironmentVariables()
    {
        // Arrange
        var subflow = new Subflow();
        
        // Act
        subflow.Env.Add(new SubflowEnv { Name = "VAR1", Type = "str", Value = "value1" });
        subflow.Env.Add(new SubflowEnv { Name = "VAR2", Type = "num", Value = "42" });

        // Assert
        subflow.Env.Should().HaveCount(2);
        subflow.Env[0].Name.Should().Be("VAR1");
        subflow.Env[1].Value.Should().Be("42");
    }

    [Fact]
    public void Subflow_ShouldSupportInputPorts()
    {
        // Arrange
        var subflow = new Subflow();
        
        // Act
        subflow.In.Add(new SubflowPort 
        { 
            X = 10, 
            Y = 20,
            Wires = new List<SubflowWire> 
            { 
                new SubflowWire { Id = "node-1", Port = 0 } 
            }
        });

        // Assert
        subflow.In.Should().ContainSingle();
        subflow.In[0].Wires.Should().ContainSingle();
        subflow.In[0].Wires[0].Id.Should().Be("node-1");
    }

    [Fact]
    public void Subflow_ShouldSupportOutputPorts()
    {
        // Arrange
        var subflow = new Subflow();
        
        // Act
        subflow.Out.Add(new SubflowPort 
        { 
            X = 100, 
            Y = 20,
            Wires = new List<SubflowWire> 
            { 
                new SubflowWire { Id = "node-2", Port = 0 } 
            }
        });

        // Assert
        subflow.Out.Should().ContainSingle();
        subflow.Out[0].X.Should().Be(100);
    }

    [Fact]
    public void Subflow_ShouldSupportStatusOutput()
    {
        // Arrange
        var subflow = new Subflow
        {
            Status = true,
            StatusPort = new SubflowPort { X = 50, Y = 100 }
        };

        // Assert
        subflow.Status.Should().BeTrue();
        subflow.StatusPort.Should().NotBeNull();
        subflow.StatusPort!.X.Should().Be(50);
    }

    #endregion

    #region NodeStatus Tests

    [Fact]
    public void NodeStatus_Error_ShouldSetRedColor()
    {
        // Act
        var status = NodeStatus.Error("Something went wrong");

        // Assert
        status.Color.Should().Be(StatusColor.Red);
        status.Text.Should().Be("Something went wrong");
    }

    [Fact]
    public void NodeStatus_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var status = new NodeStatus();

        // Assert
        status.Text.Should().BeEmpty();
        status.Color.Should().Be(StatusColor.Grey);
        status.Shape.Should().Be(StatusShape.Dot);
    }

    #endregion
}
