// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using NodeRed.Core.Interfaces;
using NodeRed.Runtime.Execution;
using NodeRed.Runtime.Services;

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

        return services;
    }
}
