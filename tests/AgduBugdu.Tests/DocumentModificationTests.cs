using System;
using System.IO;
using AgduBugdu.App.Docking;
using AgduBugdu.App.ViewModels;
using AgduBugdu.App.ViewModels.Documents;
using Xunit;

namespace AgduBugdu.Tests;

public class DocumentModificationTests
{
    [Fact]
    public void NewDocument_HasNoAsteriskInitially()
    {
        var doc = new EditorDocumentViewModel
        {
            FileName = "Untitled.txt"
        };

        doc.CheckModified();
        Assert.False(doc.IsModified);
        Assert.Equal("Untitled.txt", doc.Title);
    }

    [Fact]
    public void WelcomeDocument_HasNoAsteriskInitially()
    {
        var factory = new MainDockFactory();
        var layout = factory.CreateLayout();

        var welcomeDoc = factory.DocumentDock?.VisibleDockables?[0] as EditorDocumentViewModel;
        Assert.NotNull(welcomeDoc);
        Assert.Equal("Welcome.txt", welcomeDoc.FileName);

        welcomeDoc.CheckModified();
        Assert.False(welcomeDoc.IsModified);
        Assert.Equal("Welcome.txt", welcomeDoc.Title);
    }

    [Fact]
    public void DocumentFromFile_HasNoAsteriskInitially()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "Hello World Content");
            var doc = EditorDocumentViewModel.FromFile(tempFile);

            doc.CheckModified();
            Assert.False(doc.IsModified);
            Assert.Equal(Path.GetFileName(tempFile), doc.Title);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ModifiedDocument_ShowsAsterisk_AndClearsOnSaveOrRevert()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "Line 1\nLine 2");
            var doc = EditorDocumentViewModel.FromFile(tempFile);

            // 1. Initial state
            Assert.False(doc.IsModified);
            Assert.DoesNotContain("*", doc.Title);

            // 2. Modify document
            doc.TextDocument.Insert(0, "Modified: ");
            doc.CheckModified();
            Assert.True(doc.IsModified);
            Assert.EndsWith("*", doc.Title);

            // 3. Save document
            doc.Save();
            Assert.False(doc.IsModified);
            Assert.DoesNotContain("*", doc.Title);

            // 4. Modify again
            doc.TextDocument.Insert(doc.TextDocument.TextLength, "\nNew Line");
            doc.CheckModified();
            Assert.True(doc.IsModified);
            Assert.EndsWith("*", doc.Title);

            // 5. Revert modification back to saved content
            doc.TextDocument.Text = File.ReadAllText(tempFile);
            doc.CheckModified();
            Assert.False(doc.IsModified);
            Assert.DoesNotContain("*", doc.Title);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
