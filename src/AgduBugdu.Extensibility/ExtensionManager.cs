using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Extensibility;

public class LoadedExtensionInfo
{
    public IExtension Extension { get; }
    public PluginLoadContext LoadContext { get; }
    public string AssemblyPath { get; }
    public bool IsActive { get; internal set; }

    public LoadedExtensionInfo(IExtension extension, PluginLoadContext loadContext, string assemblyPath)
    {
        Extension = extension;
        LoadContext = loadContext;
        AssemblyPath = assemblyPath;
    }
}

public class ExtensionManager
{
    private readonly IExtensionContext _context;
    private readonly List<LoadedExtensionInfo> _loadedExtensions = new();

    public IReadOnlyList<LoadedExtensionInfo> LoadedExtensions => _loadedExtensions.AsReadOnly();

    public ExtensionManager(IExtensionContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Loads a plugin from a specified DLL file path into an isolated AssemblyLoadContext.
    /// </summary>
    public async Task<IExtension?> LoadExtensionAsync(string dllPath)
    {
        if (!File.Exists(dllPath))
        {
            _context.Log($"Extension DLL not found: {dllPath}", "Warning");
            return null;
        }

        var fullPath = Path.GetFullPath(dllPath);
        var alc = new PluginLoadContext(fullPath);

        Assembly assembly;
        try
        {
            assembly = alc.LoadFromAssemblyPath(fullPath);
        }
        catch (Exception ex)
        {
            _context.Log($"Failed to load assembly at {fullPath}: {ex.Message}", "Error");
            alc.Unload();
            return null;
        }

        var extensionType = assembly.GetTypes().FirstOrDefault(t => typeof(IExtension).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
        if (extensionType == null)
        {
            _context.Log($"No type implementing IExtension found in {Path.GetFileName(fullPath)}", "Warning");
            alc.Unload();
            return null;
        }

        if (Activator.CreateInstance(extensionType) is not IExtension extension)
        {
            _context.Log($"Could not instantiate extension type: {extensionType.FullName}", "Error");
            alc.Unload();
            return null;
        }

        try
        {
            extension.Initialize(_context);
            await extension.ActivateAsync();
            var info = new LoadedExtensionInfo(extension, alc, fullPath) { IsActive = true };
            _loadedExtensions.Add(info);
            _context.Log($"Loaded and activated extension: {extension.Name} v{extension.Version} ({extension.Id})");
            return extension;
        }
        catch (Exception ex)
        {
            _context.Log($"Error activating extension {extension.Name}: {ex.Message}", "Error");
            alc.Unload();
            return null;
        }
    }

    /// <summary>
    /// Scans a directory for all .dll files and attempts to load valid extensions.
    /// </summary>
    public async Task<int> DiscoverAndLoadExtensionsAsync(string extensionsDirectory)
    {
        if (!Directory.Exists(extensionsDirectory))
        {
            return 0;
        }

        var dllFiles = Directory.GetFiles(extensionsDirectory, "*.dll", SearchOption.AllDirectories);
        int loadedCount = 0;

        foreach (var file in dllFiles)
        {
            // Skip contracts and runtime system assemblies
            var fileName = Path.GetFileName(file);
            if (fileName.StartsWith("System.") || fileName.StartsWith("Microsoft.") || fileName.StartsWith("Avalonia.") || fileName == "AgduBugdu.PluginContracts.dll")
            {
                continue;
            }

            var ext = await LoadExtensionAsync(file);
            if (ext != null)
            {
                loadedCount++;
            }
        }

        return loadedCount;
    }

    /// <summary>
    /// Deactivates and unloads an extension, freeing its AssemblyLoadContext.
    /// </summary>
    public async Task UnloadExtensionAsync(IExtension extension)
    {
        var info = _loadedExtensions.FirstOrDefault(e => e.Extension == extension);
        if (info == null) return;

        try
        {
            await info.Extension.DeactivateAsync();
            info.IsActive = false;
        }
        catch (Exception ex)
        {
            _context.Log($"Error deactivating extension {info.Extension.Name}: {ex.Message}", "Warning");
        }

        _loadedExtensions.Remove(info);
        info.LoadContext.Unload();
        _context.Log($"Unloaded extension {info.Extension.Name}");
    }
}
