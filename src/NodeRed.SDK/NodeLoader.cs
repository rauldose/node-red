// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Reflection;
using NodeRed.Core.Entities;
using NodeRed.Core.Interfaces;

namespace NodeRed.SDK;

/// <summary>
/// Service for discovering and loading node types from assemblies.
/// Supports loading nodes from:
/// - The main application assembly
/// - Referenced assemblies
/// - External plugin DLLs in a nodes directory
/// </summary>
public interface INodeLoader
{
    /// <summary>
    /// Discovers all node types from the specified assemblies.
    /// </summary>
    IEnumerable<NodeTypeInfo> DiscoverNodes(params Assembly[] assemblies);

    /// <summary>
    /// Loads nodes from a plugins directory.
    /// </summary>
    IEnumerable<NodeTypeInfo> LoadFromDirectory(string path);

    /// <summary>
    /// Creates an instance of a node by type name.
    /// </summary>
    INode? CreateNode(string typeName);

    /// <summary>
    /// Gets all registered node definitions.
    /// </summary>
    IEnumerable<NodeDefinition> GetNodeDefinitions();
}

/// <summary>
/// Information about a discovered node type.
/// </summary>
public class NodeTypeInfo
{
    /// <summary>
    /// The node type identifier.
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// The .NET type that implements the node.
    /// </summary>
    public required Type ImplementationType { get; init; }

    /// <summary>
    /// The node's definition.
    /// </summary>
    public required NodeDefinition Definition { get; init; }

    /// <summary>
    /// The module this node belongs to (null for built-in nodes).
    /// </summary>
    public NodeModuleInfo? Module { get; init; }
}

/// <summary>
/// Information about a node module (package).
/// </summary>
public class NodeModuleInfo
{
    public required string Name { get; init; }
    public string Version { get; init; } = "1.0.0";
    public string Description { get; init; } = "";
    public string Author { get; init; } = "";
    public string AssemblyPath { get; init; } = "";
}

/// <summary>
/// Default implementation of node loader.
/// </summary>
public class NodeLoader : INodeLoader
{
    private readonly Dictionary<string, NodeTypeInfo> _nodeTypes = new();
    private readonly List<NodeModuleInfo> _modules = new();

    /// <summary>
    /// Discovers all node types from the specified assemblies.
    /// </summary>
    public IEnumerable<NodeTypeInfo> DiscoverNodes(params Assembly[] assemblies)
    {
        var discovered = new List<NodeTypeInfo>();

        foreach (var assembly in assemblies)
        {
            // Find the module attribute if present
            var moduleAttr = assembly.GetCustomAttribute<NodeModuleAttribute>();
            NodeModuleInfo? moduleInfo = null;

            if (moduleAttr != null)
            {
                moduleInfo = new NodeModuleInfo
                {
                    Name = moduleAttr.Name,
                    Version = moduleAttr.Version,
                    Description = moduleAttr.Description,
                    Author = moduleAttr.Author,
                    AssemblyPath = assembly.Location
                };
                _modules.Add(moduleInfo);
            }

            // Find all types with NodeTypeAttribute
            var nodeTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(INode).IsAssignableFrom(t))
                .Where(t => t.GetCustomAttribute<NodeTypeAttribute>() != null);

            foreach (var type in nodeTypes)
            {
                var attr = type.GetCustomAttribute<NodeTypeAttribute>()!;
                
                // Create a temporary instance to get the full definition
                NodeDefinition definition;
                try
                {
                    var instance = (NodeBase)Activator.CreateInstance(type)!;
                    // We need to call a method to get the definition
                    // For now, use the attribute info
                    definition = new NodeDefinition
                    {
                        Type = attr.Type,
                        DisplayName = attr.DisplayName,
                        Category = attr.Category,
                        Color = attr.Color,
                        Icon = attr.Icon,
                        Inputs = attr.Inputs,
                        Outputs = attr.Outputs,
                        HasButton = attr.HasButton
                    };
                }
                catch
                {
                    // If we can't create an instance, use attribute info only
                    definition = new NodeDefinition
                    {
                        Type = attr.Type,
                        DisplayName = attr.DisplayName,
                        Category = attr.Category,
                        Color = attr.Color,
                        Icon = attr.Icon,
                        Inputs = attr.Inputs,
                        Outputs = attr.Outputs,
                        HasButton = attr.HasButton
                    };
                }

                var nodeInfo = new NodeTypeInfo
                {
                    TypeName = attr.Type,
                    ImplementationType = type,
                    Definition = definition,
                    Module = moduleInfo
                };

                _nodeTypes[attr.Type] = nodeInfo;
                discovered.Add(nodeInfo);
            }
        }

        return discovered;
    }

    /// <summary>
    /// Loads nodes from a plugins directory.
    /// Each plugin can optionally include a plugin.json manifest.
    /// </summary>
    public IEnumerable<NodeTypeInfo> LoadFromDirectory(string path)
    {
        var discovered = new List<NodeTypeInfo>();

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            return discovered;
        }

        var dllFiles = Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                // Check for plugin manifest
                var manifestPath = Path.Combine(Path.GetDirectoryName(dllPath) ?? "", "plugin.json");
                PluginManifest? manifest = null;
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var json = File.ReadAllText(manifestPath);
                        manifest = System.Text.Json.JsonSerializer.Deserialize<PluginManifest>(json);
                    }
                    catch
                    {
                        // Invalid manifest - continue without it
                    }
                }

                var assembly = LoadAssembly(dllPath);
                if (assembly != null)
                {
                    var nodes = DiscoverNodes(assembly);
                    
                    // Update module info with manifest data if available
                    if (manifest != null)
                    {
                        foreach (var node in nodes)
                        {
                            if (node.Module != null)
                            {
                                // Module info is immutable, but we track the manifest separately
                                _manifests[node.Module.Name] = manifest;
                            }
                        }
                    }
                    
                    discovered.AddRange(nodes);
                }
            }
            catch (Exception ex)
            {
                // Track loading errors for diagnostic purposes
                _loadErrors[dllPath] = ex.Message;
            }
        }

        return discovered;
    }

    /// <summary>
    /// Gets any errors that occurred during plugin loading.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetLoadErrors() => _loadErrors;

    /// <summary>
    /// Gets loaded plugin manifests.
    /// </summary>
    public IReadOnlyDictionary<string, PluginManifest> GetManifests() => _manifests;

    private readonly Dictionary<string, string> _loadErrors = new();
    private readonly Dictionary<string, PluginManifest> _manifests = new();

    /// <summary>
    /// Creates an instance of a node by type name.
    /// </summary>
    public INode? CreateNode(string typeName)
    {
        if (!_nodeTypes.TryGetValue(typeName, out var nodeInfo))
            return null;

        try
        {
            return (INode)Activator.CreateInstance(nodeInfo.ImplementationType)!;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets all registered node definitions.
    /// </summary>
    public IEnumerable<NodeDefinition> GetNodeDefinitions()
    {
        return _nodeTypes.Values.Select(n => n.Definition);
    }

    private Assembly? LoadAssembly(string path)
    {
        try
        {
            // Use AssemblyLoadContext for proper isolation
            var context = new PluginLoadContext(path);
            return context.LoadFromAssemblyPath(path);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Custom assembly load context for plugin isolation.
/// Handles dependency version conflicts by:
/// 1. Sharing SDK and Core assemblies with the host application
/// 2. Loading plugin-specific dependencies in isolation
/// 3. Falling back to host assemblies for shared dependencies
/// </summary>
internal class PluginLoadContext : System.Runtime.Loader.AssemblyLoadContext
{
    private readonly System.Runtime.Loader.AssemblyDependencyResolver _resolver;
    
    /// <summary>
    /// Assemblies that should be shared with the host (not loaded per-plugin).
    /// This prevents version conflicts for core framework types.
    /// </summary>
    private static readonly HashSet<string> SharedAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "NodeRed.Core",
        "NodeRed.SDK",
        "System.Runtime",
        "System.Private.CoreLib",
        "System.Collections",
        "System.Linq",
        "System.Text.Json",
        "Microsoft.Extensions.DependencyInjection.Abstractions"
    };

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new System.Runtime.Loader.AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // For shared assemblies, use the host's version to ensure type compatibility
        if (IsSharedAssembly(assemblyName.Name))
        {
            // Return null to fall back to the default context (host application)
            return null;
        }

        // For plugin-specific dependencies, try to load from plugin's directory
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Fall back to host for any unresolved assemblies
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }

    private static bool IsSharedAssembly(string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
            return false;

        // Check if it's in the explicit shared list
        if (SharedAssemblies.Contains(assemblyName))
            return true;

        // System and Microsoft assemblies should generally be shared
        if (assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
            return true;

        if (assemblyName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) &&
            !assemblyName.StartsWith("Microsoft.Data.", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}

/// <summary>
/// Plugin manifest for declaring dependencies and compatibility.
/// </summary>
public class PluginManifest
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string MinRuntimeVersion { get; set; } = "1.0.0";
    public List<PluginDependency> Dependencies { get; set; } = new();
}

/// <summary>
/// Dependency declaration for a plugin.
/// </summary>
public class PluginDependency
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public bool Optional { get; set; } = false;
}
