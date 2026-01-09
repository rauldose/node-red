// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using NodeRed.Core.Interfaces;
using NodeRed.Runtime.Execution;
using NodeRed.Runtime.Services;
using NodeRed.SDK;

namespace NodeRed.Runtime;

/// <summary>
/// Extension methods for registering runtime services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Node-RED runtime services to the service collection.
    /// </summary>
    public static IServiceCollection AddNodeRedRuntime(this IServiceCollection services)
    {
        // Register core services
        services.AddSingleton<INodeRegistry, NodeRegistry>();
        services.AddSingleton<IFlowStorage, InMemoryFlowStorage>();
        services.AddSingleton<IFlowRuntime, FlowRuntime>();
        services.AddSingleton<NodeLoader>();

        return services;
    }

    /// <summary>
    /// Adds Node-RED runtime services with a custom storage implementation.
    /// </summary>
    public static IServiceCollection AddNodeRedRuntime<TStorage>(this IServiceCollection services)
        where TStorage : class, IFlowStorage
    {
        services.AddSingleton<INodeRegistry, NodeRegistry>();
        services.AddSingleton<IFlowStorage, TStorage>();
        services.AddSingleton<IFlowRuntime, FlowRuntime>();
        services.AddSingleton<NodeLoader>();

        return services;
    }

    /// <summary>
    /// Loads node plugins from a directory.
    /// Call this after building the service provider to discover and register external nodes.
    /// </summary>
    public static IServiceProvider LoadNodePlugins(this IServiceProvider serviceProvider, string pluginsPath)
    {
        var loader = serviceProvider.GetRequiredService<NodeLoader>();
        var registry = serviceProvider.GetRequiredService<INodeRegistry>();

        if (Directory.Exists(pluginsPath))
        {
            var nodeTypes = loader.LoadFromDirectory(pluginsPath);
            
            foreach (var nodeType in nodeTypes)
            {
                // Register each discovered node type with the registry
                var attr = nodeType.ImplementationType.GetCustomAttributes(typeof(NodeTypeAttribute), false)
                    .FirstOrDefault() as NodeTypeAttribute;

                if (attr != null)
                {
                    Console.WriteLine($"[Plugin] Loaded node type: {attr.Type} ({attr.DisplayName}) from {nodeType.ImplementationType.Assembly.GetName().Name}");
                }
            }

            // Log any errors
            foreach (var (path, error) in loader.GetLoadErrors())
            {
                Console.WriteLine($"[Plugin] Failed to load {Path.GetFileName(path)}: {error}");
            }
        }
        else
        {
            Console.WriteLine($"[Plugin] Plugins directory not found: {pluginsPath}");
        }

        return serviceProvider;
    }
}
