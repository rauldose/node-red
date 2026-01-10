// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;

namespace NodeRed.Core.Interfaces;

/// <summary>
/// Service for validating flow configurations before deployment.
/// </summary>
public interface IFlowValidator
{
    /// <summary>
    /// Validates a workspace configuration.
    /// </summary>
    /// <param name="workspace">The workspace to validate.</param>
    /// <returns>Validation result with any errors or warnings.</returns>
    ValidationResult ValidateWorkspace(Workspace workspace);

    /// <summary>
    /// Validates a single flow.
    /// </summary>
    /// <param name="flow">The flow to validate.</param>
    /// <param name="availableNodes">Available node types.</param>
    /// <returns>Validation result with any errors or warnings.</returns>
    ValidationResult ValidateFlow(Flow flow, IEnumerable<string> availableNodes);

    /// <summary>
    /// Validates a single node configuration.
    /// </summary>
    /// <param name="node">The node to validate.</param>
    /// <param name="nodeDefinition">The node type definition.</param>
    /// <returns>Validation result with any errors or warnings.</returns>
    ValidationResult ValidateNode(FlowNode node, NodeDefinition? nodeDefinition);

    /// <summary>
    /// Validates wire connections in a flow.
    /// </summary>
    /// <param name="flow">The flow containing the wires.</param>
    /// <returns>Validation result with any errors or warnings.</returns>
    ValidationResult ValidateWires(Flow flow);

    /// <summary>
    /// Detects differences between two node configurations.
    /// </summary>
    /// <param name="oldNode">The original node.</param>
    /// <param name="newNode">The new node.</param>
    /// <returns>True if nodes are different (ignoring position).</returns>
    bool DiffNodes(FlowNode? oldNode, FlowNode? newNode);
}

/// <summary>
/// Result of a validation operation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the validation passed (no errors).
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// List of validation errors.
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// List of validation warnings (non-fatal).
    /// </summary>
    public List<ValidationWarning> Warnings { get; set; } = new();

    /// <summary>
    /// Merges another validation result into this one.
    /// </summary>
    public void Merge(ValidationResult other)
    {
        Errors.AddRange(other.Errors);
        Warnings.AddRange(other.Warnings);
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new();

    /// <summary>
    /// Creates a failed validation result with an error.
    /// </summary>
    public static ValidationResult Fail(string message, string? nodeId = null, string? property = null)
    {
        var result = new ValidationResult();
        result.Errors.Add(new ValidationError
        {
            Message = message,
            NodeId = nodeId,
            Property = property
        });
        return result;
    }
}

/// <summary>
/// A validation error.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The node ID where the error occurred.
    /// </summary>
    public string? NodeId { get; set; }

    /// <summary>
    /// The property that has the error.
    /// </summary>
    public string? Property { get; set; }

    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string? Code { get; set; }
}

/// <summary>
/// A validation warning (non-fatal).
/// </summary>
public class ValidationWarning
{
    /// <summary>
    /// Warning message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The node ID where the warning occurred.
    /// </summary>
    public string? NodeId { get; set; }

    /// <summary>
    /// The property that has the warning.
    /// </summary>
    public string? Property { get; set; }
}
