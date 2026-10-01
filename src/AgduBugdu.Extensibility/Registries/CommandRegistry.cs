using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Extensibility.Registries;

public class CommandRegistry : ICommandRegistry
{
    private readonly ConcurrentDictionary<string, CommandDescriptor> _commands = new();
    private readonly ConcurrentDictionary<string, List<string>> _menuItems = new();

    public void RegisterCommand(string commandId, string title, Func<Task> execute, string? defaultShortcut = null)
    {
        _commands[commandId] = new CommandDescriptor(commandId, title, execute, defaultShortcut);
    }

    public void RegisterMenuItem(string menuPath, string commandId, int order = 0)
    {
        _menuItems.AddOrUpdate(menuPath,
            _ => new List<string> { commandId },
            (_, list) => { lock (list) { list.Add(commandId); } return list; });
    }

    public IReadOnlyDictionary<string, CommandDescriptor> GetRegisteredCommands() => _commands;
    public IReadOnlyDictionary<string, List<string>> GetMenuItems() => _menuItems;
}
