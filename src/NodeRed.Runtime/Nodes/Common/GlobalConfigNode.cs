// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.Core.Interfaces;

namespace NodeRed.Runtime.Nodes.Common;

/// <summary>
/// Global Config node - a configuration node for storing global settings.
/// This node does not process messages; it provides configuration data
/// that other nodes can reference.
/// </summary>
public class GlobalConfigNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "global-config",
        DisplayName = "global-config",
        Category = NodeCategory.Config,
        Color = "#a6bbcf",
        Icon = "cog",
        Inputs = 0,
        Outputs = 0,
        Defaults = new Dictionary<string, object?>
        {
            { "name", "" },
            { "env", new List<object>() }
        },
        HelpText = "A configuration node for storing global settings and environment variables."
    };

    public override Task InitializeAsync(FlowNode config, INodeContext context)
    {
        var result = base.InitializeAsync(config, context);
        
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
                        Context.SetGlobalContext(name, valueObj);
                    }
                }
            }
        }
        
        return result;
    }

    public override Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        // Config nodes don't process messages
        Done();
        return Task.CompletedTask;
    }
}
