using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AgduBugdu.App.Docking;
using AgduBugdu.App.ViewModels;
using AgduBugdu.App.ViewModels.Documents;
using Dock.Model.Core;
using Xunit;

namespace AgduBugdu.Tests;

public class DockingTests
{
    [Fact]
    public void TestMainDockFactoryLifecycle()
    {
        var factory = new MainDockFactory();
        var layout = factory.CreateLayout();
        factory.InitLayout(layout);

        Assert.NotNull(layout);
        Assert.NotNull(factory.DocumentDock);
        Assert.NotNull(factory.BottomDock);
        Assert.NotNull(factory.LeftDock);
        Assert.Equal(0.22, factory.LeftDock.Proportion);
        Assert.Equal(0.72, factory.DocumentDock.Proportion);
        Assert.Equal(0.28, factory.BottomDock.Proportion);
    }

    [Fact]
    public void TestMainViewModelDockingInitialization()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.Layout);
        Assert.NotNull(vm.DockFactory);
        Assert.NotNull(vm.DockFactory.ExplorerTool);
        Assert.NotNull(vm.DockFactory.TerminalTool);
        Assert.NotNull(vm.DockFactory.OutputTool);
        Assert.NotNull(vm.DockFactory.TodoTool);
        Assert.NotNull(vm.DockFactory.DebuggerTool);

        Assert.NotNull(vm.DockFactory.LeftDock);
        Assert.NotNull(vm.DockFactory.BottomDock);
        Assert.NotNull(vm.DockFactory.DocumentDock);

        // Check if Explorer is in LeftDock
        Assert.NotNull(vm.DockFactory.LeftDock.VisibleDockables);
        Assert.Contains(vm.DockFactory.ExplorerTool, vm.DockFactory.LeftDock.VisibleDockables);
        // Check if BottomDock has tools
        Assert.NotNull(vm.DockFactory.BottomDock.VisibleDockables);
        Assert.Contains(vm.DockFactory.TerminalTool, vm.DockFactory.BottomDock.VisibleDockables);
        Assert.Contains(vm.DockFactory.OutputTool, vm.DockFactory.BottomDock.VisibleDockables);
        Assert.Contains(vm.DockFactory.TodoTool, vm.DockFactory.BottomDock.VisibleDockables);
        Assert.Contains(vm.DockFactory.DebuggerTool, vm.DockFactory.BottomDock.VisibleDockables);

        // Test Focus commands
        vm.FocusExplorerCommand.Execute(null);
        Assert.Equal(vm.DockFactory.ExplorerTool, vm.DockFactory.LeftDock.ActiveDockable);

        vm.FocusTerminalCommand.Execute(null);
        Assert.Equal(vm.DockFactory.TerminalTool, vm.DockFactory.BottomDock.ActiveDockable);

        vm.FocusOutputCommand.Execute(null);
        Assert.Equal(vm.DockFactory.OutputTool, vm.DockFactory.BottomDock.ActiveDockable);

        vm.FocusTodoCommand.Execute(null);
        Assert.Equal(vm.DockFactory.TodoTool, vm.DockFactory.BottomDock.ActiveDockable);

        vm.FocusDebuggerCommand.Execute(null);
        Assert.Equal(vm.DockFactory.DebuggerTool, vm.DockFactory.BottomDock.ActiveDockable);
    }

    [Fact]
    public void TestDockControlProperties()
    {
        var type = typeof(Dock.Avalonia.Controls.DockControl);
        var props = type.GetProperties().Select(p => p.Name).ToList();
        Assert.Contains("Layout", props);
        bool hasFactory = props.Contains("Factory");
        Assert.True(hasFactory);
    }

    [Fact]
    public void TestMinimizeAndRestoreBottomPane()
    {
        var factory = new MainDockFactory();
        var layout = factory.CreateLayout();
        factory.InitLayout(layout);

        Assert.False(factory.IsBottomPaneMinimized);
        Assert.NotNull(factory.BottomDock);

        // Minimize
        factory.MinimizeBottomPane();
        Assert.True(factory.IsBottomPaneMinimized);

        // Restore
        factory.RestoreBottomPane(factory.TerminalTool);
        Assert.False(factory.IsBottomPaneMinimized);
        Assert.Equal(factory.TerminalTool, factory.BottomDock.ActiveDockable);
        Assert.Equal(0.28, factory.BottomDock.Proportion);
    }

    [Fact]
    public void TestResetLayoutPreservesOpenDocuments()
    {
        var factory = new MainDockFactory();
        var layout = factory.CreateLayout();
        factory.InitLayout(layout);

        var customDoc = new EditorDocumentViewModel
        {
            FileName = "TestFile.cs",
            Title = "TestFile.cs",
            FilePath = "C:/Test/TestFile.cs"
        };
        factory.DocumentDock?.VisibleDockables?.Add(customDoc);
        if (factory.DocumentDock != null)
        {
            factory.DocumentDock.ActiveDockable = customDoc;
            factory.DocumentDock.Proportion = 0.50; // Modified proportion
        }
        if (factory.BottomDock != null)
        {
            factory.BottomDock.Proportion = 0.50; // Modified proportion
        }

        var newLayout = factory.ResetLayout();
        Assert.NotNull(newLayout);
        Assert.Equal(0.72, factory.DocumentDock?.Proportion);
        Assert.Equal(0.28, factory.BottomDock?.Proportion);

        var docNames = factory.DocumentDock?.VisibleDockables?.OfType<EditorDocumentViewModel>().Select(d => d.FileName).ToList();
        Assert.NotNull(docNames);
        Assert.Contains("TestFile.cs", docNames);
        Assert.Equal(customDoc, factory.DocumentDock?.ActiveDockable);
    }

    [Fact]
    public void TestDockableLocatorContainsAllDockables()
    {
        var factory = new MainDockFactory();
        var layout = factory.CreateLayout();
        factory.InitLayout(layout);

        var expectedIds = new[] { "LeftPane", "BottomPane", "DocumentsPane", "Explorer", "Output", "Terminal", "TodoExplorer", "Debugger" };
        foreach (var id in expectedIds)
        {
            var dockable = factory.GetDockable<IDockable>(id);
            Assert.NotNull(dockable);
            Assert.Equal(id, dockable.Id);
        }
    }
}
