// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Common;

/// <summary>
/// Global Config node - a configuration node for storing global settings.
/// This node does not process messages; it provides configuration data
/// that other nodes can reference.
/// </summary>
[NodeType("global-config", "global-config",
    Category = NodeCategory.Config,
    Color = "#a6bbcf",
    Icon = "fa fa-cog",
    Inputs = 0,
    Outputs = 0)]
public class GlobalConfigNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "env", new List<object>() }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("A configuration node for storing global settings and environment variables.")
        .Details(@"
The Global Config node is a configuration node that stores global settings
and environment variables that can be accessed by other nodes.

Environment variables defined in this node are available to:
- All nodes in all flows
- Template expressions
- Function nodes

This is useful for:
- Storing connection strings
- Defining shared constants
- Managing environment-specific settings")
        .Build();

    protected override async Task OnInitializeAsync()
    {
        // Store any configured environment variables in global context
        var envVars = GetConfig<List<object>?>("env", null);
        if (envVars != null)
        {
            foreach (var env in envVars)
            {
                if (env is IDictionary<string, object> envDict &&
                    envDict.TryGetValue("name", out var nameObj) &&
                    envDict.TryGetValue("value", out var valueObj))
                {
                    var name = nameObj?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        Global.Set(name, valueObj);
                    }
                }
            }
        }
        await Task.CompletedTask;
    }

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        // Config nodes don't process messages
        done();
        return Task.CompletedTask;
    }
}
