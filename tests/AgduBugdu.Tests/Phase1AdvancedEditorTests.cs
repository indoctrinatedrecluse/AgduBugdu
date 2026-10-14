using System;
using System.IO;
using System.Linq;
using AgduBugdu.App.Docking;
using AgduBugdu.App.ViewModels.Documents;
using AgduBugdu.Core.Session;
using AgduBugdu.Core.Snippets;
using AgduBugdu.Core.Symbols;
using AgduBugdu.Infrastructure.Session;
using Dock.Model.Core;
using Xunit;

namespace AgduBugdu.Tests;

public class Phase1AdvancedEditorTests
{
    [Fact]
    public void TestCodeSymbolParser_CSharp()
    {
        string csharpCode = @"
namespace DemoApp;

public class CustomerService
{
    public string Name { get; set; }
    public void ProcessOrder(int orderId)
    {
    }
}
";
        var symbols = CodeSymbolParser.Parse("CustomerService.cs", csharpCode);
        Assert.NotEmpty(symbols);
        Assert.Contains(symbols, s => s.Name == "CustomerService" && s.Kind == SymbolKind.Class);
        Assert.Contains(symbols, s => s.Name == "ProcessOrder" && s.Kind == SymbolKind.Method);
    }

    [Fact]
    public void TestCodeSymbolParser_Python()
    {
        string pyCode = @"
class OrderManager:
    def calculate_total(self, items):
        pass

def standalone_helper():
    pass
";
        var symbols = CodeSymbolParser.Parse("manager.py", pyCode);
        Assert.NotEmpty(symbols);
        Assert.Contains(symbols, s => s.Name == "OrderManager" && s.Kind == SymbolKind.Class);
        Assert.Contains(symbols, s => s.Name == "calculate_total" && s.Kind == SymbolKind.Function);
        Assert.Contains(symbols, s => s.Name == "standalone_helper" && s.Kind == SymbolKind.Function);
    }

    [Fact]
    public void TestCodeSymbolParser_Rust()
    {
        string rustCode = @"
pub struct UserProfile {
    id: u64,
}

pub enum Status {
    Active,
    Suspended,
}

pub fn create_user() -> UserProfile {
    UserProfile { id: 1 }
}
";
        var symbols = CodeSymbolParser.Parse("lib.rs", rustCode);
        Assert.NotEmpty(symbols);
        Assert.Contains(symbols, s => s.Name == "UserProfile" && s.Kind == SymbolKind.Struct);
        Assert.Contains(symbols, s => s.Name == "Status" && s.Kind == SymbolKind.Enum);
        Assert.Contains(symbols, s => s.Name == "create_user" && s.Kind == SymbolKind.Function);
    }

    [Fact]
    public void TestCodeSymbolParser_Markdown()
    {
        string md = @"
# Project Overview
Some text here.
## Getting Started
### Prerequisites
";
        var symbols = CodeSymbolParser.Parse("README.md", md);
        Assert.Equal(3, symbols.Count);
        Assert.Contains(symbols, s => s.Name == "Project Overview" && s.Kind == SymbolKind.Heading);
        Assert.Contains(symbols, s => s.Name == "Getting Started" && s.Kind == SymbolKind.Heading);
        Assert.Contains(symbols, s => s.Name == "Prerequisites" && s.Kind == SymbolKind.Heading);
    }

    [Fact]
    public void TestBuiltInSnippets()
    {
        var snippets = BuiltInSnippets.GetDefaultSnippets();
        Assert.NotEmpty(snippets);
        Assert.Contains(snippets, s => s.LanguageId == "csharp" && s.Prefix == "prop");
        Assert.Contains(snippets, s => s.LanguageId == "python" && s.Prefix == "def");
        Assert.Contains(snippets, s => s.LanguageId == "rust" && s.Prefix == "fn");
        Assert.Contains(snippets, s => s.LanguageId == "cpp" && s.Prefix == "cout");
        Assert.Contains(snippets, s => s.LanguageId == "go" && s.Prefix == "func");
    }

    [Fact]
    public void TestSessionService_SaveAndLoad()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AgduBugdu_TestSession_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string sessionFile = Path.Combine(tempDir, "test_session.json");

        try
        {
            var service = new SessionService(sessionFile);
            var session = new WorkspaceSessionData
            {
                WorkspaceFolderPath = tempDir,
                IsSplitActive = true,
                SplitOrientation = "Vertical",
                ActiveDocumentPath = Path.Combine(tempDir, "file1.txt"),
                PrimaryDocuments = { new DocumentSessionData { FilePath = Path.Combine(tempDir, "file1.txt"), CaretLine = 12, CaretColumn = 4 } },
                SecondaryDocuments = { new DocumentSessionData { FilePath = Path.Combine(tempDir, "file2.txt"), CaretLine = 5, CaretColumn = 1 } }
            };

            service.SaveSession(session);
            Assert.True(File.Exists(sessionFile));

            var loaded = service.LoadSession();
            Assert.NotNull(loaded);
            Assert.Equal(tempDir, loaded.WorkspaceFolderPath);
            Assert.True(loaded.IsSplitActive);
            Assert.Equal("Vertical", loaded.SplitOrientation);
            Assert.Single(loaded.PrimaryDocuments);
            Assert.Equal(12, loaded.PrimaryDocuments[0].CaretLine);
            Assert.Single(loaded.SecondaryDocuments);
            Assert.Equal(5, loaded.SecondaryDocuments[0].CaretLine);

            service.ClearSession();
            Assert.False(File.Exists(sessionFile));
            Assert.Null(service.LoadSession());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void TestMainDockFactory_SplitAndMoveDocuments()
    {
        var factory = new MainDockFactory();
        var layout = factory.CreateLayout();
        factory.InitLayout(layout);

        Assert.False(factory.IsSplitActive);

        // Split Editor Horizontally
        factory.SplitEditor(Orientation.Horizontal);
        Assert.True(factory.IsSplitActive);
        Assert.NotNull(factory.SecondaryDocumentDock);

        // Add a document to primary
        var doc = new EditorDocumentViewModel
        {
            FileName = "SplitTest.txt",
            Title = "SplitTest.txt",
            FilePath = "C:/Test/SplitTest.txt"
        };
        factory.DocumentDock?.VisibleDockables?.Add(doc);
        if (factory.DocumentDock != null)
        {
            factory.DocumentDock.ActiveDockable = doc;
            factory.SetActiveDockable(doc);
        }

        // Move active document to other pane
        factory.MoveActiveDocumentToOtherPane();
        Assert.Contains(doc, factory.SecondaryDocumentDock.VisibleDockables!);
        Assert.DoesNotContain(doc, factory.DocumentDock?.VisibleDockables!);

        // Close split
        factory.CloseSplit();
        Assert.False(factory.IsSplitActive);
        // Document should be restored to primary dock
        Assert.Contains(doc, factory.DocumentDock?.VisibleDockables!);
    }
}
