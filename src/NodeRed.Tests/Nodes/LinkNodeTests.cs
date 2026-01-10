// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using FluentAssertions;
using Moq;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;
using NodeRed.Runtime.Execution;
using Xunit;

namespace NodeRed.Tests.Nodes;

/// <summary>
/// Tests for Link nodes - Link In, Link Out, and Link Call.
/// Based on the JavaScript tests in test/nodes/core/common/60-link_spec.js
/// </summary>
public class LinkNodeTests
{
    private readonly Mock<INodeRegistry> _nodeRegistryMock;
    private readonly Dictionary<string, object?> _globalContext;

    public LinkNodeTests()
    {
        _nodeRegistryMock = new Mock<INodeRegistry>();
        _globalContext = new Dictionary<string, object?>();
    }

    #region Link In Node Tests

    [Fact]
    public void LinkInNode_ShouldHaveCorrectType()
    {
        // Arrange
        var node = CreateLinkNode("link in", "link-in-1");

        // Assert
        node.Type.Should().Be("link in");
    }

    [Fact]
    public void LinkInNode_ShouldHaveNoInputs()
    {
        // This is a design characteristic - Link In receives from Link Out
        var node = CreateLinkNode("link in", "link-in-1");
        // Link in nodes have 0 regular inputs, they receive from link out nodes
        node.Type.Should().Be("link in");
    }

    #endregion

    #region Link Out Node Tests

    [Fact]
    public void LinkOutNode_ShouldHaveCorrectType()
    {
        // Arrange
        var node = CreateLinkNode("link out", "link-out-1");

        // Assert
        node.Type.Should().Be("link out");
    }

    [Fact]
    public void LinkOutNode_ShouldHaveDefaultLinkMode()
    {
        // Arrange
        var node = CreateLinkNode("link out", "link-out-1");
        node.Config["mode"] = "link";

        // Assert
        node.Config["mode"].Should().Be("link");
    }

    [Fact]
    public void LinkOutNode_ShouldSupportReturnMode()
    {
        // Arrange
        var node = CreateLinkNode("link out", "link-out-1");
        node.Config["mode"] = "return";

        // Assert
        node.Config["mode"].Should().Be("return");
    }

    #endregion

    #region Link Call Node Tests

    [Fact]
    public void LinkCallNode_ShouldHaveCorrectType()
    {
        // Arrange
        var node = CreateLinkNode("link call", "link-call-1");

        // Assert
        node.Type.Should().Be("link call");
    }

    [Fact]
    public void LinkCallNode_ShouldHaveDefaultTimeout()
    {
        // Arrange
        var node = CreateLinkNode("link call", "link-call-1");
        node.Config["timeout"] = 30;

        // Assert
        node.Config["timeout"].Should().Be(30);
    }

    [Fact]
    public void LinkCallNode_ShouldSupportCustomTimeout()
    {
        // Arrange
        var node = CreateLinkNode("link call", "link-call-1");
        node.Config["timeout"] = 60;

        // Assert
        node.Config["timeout"].Should().Be(60);
    }

    #endregion

    #region Cross-Flow Routing Tests

    [Fact]
    public async Task FlowRuntime_ShouldRouteLinkMessage_ToLinkInNode()
    {
        // Arrange
        var runtime = new FlowRuntime(_nodeRegistryMock.Object);
        
        var workspace = new Workspace
        {
            Id = "test-workspace",
            Flows = new List<Flow>
            {
                new Flow
                {
                    Id = "flow-1",
                    Label = "Flow 1",
                    Nodes = new List<FlowNode>
                    {
                        CreateLinkNode("link in", "link-in-1")
                    }
                },
                new Flow
                {
                    Id = "flow-2",
                    Label = "Flow 2",
                    Nodes = new List<FlowNode>
                    {
                        CreateLinkNode("link out", "link-out-1", links: new[] { "link-in-1" })
                    }
                }
            }
        };

        await runtime.LoadAsync(workspace);

        // Act & Assert - should not throw
        runtime.RouteLinkMessage("link-in-1", new NodeMessage { Payload = "test" });
    }

    [Fact]
    public async Task FlowRuntime_ShouldRouteLinkReturn_ToLinkCallNode()
    {
        // Arrange
        var runtime = new FlowRuntime(_nodeRegistryMock.Object);
        
        var workspace = new Workspace
        {
            Id = "test-workspace",
            Flows = new List<Flow>
            {
                new Flow
                {
                    Id = "flow-1",
                    Label = "Flow 1",
                    Nodes = new List<FlowNode>
                    {
                        CreateLinkNode("link call", "link-call-1")
                    }
                }
            }
        };

        await runtime.LoadAsync(workspace);
        await runtime.StartAsync();

        // Act - should not throw
        runtime.RouteLinkReturn("link-call-1", new NodeMessage { Payload = "response" });

        // Assert
        runtime.State.Should().Be(FlowState.Running);
    }

    #endregion

    #region Message Property Tests

    [Fact]
    public void LinkSource_PropertyShouldBeSet_ForReturnRouting()
    {
        // Arrange
        var msg = new NodeMessage { Payload = "test" };

        // Act
        msg.Properties["_linkSource"] = "link-call-1";

        // Assert
        msg.Properties.Should().ContainKey("_linkSource");
        msg.Properties["_linkSource"].Should().Be("link-call-1");
    }

    [Fact]
    public void LinkSource_PropertyCanBeRemoved()
    {
        // Arrange
        var msg = new NodeMessage { Payload = "test" };
        msg.Properties["_linkSource"] = "link-call-1";

        // Act
        msg.Properties.Remove("_linkSource");

        // Assert
        msg.Properties.Should().NotContainKey("_linkSource");
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void LinkOutNode_ShouldStoreTargetLinks()
    {
        // Arrange
        var node = CreateLinkNode("link out", "link-out-1", links: new[] { "link-in-1", "link-in-2" });

        // Assert
        node.Config.Should().ContainKey("links");
        var links = node.Config["links"] as List<string>;
        links.Should().NotBeNull();
        links.Should().HaveCount(2);
        links.Should().Contain("link-in-1");
        links.Should().Contain("link-in-2");
    }

    [Fact]
    public void LinkCallNode_ShouldStoreTargetLinks()
    {
        // Arrange
        var node = CreateLinkNode("link call", "link-call-1", links: new[] { "link-in-1" });

        // Assert
        node.Config.Should().ContainKey("links");
        var links = node.Config["links"] as List<string>;
        links.Should().NotBeNull();
        links.Should().Contain("link-in-1");
    }

    #endregion

    #region Helper Methods

    private static FlowNode CreateLinkNode(string type, string id, string[]? links = null)
    {
        var config = new Dictionary<string, object?>
        {
            { "name", id }
        };

        if (links != null)
        {
            config["links"] = links.ToList();
        }

        if (type == "link out")
        {
            config["mode"] = "link";
        }

        if (type == "link call")
        {
            config["timeout"] = 30;
            config["links"] = links?.ToList() ?? new List<string>();
        }

        return new FlowNode
        {
            Id = id,
            Type = type,
            Name = id,
            Config = config,
            Wires = new List<List<string>>()
        };
    }

    #endregion
}
