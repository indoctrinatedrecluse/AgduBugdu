using System;
using System.IO;
using System.Threading.Tasks;
using AgduBugdu.App.ViewModels;
using AgduBugdu.App.ViewModels.Documents;
using AgduBugdu.Core.Git;
using AgduBugdu.Core.Symbols;
using AgduBugdu.PluginContracts;
using Xunit;

namespace AgduBugdu.Tests;

public class Phase4ExtensibilityTests
{
    // ==========================================
    // 1. CodeLens with Author Tag
    // ==========================================
    [Fact]
    public void TestCodeLensEngine_WithGitAuthorTag()
    {
        string code = @"
public class OrderService
{
    public void ProcessOrder(int id)
    {
    }
}";
        var items = CodeLensEngine.ResolveCodeLenses(code, ".cs", authorTag: "Abhishek Mitra, 2 days ago");

        Assert.NotEmpty(items);
        var methodLens = items.Find(x => x.SymbolName == "ProcessOrder");
        Assert.NotNull(methodLens);
        Assert.Equal("Abhishek Mitra, 2 days ago", methodLens.AuthorTag);
        Assert.Contains("• Abhishek Mitra, 2 days ago", methodLens.DisplayText);
    }

    // ==========================================
    // 2. CodeLens with Workspace Reference Counter
    // ==========================================
    [Fact]
    public void TestCodeLensEngine_WithWorkspaceReferenceCounter()
    {
        string code = @"
public class PaymentGateway
{
    public void ChargeCreditCard()
    {
    }
}";
        // Mock a workspace reference counter returning 15 references
        Func<string, int> mockCounter = symbol => symbol == "ChargeCreditCard" ? 15 : 0;

        var items = CodeLensEngine.ResolveCodeLenses(
            code,
            ".cs",
            authorTag: "indoctrinatedrecluse",
            workspaceReferenceCounter: mockCounter);

        var methodLens = items.Find(x => x.SymbolName == "ChargeCreditCard");
        Assert.NotNull(methodLens);
        Assert.Equal(15, methodLens.ReferenceCount);
        Assert.Contains("15 references", methodLens.DisplayText);
    }

    // ==========================================
    // 3. Dynamic Extensibility Modal Activation / Deactivation
    // ==========================================
    private class DummyTestExtension : IExtension
    {
        public string Id => "test.dummy.ext";
        public string Name => "Test Dummy Extension";
        public string Version => "1.0.0";
        public int ActivateCount { get; private set; }
        public int DeactivateCount { get; private set; }

        public void Initialize(IExtensionContext context) { }

        public Task ActivateAsync()
        {
            ActivateCount++;
            return Task.CompletedTask;
        }

        public Task DeactivateAsync()
        {
            DeactivateCount++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task TestExtensionsModalViewModel_ToggleLifecycle()
    {
        var ext = new DummyTestExtension();
        var item = new ExtensionDisplayItem
        {
            Id = ext.Id,
            Name = ext.Name,
            Version = ext.Version,
            Description = "Test extension item",
            ExtensionInstance = ext,
            IsActive = true,
            Status = "Active",
            ActionButtonText = "Disable",
            StatusColor = "#34D399",
            StatusBgColor = "#064E3B"
        };

        var modal = new ExtensionsModalViewModel();
        modal.Extensions.Add(item);

        // Toggle from Active -> Disabled
        await modal.ToggleExtensionAsync(item);

        Assert.False(item.IsActive);
        Assert.Equal("Disabled", item.Status);
        Assert.Equal("Enable", item.ActionButtonText);
        Assert.Equal(1, ext.DeactivateCount);

        // Toggle from Disabled -> Active
        await modal.ToggleExtensionAsync(item);

        Assert.True(item.IsActive);
        Assert.Equal("Active", item.Status);
        Assert.Equal("Disable", item.ActionButtonText);
        Assert.Equal(1, ext.ActivateCount);
    }

    // ==========================================
    // 4. Git Last Author Resolution
    // ==========================================
    [Fact]
    public async Task TestGitService_GetFileLastAuthorAsync()
    {
        var gitService = new GitService();
        var currentDir = Directory.GetCurrentDirectory();

        // Query the repository for an existing tracked file
        var author = await gitService.GetFileLastAuthorAsync(currentDir, "README.md");

        if (author != null)
        {
            // Should contain author and relative time
            Assert.NotEmpty(author);
            Assert.Contains(",", author);
        }
    }

    // ==========================================
    // 5. EditorDocumentViewModel Interactive Events
    // ==========================================
    [Fact]
    public void TestEditorDocumentViewModel_InteractiveCodeLensEvents()
    {
        var doc = new EditorDocumentViewModel();
        string? testTriggered = null;
        string? referenceSymbol = null;

        doc.RunTestRequested += (s, name) => testTriggered = name;
        doc.FindReferencesRequested += (s, sym) => referenceSymbol = sym;

        doc.RequestRunTest("MyUnitTest");
        doc.RequestFindReferences("MySymbol");

        Assert.Equal("MyUnitTest", testTriggered);
        Assert.Equal("MySymbol", referenceSymbol);
    }
}
