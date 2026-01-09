// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;
using System.Security.Cryptography.X509Certificates;

namespace NodeRed.Runtime.Nodes.Network;

/// <summary>
/// TLS Config node - provides TLS/SSL configuration for secure connections.
/// This is a configuration node that other nodes can reference.
/// </summary>
public class TlsConfigNode : NodeBase
{
    private X509Certificate2? _certificate;
    private bool _isValid = true;
    private string? _errorMessage;

    /// <summary>
    /// Gets whether the TLS configuration is valid.
    /// </summary>
    public bool IsValid => _isValid;

    /// <summary>
    /// Gets the certificate if configured and valid.
    /// </summary>
    public X509Certificate2? Certificate => _certificate;

    /// <summary>
    /// Gets whether to verify server certificates.
    /// </summary>
    public bool VerifyServerCert => GetConfig("verifyservercert", true);

    /// <summary>
    /// Gets the server name for SNI.
    /// </summary>
    public string ServerName => GetConfig<string?>("servername", null)?.Trim() ?? "";

    /// <summary>
    /// Gets the ALPN protocol.
    /// </summary>
    public string AlpnProtocol => GetConfig<string?>("alpnprotocol", null)?.Trim() ?? "";

    public override NodeDefinition Definition => new()
    {
        Type = "tls-config",
        DisplayName = "tls-config",
        Category = NodeCategory.Config,
        Color = "#d8bfd8",
        Icon = "lock",
        Inputs = 0,
        Outputs = 0,
        Defaults = new Dictionary<string, object?>
        {
            { "name", "" },
            { "cert", "" },
            { "key", "" },
            { "ca", "" },
            { "certname", "" },
            { "keyname", "" },
            { "caname", "" },
            { "servername", "" },
            { "verifyservercert", true },
            { "alpnprotocol", "" }
        },
        HelpText = "Provides TLS/SSL configuration for secure connections."
    };

    public override Task InitializeAsync(FlowNode config, INodeContext context)
    {
        var result = base.InitializeAsync(config, context);

        // Try to load certificates
        LoadCertificates();

        return result;
    }

    private void LoadCertificates()
    {
        var certPath = GetConfig<string?>("cert", null)?.Trim() ?? "";
        var keyPath = GetConfig<string?>("key", null)?.Trim() ?? "";
        var caPath = GetConfig<string?>("ca", null)?.Trim() ?? "";

        // Check if file paths are provided
        if (!string.IsNullOrEmpty(certPath) || !string.IsNullOrEmpty(keyPath))
        {
            // Both cert and key must be provided together
            if ((certPath.Length > 0) != (keyPath.Length > 0))
            {
                _isValid = false;
                _errorMessage = "Both certificate and key must be provided together.";
                Log(_errorMessage, LogLevel.Error);
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath))
                {
                    // Load certificate with private key
                    if (!string.IsNullOrEmpty(keyPath) && File.Exists(keyPath))
                    {
                        var certPem = File.ReadAllText(certPath);
                        var keyPem = File.ReadAllText(keyPath);
                        _certificate = X509Certificate2.CreateFromPem(certPem, keyPem);
                    }
                    else
                    {
                        var certPem = File.ReadAllText(certPath);
                        _certificate = X509Certificate2.CreateFromPem(certPem);
                    }
                }
            }
            catch (Exception ex)
            {
                _isValid = false;
                _errorMessage = $"Failed to load certificate: {ex.Message}";
                Log(_errorMessage, LogLevel.Error);
            }
        }
    }

    /// <summary>
    /// Configures TLS options for an HttpClientHandler or similar.
    /// </summary>
    public HttpClientHandler ConfigureHandler(HttpClientHandler? handler = null)
    {
        handler ??= new HttpClientHandler();

        if (!VerifyServerCert)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        if (_certificate != null)
        {
            handler.ClientCertificates.Add(_certificate);
        }

        return handler;
    }

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // Config nodes don't process messages
        Done();
        return Task.CompletedTask;
    }

    public override Task CloseAsync()
    {
        _certificate?.Dispose();
        _certificate = null;
        return base.CloseAsync();
    }
}
