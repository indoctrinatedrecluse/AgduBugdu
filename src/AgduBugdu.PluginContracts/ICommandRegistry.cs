using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgduBugdu.PluginContracts;

/// <summary>
/// Allows plugins to register executable commands, hotkeys, and menu items.
/// </summary>
public interface ICommandRegistry
{
    void RegisterCommand(string commandId, string title, Func<Task> execute, string? defaultShortcut = null);
    void RegisterMenuItem(string menuPath, string commandId, int order = 0);
    IReadOnlyDictionary<string, CommandDescriptor> GetRegisteredCommands();
}

public record CommandDescriptor(string Id, string Title, Func<Task> Execute, string? Shortcut);
