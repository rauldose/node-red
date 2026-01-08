// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;

namespace NodeRed.SDK;

/// <summary>
/// Fluent builder for creating node help text.
/// 
/// Example:
/// <code>
/// protected override NodeHelpText DefineHelp() =>
///     HelpBuilder.Create()
///         .Summary("Injects a message into the flow.")
///         .AddInput("payload", "string | number | object", "The payload to inject")
///         .AddInput("topic", "string", "The message topic")
///         .AddOutput("payload", "any", "The injected payload")
///         .Details(@"
///             This node can inject a message at regular intervals or when triggered.
///             The payload can be configured as a timestamp, string, number, or JSON object.
///         ")
///         .AddReference("Node-RED Documentation", "https://nodered.org/docs/")
///         .Build();
/// </code>
/// </summary>
public class HelpBuilder
{
    private string _summary = "";
    private readonly List<HelpProperty> _inputs = new();
    private readonly List<HelpProperty> _outputs = new();
    private string _details = "";
    private readonly List<HelpReference> _references = new();

    public static HelpBuilder Create() => new();

    /// <summary>
    /// Sets the short summary (shown as palette tooltip).
    /// </summary>
    public HelpBuilder Summary(string summary)
    {
        _summary = summary;
        return this;
    }

    /// <summary>
    /// Adds an input property description.
    /// </summary>
    public HelpBuilder AddInput(string name, string type, string description)
    {
        _inputs.Add(new HelpProperty { Name = name, Type = type, Description = description });
        return this;
    }

    /// <summary>
    /// Adds an output property description.
    /// </summary>
    public HelpBuilder AddOutput(string name, string type, string description)
    {
        _outputs.Add(new HelpProperty { Name = name, Type = type, Description = description });
        return this;
    }

    /// <summary>
    /// Sets detailed usage information.
    /// </summary>
    public HelpBuilder Details(string details)
    {
        _details = details.Trim();
        return this;
    }

    /// <summary>
    /// Adds a reference link.
    /// </summary>
    public HelpBuilder AddReference(string title, string url)
    {
        _references.Add(new HelpReference { Title = title, Url = url });
        return this;
    }

    /// <summary>
    /// Builds the help text object.
    /// </summary>
    public NodeHelpText Build() => new()
    {
        Summary = _summary,
        Inputs = _inputs,
        Outputs = _outputs,
        Details = _details,
        References = _references
    };
}
