using System;
using System.Reflection;
using System.Runtime.Loader;

namespace AgduBugdu.Extensibility;

/// <summary>
/// Isolated collectible AssemblyLoadContext for loading extension assemblies.
/// Shares contracts and core dependencies with the host application.
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Share PluginContracts with the host so interfaces match across boundaries
        if (assemblyName.Name == "AgduBugdu.PluginContracts")
        {
            return null; // Fall back to default context
        }

        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
