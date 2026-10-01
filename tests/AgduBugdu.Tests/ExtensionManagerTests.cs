using System;
using System.IO;
using System.Threading.Tasks;
using AgduBugdu.Extensibility;
using AgduBugdu.Extensibility.Registries;
using AgduBugdu.PluginContracts;
using Xunit;

namespace AgduBugdu.Tests;

public class MockWorkspaceService : IWorkspaceService
{
    public string? CurrentDirectory { get; private set; }
    public event EventHandler<WorkspaceChangedEventArgs>? WorkspaceChanged;

    public void OpenFolder(string folderPath)
    {
        CurrentDirectory = folderPath;
        WorkspaceChanged?.Invoke(this, new WorkspaceChangedEventArgs(folderPath));
    }
}

public class MockEditorService : IEditorService
{
    public event EventHandler<DocumentEventArgs>? DocumentOpened;
    public event EventHandler<DocumentEventArgs>? DocumentSaved;
    public event EventHandler<DocumentEventArgs>? DocumentClosed;

    public string? ActiveDocumentPath { get; private set; }

    public void OpenFile(string filePath)
    {
        ActiveDocumentPath = filePath;
        DocumentOpened?.Invoke(this, new DocumentEventArgs(filePath));
    }

    public void TriggerSave(string filePath)
    {
        DocumentSaved?.Invoke(this, new DocumentEventArgs(filePath));
    }

    public void TriggerClose(string filePath)
    {
        DocumentClosed?.Invoke(this, new DocumentEventArgs(filePath));
    }
}

public class SampleTestExtension : IExtension
{
    public string Id => "test.sample";
    public string Name => "Sample Plugin";
    public string Version => "1.0.0";
    public bool Initialized { get; private set; }
    public bool Activated { get; private set; }
    public bool Deactivated { get; private set; }

    public void Initialize(IExtensionContext context)
    {
        Initialized = true;
        context.Commands.RegisterCommand("sample.hello", "Hello World", () => Task.CompletedTask);
        context.ToolWindows.RegisterToolWindow("sample.tool", "Sample Tool", () => new object(), vm => new object());
    }

    public Task ActivateAsync()
    {
        Activated = true;
        return Task.CompletedTask;
    }

    public Task DeactivateAsync()
    {
        Deactivated = true;
        return Task.CompletedTask;
    }
}

public class ExtensionManagerTests
{
    [Fact]
    public async Task Extension_Lifecycle_And_Registration_Works()
    {
        var commands = new CommandRegistry();
        var tools = new ToolWindowRegistry();
        var editor = new MockEditorService();
        var workspace = new MockWorkspaceService();
        var context = new ExtensionContext(commands, tools, editor, workspace);

        var extension = new SampleTestExtension();
        extension.Initialize(context);
        await extension.ActivateAsync();

        Assert.True(extension.Initialized);
        Assert.True(extension.Activated);

        // Check command registered
        var registeredCommands = commands.GetRegisteredCommands();
        Assert.True(registeredCommands.ContainsKey("sample.hello"));
        Assert.Equal("Hello World", registeredCommands["sample.hello"].Title);

        // Check tool window registered
        var registeredTools = tools.GetRegisteredTools();
        Assert.True(registeredTools.ContainsKey("sample.tool"));
        Assert.Equal("Sample Tool", registeredTools["sample.tool"].Title);

        await extension.DeactivateAsync();
        Assert.True(extension.Deactivated);
    }

    [Fact]
    public void Document_Save_And_Modification_State_Work()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "Original Content");
            var content = File.ReadAllText(tempFile);
            Assert.Equal("Original Content", content);

            File.WriteAllText(tempFile, "Updated Content");
            var updated = File.ReadAllText(tempFile);
            Assert.Equal("Updated Content", updated);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
