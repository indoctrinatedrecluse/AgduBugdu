using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AgduBugdu.App.ViewModels;
using AgduBugdu.Extensibility;
using AgduBugdu.Extensibility.Registries;
using AgduBugdu.Plugin.PdfLive;
using AgduBugdu.PluginContracts;
using Xunit;

namespace AgduBugdu.Tests;

public class PdfLiveTests
{
    private static string CreateSamplePdf(string title, int pageCount = 2)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"agdubugdu_test_{Guid.NewGuid():N}.pdf");
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= pageCount; i++)
        {
            sb.AppendLine($"This is page {i} content for sample document.");
            sb.AppendLine($"Testing PDF parsing, text extraction and editing for AgduBugdu IDE.");
            if (i < pageCount)
            {
                // Add enough content to trigger pagination
                for (int j = 0; j < 50; j++)
                {
                    sb.AppendLine($"Filler text line {j} on page {i}");
                }
            }
        }

        PdfEngine.CreateNewDocument(tempPath, title, sb.ToString());
        return tempPath;
    }

    [Fact]
    public void PdfEngine_CreateNewDocument_And_LoadDocument()
    {
        var pdfPath = CreateSamplePdf("AgduBugdu Manual", 1);
        try
        {
            var doc = PdfEngine.LoadDocument(pdfPath);
            Assert.NotNull(doc);
            Assert.True(doc.Pages.Count >= 1);
            Assert.Equal("AgduBugdu Manual", doc.Metadata.Title);
            Assert.Contains("This is page 1 content", doc.Pages[0].Text);
            Assert.True(doc.Pages[0].Width > 0);
            Assert.True(doc.Pages[0].Height > 0);
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    [Fact]
    public void PdfEngine_Search_FindsMatchesAcrossPages()
    {
        var pdfPath = CreateSamplePdf("Search Test Doc", 2);
        try
        {
            var doc = PdfEngine.LoadDocument(pdfPath);
            var results = PdfEngine.Search(doc, "AgduBugdu", matchCase: false);

            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.Snippet.Contains("AgduBugdu"));
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    [Fact]
    public void PdfEngine_RotatePage_RotatesSpecifiedPage()
    {
        var pdfPath = CreateSamplePdf("Rotation Test", 1);
        try
        {
            PdfEngine.RotatePage(pdfPath, pdfPath, 1, 90);
            var doc = PdfEngine.LoadDocument(pdfPath);
            Assert.Equal(90, doc.Pages[0].RotationDegrees);

            PdfEngine.RotatePage(pdfPath, pdfPath, 1, 90);
            var doc2 = PdfEngine.LoadDocument(pdfPath);
            Assert.Equal(180, doc2.Pages[0].RotationDegrees);
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    [Fact]
    public void PdfEngine_UpdateMetadata_ModifiesDocumentInfo()
    {
        var pdfPath = CreateSamplePdf("Original Title", 1);
        try
        {
            PdfEngine.UpdateMetadata(pdfPath, pdfPath, info =>
            {
                info.Title = "Updated Title";
                info.Author = "Bugdu Developer";
                info.Subject = "Extensibility";
                info.Keywords = "PDF, Editing, Avalonia";
            });

            var doc = PdfEngine.LoadDocument(pdfPath);
            Assert.Equal("Updated Title", doc.Metadata.Title);
            Assert.Equal("Bugdu Developer", doc.Metadata.Author);
            Assert.Equal("Extensibility", doc.Metadata.Subject);
            Assert.Equal("PDF, Editing, Avalonia", doc.Metadata.Keywords);
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    [Fact]
    public void PdfEngine_AddWatermark_DrawsOnPages()
    {
        var pdfPath = CreateSamplePdf("Watermark Test", 1);
        try
        {
            PdfEngine.AddWatermark(pdfPath, pdfPath, "DRAFT", WatermarkPosition.CenterDiagonal, 1);
            var doc = PdfEngine.LoadDocument(pdfPath);
            Assert.NotNull(doc);
            Assert.Single(doc.Pages);
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    [Fact]
    public void PdfEngine_MergeDocuments_CombinesMultiplePdfs()
    {
        var pdf1 = CreateSamplePdf("Doc One", 1);
        var pdf2 = CreateSamplePdf("Doc Two", 1);
        var mergedPath = Path.Combine(Path.GetTempPath(), $"merged_{Guid.NewGuid():N}.pdf");

        try
        {
            var doc1 = PdfEngine.LoadDocument(pdf1);
            var doc2 = PdfEngine.LoadDocument(pdf2);
            int expectedPages = doc1.Pages.Count + doc2.Pages.Count;

            PdfEngine.MergeDocuments(new[] { pdf1, pdf2 }, mergedPath);

            var mergedDoc = PdfEngine.LoadDocument(mergedPath);
            Assert.Equal(expectedPages, mergedDoc.Pages.Count);
        }
        finally
        {
            if (File.Exists(pdf1)) File.Delete(pdf1);
            if (File.Exists(pdf2)) File.Delete(pdf2);
            if (File.Exists(mergedPath)) File.Delete(mergedPath);
        }
    }

    [Fact]
    public void PdfEngine_ExtractPages_CreatesNewPdf()
    {
        var pdfPath = CreateSamplePdf("Multi-page Doc", 2);
        var extractedPath = Path.Combine(Path.GetTempPath(), $"extracted_{Guid.NewGuid():N}.pdf");

        try
        {
            PdfEngine.ExtractPages(pdfPath, extractedPath, 1, 1);
            var extractedDoc = PdfEngine.LoadDocument(extractedPath);
            Assert.Single(extractedDoc.Pages);
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
            if (File.Exists(extractedPath)) File.Delete(extractedPath);
        }
    }

    [Fact]
    public void PdfEngine_DeletePage_RemovesPage()
    {
        var pdfPath = CreateSamplePdf("Three Page Doc", 3);
        try
        {
            var initialDoc = PdfEngine.LoadDocument(pdfPath);
            int initialCount = initialDoc.Pages.Count;
            Assert.True(initialCount >= 2);

            PdfEngine.DeletePage(pdfPath, pdfPath, 1);
            var updatedDoc = PdfEngine.LoadDocument(pdfPath);
            Assert.Equal(initialCount - 1, updatedDoc.Pages.Count);
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    [Fact]
    public void PdfLiveViewModel_Navigation_And_FormattedView()
    {
        var pdfPath = CreateSamplePdf("ViewModel Test Doc", 2);
        try
        {
            var vm = new PdfLiveViewModel();
            vm.LoadFile(pdfPath);

            Assert.Equal(1, vm.CurrentPageNumber);
            Assert.True(vm.TotalPages >= 1);
            Assert.NotNull(vm.CurrentPage);

            vm.NextPage();
            Assert.Equal(Math.Min(2, vm.TotalPages), vm.CurrentPageNumber);

            vm.FirstPage();
            Assert.Equal(1, vm.CurrentPageNumber);

            var formatted = vm.ToFormattedText();
            Assert.Contains("PDF VIEWER & EDITOR", formatted);
            Assert.Contains("ViewModel Test Doc", formatted);
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    [Fact]
    public void PdfLiveExtension_Lifecycle_And_CommandRegistration()
    {
        var cmdRegistry = new CommandRegistry();
        var toolRegistry = new ToolWindowRegistry();
        var editorService = new AppEditorService((f, l, c) => { }, () => null, (n, c) => { });
        var workspaceService = new AppWorkspaceService(f => { });
        var context = new ExtensionContext(cmdRegistry, toolRegistry, editorService, workspaceService);

        var extension = new PdfLiveExtension();
        extension.Initialize(context);

        Assert.Equal("agdubugdu.plugin.pdflive", extension.Id);
        Assert.Equal("Live PDF Viewer & Editor", extension.Name);
        Assert.NotNull(extension.ViewModel);

        // Verify commands registered
        Assert.True(cmdRegistry.GetRegisteredCommands().ContainsKey("pdf.view"));
        Assert.True(cmdRegistry.GetRegisteredCommands().ContainsKey("pdf.nextPage"));
        Assert.True(cmdRegistry.GetRegisteredCommands().ContainsKey("pdf.prevPage"));
        Assert.True(cmdRegistry.GetRegisteredCommands().ContainsKey("pdf.rotate"));
        Assert.True(cmdRegistry.GetRegisteredCommands().ContainsKey("pdf.deletePage"));
        Assert.True(cmdRegistry.GetRegisteredCommands().ContainsKey("pdf.watermark"));

        // Verify tool window registered
        Assert.True(toolRegistry.GetRegisteredTools().ContainsKey("pdf.live.tool"));
    }
}
