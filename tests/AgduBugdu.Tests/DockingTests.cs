using System;
using System.Collections.Generic;
using System.Linq;
using AgduBugdu.App.Docking;
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
