// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Core.Exceptions;

/// <summary>
/// Exception thrown when a version conflict is detected during deployment.
/// This occurs when the client's revision doesn't match the server's revision.
/// </summary>
public class VersionConflictException : Exception
{
    /// <summary>
    /// The revision the client was expecting.
    /// </summary>
    public string? ClientRevision { get; }

    /// <summary>
    /// The current revision on the server.
    /// </summary>
    public string? ServerRevision { get; }

    /// <summary>
    /// HTTP status code for this error (409 Conflict).
    /// </summary>
    public int StatusCode => 409;

    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string ErrorCode => "version_mismatch";

    public VersionConflictException()
        : base("Flow revision mismatch. The flows have been modified by another user.")
    {
    }

    public VersionConflictException(string? clientRevision, string? serverRevision)
        : base($"Flow revision mismatch. Expected: {clientRevision ?? "none"}, Current: {serverRevision ?? "none"}")
    {
        ClientRevision = clientRevision;
        ServerRevision = serverRevision;
    }

    public VersionConflictException(string message)
        : base(message)
    {
    }

    public VersionConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when flow validation fails.
/// </summary>
public class FlowValidationException : Exception
{
    /// <summary>
    /// The validation errors.
    /// </summary>
    public List<string> ValidationErrors { get; }

    /// <summary>
    /// HTTP status code for this error (400 Bad Request).
    /// </summary>
    public int StatusCode => 400;

    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string ErrorCode => "validation_error";

    public FlowValidationException(List<string> errors)
        : base($"Flow validation failed: {string.Join(", ", errors)}")
    {
        ValidationErrors = errors;
    }

    public FlowValidationException(string error)
        : base($"Flow validation failed: {error}")
    {
        ValidationErrors = new List<string> { error };
    }

    public FlowValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        ValidationErrors = new List<string>();
    }
}

/// <summary>
/// Exception thrown when authentication fails.
/// </summary>
public class AuthenticationException : Exception
{
    /// <summary>
    /// HTTP status code for this error (401 Unauthorized).
    /// </summary>
    public int StatusCode => 401;

    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string ErrorCode => "authentication_failed";

    public AuthenticationException()
        : base("Authentication failed")
    {
    }

    public AuthenticationException(string message)
        : base(message)
    {
    }

    public AuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when authorization fails.
/// </summary>
public class AuthorizationException : Exception
{
    /// <summary>
    /// The permission that was required.
    /// </summary>
    public string? RequiredPermission { get; }

    /// <summary>
    /// HTTP status code for this error (403 Forbidden).
    /// </summary>
    public int StatusCode => 403;

    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string ErrorCode => "permission_denied";

    public AuthorizationException()
        : base("Permission denied")
    {
    }

    public AuthorizationException(string requiredPermission)
        : base($"Permission denied. Required: {requiredPermission}")
    {
        RequiredPermission = requiredPermission;
    }

    public AuthorizationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
