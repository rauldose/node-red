// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using FluentAssertions;
using Moq;
using NodeRed.Core.Entities;
using NodeRed.Core.Exceptions;
using NodeRed.Core.Interfaces;
using NodeRed.Runtime.Services;
using Xunit;

namespace NodeRed.Tests.Services;

/// <summary>
/// Tests for FlowValidator service.
/// </summary>
public class FlowValidatorTests
{
    private readonly Mock<INodeRegistry> _nodeRegistryMock;
    private readonly FlowValidator _validator;

    public FlowValidatorTests()
    {
        _nodeRegistryMock = new Mock<INodeRegistry>();
        _nodeRegistryMock.Setup(r => r.GetAllDefinitions())
            .Returns(new List<NodeDefinition>
            {
                new NodeDefinition { Type = "inject", DisplayName = "Inject" },
                new NodeDefinition { Type = "debug", DisplayName = "Debug" },
                new NodeDefinition { Type = "function", DisplayName = "Function" }
            });
        
        _validator = new FlowValidator(_nodeRegistryMock.Object);
    }

    #region Workspace Validation Tests

    [Fact]
    public void ValidateWorkspace_ShouldPass_WhenValid()
    {
        // Arrange
        var workspace = CreateValidWorkspace();

        // Act
        var result = _validator.ValidateWorkspace(workspace);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateWorkspace_ShouldDetect_DuplicateFlowIds()
    {
        // Arrange
        var workspace = new Workspace
        {
            Flows = new List<Flow>
            {
                new Flow { Id = "flow-1", Label = "Flow 1" },
                new Flow { Id = "flow-1", Label = "Flow 2" } // Duplicate
            }
        };

        // Act
        var result = _validator.ValidateWorkspace(workspace);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "duplicate_id");
    }

    [Fact]
    public void ValidateWorkspace_ShouldDetect_DuplicateNodeIds()
    {
        // Arrange
        var workspace = new Workspace
        {
            Flows = new List<Flow>
            {
                new Flow
                {
                    Id = "flow-1",
                    Nodes = new List<FlowNode>
                    {
                        new FlowNode { Id = "node-1", Type = "inject" },
                        new FlowNode { Id = "node-1", Type = "debug" } // Duplicate
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateWorkspace(workspace);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "duplicate_id");
    }

    #endregion

    #region Flow Validation Tests

    [Fact]
    public void ValidateFlow_ShouldPass_WhenValid()
    {
        // Arrange
        var flow = CreateValidFlow();

        // Act
        var result = _validator.ValidateFlow(flow, new[] { "inject", "debug", "function" });

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateFlow_ShouldWarn_WhenUnknownNodeType()
    {
        // Arrange
        var flow = new Flow
        {
            Id = "flow-1",
            Nodes = new List<FlowNode>
            {
                new FlowNode { Id = "node-1", Type = "unknown-type" }
            }
        };

        // Act
        var result = _validator.ValidateFlow(flow, new[] { "inject", "debug" });

        // Assert
        result.IsValid.Should().BeTrue(); // Warnings don't fail validation
        result.Warnings.Should().Contain(w => w.Message.Contains("Unknown node type"));
    }

    [Fact]
    public void ValidateFlow_ShouldNotWarn_ForSubflowType()
    {
        // Arrange
        var flow = new Flow
        {
            Id = "flow-1",
            Nodes = new List<FlowNode>
            {
                new FlowNode { Id = "node-1", Type = "subflow:subflow-123" }
            }
        };

        // Act
        var result = _validator.ValidateFlow(flow, new[] { "inject", "debug" });

        // Assert
        result.Warnings.Should().NotContain(w => w.Message.Contains("Unknown node type"));
    }

    #endregion

    #region Node Validation Tests

    [Fact]
    public void ValidateNode_ShouldFail_WhenIdMissing()
    {
        // Arrange
        var node = new FlowNode { Id = "", Type = "inject" };

        // Act
        var result = _validator.ValidateNode(node, null);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "missing_id");
    }

    [Fact]
    public void ValidateNode_ShouldFail_WhenTypeMissing()
    {
        // Arrange
        var node = new FlowNode { Id = "node-1", Type = "" };

        // Act
        var result = _validator.ValidateNode(node, null);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "missing_type");
    }

    #endregion

    #region Wire Validation Tests

    [Fact]
    public void ValidateWires_ShouldFail_WhenTargetNotExists()
    {
        // Arrange
        var flow = new Flow
        {
            Id = "flow-1",
            Nodes = new List<FlowNode>
            {
                new FlowNode
                {
                    Id = "node-1",
                    Type = "inject",
                    Wires = new List<List<string>> { new List<string> { "nonexistent-node" } }
                }
            }
        };

        // Act
        var result = _validator.ValidateWires(flow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "invalid_wire_target");
    }

    [Fact]
    public void ValidateWires_ShouldPass_WhenTargetsExist()
    {
        // Arrange
        var flow = new Flow
        {
            Id = "flow-1",
            Nodes = new List<FlowNode>
            {
                new FlowNode
                {
                    Id = "node-1",
                    Type = "inject",
                    Wires = new List<List<string>> { new List<string> { "node-2" } }
                },
                new FlowNode { Id = "node-2", Type = "debug" }
            }
        };

        // Act
        var result = _validator.ValidateWires(flow);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region DiffNodes Tests

    [Fact]
    public void DiffNodes_ShouldReturnTrue_WhenNull()
    {
        // Arrange & Act & Assert
        _validator.DiffNodes(null, null).Should().BeTrue();
        _validator.DiffNodes(new FlowNode { Type = "inject" }, null).Should().BeTrue();
        _validator.DiffNodes(null, new FlowNode { Type = "inject" }).Should().BeTrue();
    }

    [Fact]
    public void DiffNodes_ShouldReturnFalse_WhenSame()
    {
        // Arrange
        var node1 = new FlowNode { Id = "n1", Type = "inject", Name = "Test" };
        var node2 = new FlowNode { Id = "n1", Type = "inject", Name = "Test" };

        // Act & Assert
        _validator.DiffNodes(node1, node2).Should().BeFalse();
    }

    [Fact]
    public void DiffNodes_ShouldReturnTrue_WhenTypeChanged()
    {
        // Arrange
        var node1 = new FlowNode { Id = "n1", Type = "inject", Name = "Test" };
        var node2 = new FlowNode { Id = "n1", Type = "debug", Name = "Test" };

        // Act & Assert
        _validator.DiffNodes(node1, node2).Should().BeTrue();
    }

    [Fact]
    public void DiffNodes_ShouldIgnorePosition()
    {
        // Arrange
        var node1 = new FlowNode { Id = "n1", Type = "inject", X = 100, Y = 100 };
        var node2 = new FlowNode { Id = "n1", Type = "inject", X = 200, Y = 200 };

        // Act & Assert
        _validator.DiffNodes(node1, node2).Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private static Workspace CreateValidWorkspace()
    {
        return new Workspace
        {
            Id = "workspace-1",
            Flows = new List<Flow> { CreateValidFlow() }
        };
    }

    private static Flow CreateValidFlow()
    {
        return new Flow
        {
            Id = "flow-1",
            Label = "Test Flow",
            Nodes = new List<FlowNode>
            {
                new FlowNode
                {
                    Id = "node-1",
                    Type = "inject",
                    Wires = new List<List<string>> { new List<string> { "node-2" } }
                },
                new FlowNode { Id = "node-2", Type = "debug" }
            }
        };
    }

    #endregion
}

/// <summary>
/// Tests for version conflict detection.
/// </summary>
public class VersionConflictTests
{
    [Fact]
    public void Workspace_UpdateRevision_ShouldGenerateNewRevision()
    {
        // Arrange
        var workspace = new Workspace();
        var originalRevision = workspace.Revision;

        // Act
        workspace.UpdateRevision();

        // Assert
        workspace.Revision.Should().NotBe(originalRevision);
    }

    [Fact]
    public void VersionConflictException_ShouldContainRevisions()
    {
        // Arrange
        var clientRev = "client-123";
        var serverRev = "server-456";

        // Act
        var exception = new VersionConflictException(clientRev, serverRev);

        // Assert
        exception.ClientRevision.Should().Be(clientRev);
        exception.ServerRevision.Should().Be(serverRev);
        exception.StatusCode.Should().Be(409);
        exception.ErrorCode.Should().Be("version_mismatch");
    }
}
