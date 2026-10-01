using System.Threading.Tasks;

namespace AgduBugdu.PluginContracts;

/// <summary>
/// Root lifecycle contract for external AgduBugdu extension DLLs.
/// </summary>
public interface IExtension
{
    /// <summary>
    /// Unique identifier for the extension, e.g. "com.author.plugin".
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name of the extension.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Extension semantic version.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Called immediately after the extension assembly is loaded.
    /// Used to register services, commands, and tool windows.
    /// </summary>
    void Initialize(IExtensionContext context);

    /// <summary>
    /// Called when the extension is activated by the host.
    /// </summary>
    Task ActivateAsync();

    /// <summary>
    /// Called when the extension is deactivated or unloaded.
    /// Should release resources and event subscriptions.
    /// </summary>
    Task DeactivateAsync();
}
