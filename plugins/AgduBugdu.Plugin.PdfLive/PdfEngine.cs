using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace AgduBugdu.Plugin.PdfLive;

public static class PdfEngine
{
    public static PdfDocumentData LoadDocument(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"PDF file not found: {filePath}", filePath);
        }

        var fileInfo = new FileInfo(filePath);
        var data = new PdfDocumentData
        {
            FilePath = filePath,
            Metadata = new PdfMetadata
            {
                FileSizeBytes = fileInfo.Length
            }
        };

        using (var document = UglyToad.PdfPig.PdfDocument.Open(filePath))
        {
            data.Metadata.PageCount = document.NumberOfPages;
            data.Metadata.PdfVersion = document.Version.ToString();

            if (document.Information != null)
            {
                data.Metadata.Title = document.Information.Title ?? string.Empty;
                data.Metadata.Author = document.Information.Author ?? string.Empty;
                data.Metadata.Subject = document.Information.Subject ?? string.Empty;
                data.Metadata.Keywords = document.Information.Keywords ?? string.Empty;
                data.Metadata.Creator = document.Information.Creator ?? string.Empty;
                data.Metadata.Producer = document.Information.Producer ?? string.Empty;
            }

            // Extract Bookmarks if available
            try
            {
                if (document.TryGetBookmarks(out var bookmarks))
                {
                    ExtractBookmarksRecursive(bookmarks.GetNodes(), data.Bookmarks, 0);
                }
            }
            catch
            {
                // Bookmarks extraction fallback
            }

            // Extract Pages
            foreach (var page in document.GetPages())
            {
                string rawText;
                try
                {
                    rawText = ContentOrderTextExtractor.GetText(page);
                }
                catch
                {
                    rawText = page.Text ?? string.Empty;
                }

                var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                    .Select(l => l.TrimEnd())
                    .ToList();

                var words = rawText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                var pageModel = new PdfPageModel
                {
                    PageNumber = page.Number,
                    Width = Math.Round(page.Width, 1),
                    Height = Math.Round(page.Height, 1),
                    RotationDegrees = (int)page.Rotation.Value,
                    Text = rawText,
                    WordCount = words.Length,
                    CharacterCount = rawText.Length,
                    Lines = lines
                };

                data.Pages.Add(pageModel);
            }
        }

        return data;
    }

    private static void ExtractBookmarksRecursive(IEnumerable<UglyToad.PdfPig.Outline.BookmarkNode> nodes, List<PdfBookmarkItem> destination, int level)
    {
        if (nodes == null) return;

        foreach (var node in nodes)
        {
            int pageNum = 1;
            if (node is UglyToad.PdfPig.Outline.DocumentBookmarkNode docNode)
            {
                pageNum = docNode.PageNumber;
            }

            destination.Add(new PdfBookmarkItem
            {
                Title = node.Title,
                PageNumber = pageNum,
                Level = level
            });

            if (node.Children != null && node.Children.Count > 0)
            {
                ExtractBookmarksRecursive(node.Children, destination, level + 1);
            }
        }
    }

    public static List<PdfSearchResultItem> Search(PdfDocumentData document, string query, bool matchCase)
    {
        var results = new List<PdfSearchResultItem>();
        if (document == null || string.IsNullOrWhiteSpace(query))
            return results;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        foreach (var page in document.Pages)
        {
            for (int lineIdx = 0; lineIdx < page.Lines.Count; lineIdx++)
            {
                var line = page.Lines[lineIdx];
                int foundIdx = line.IndexOf(query, comparison);
                while (foundIdx >= 0)
                {
                    int snippetStart = Math.Max(0, foundIdx - 30);
                    int snippetLen = Math.Min(line.Length - snippetStart, query.Length + 60);
                    string snippet = line.Substring(snippetStart, snippetLen);

                    results.Add(new PdfSearchResultItem
                    {
                        PageNumber = page.PageNumber,
                        LineNumber = lineIdx + 1,
                        Snippet = snippet,
                        MatchIndexInLine = foundIdx
                    });

                    foundIdx = line.IndexOf(query, foundIdx + query.Length, comparison);
                }
            }
        }

        return results;
    }

    public static void RotatePage(string inputPath, string outputPath, int pageNumber, int degrees)
    {
        using var doc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);
        if (pageNumber < 1 || pageNumber > doc.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), $"Invalid page {pageNumber}. Document has {doc.PageCount} pages.");
        }

        var page = doc.Pages[pageNumber - 1];
        page.Rotate = (page.Rotate + degrees) % 360;
        doc.Save(outputPath);
    }

    public static void DeletePage(string inputPath, string outputPath, int pageNumber)
    {
        using var doc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);
        if (doc.PageCount <= 1)
        {
            throw new InvalidOperationException("Cannot delete the only page in a PDF document.");
        }

        if (pageNumber < 1 || pageNumber > doc.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), $"Invalid page {pageNumber}. Document has {doc.PageCount} pages.");
        }

        doc.Pages.RemoveAt(pageNumber - 1);
        doc.Save(outputPath);
    }

    public static void ExtractPages(string inputPath, string outputPath, int startPage, int endPage)
    {
        using var inputDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
        int total = inputDoc.PageCount;
        startPage = Math.Clamp(startPage, 1, total);
        endPage = Math.Clamp(endPage, startPage, total);

        using var outputDoc = new PdfSharpCore.Pdf.PdfDocument();
        for (int i = startPage - 1; i < endPage; i++)
        {
            outputDoc.AddPage(inputDoc.Pages[i]);
        }

        outputDoc.Save(outputPath);
    }

    public static void MergeDocuments(IEnumerable<string> sourceFiles, string outputPath)
    {
        using var outputDoc = new PdfSharpCore.Pdf.PdfDocument();

        foreach (var file in sourceFiles)
        {
            if (!File.Exists(file)) continue;

            using var inputDoc = PdfReader.Open(file, PdfDocumentOpenMode.Import);
            foreach (var page in inputDoc.Pages)
            {
                outputDoc.AddPage(page);
            }
        }

        outputDoc.Save(outputPath);
    }

    public static void UpdateMetadata(string inputPath, string outputPath, Action<PdfDocumentInformation> updater)
    {
        using var doc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);
        updater(doc.Info);
        doc.Save(outputPath);
    }

    public static void AddWatermark(string inputPath, string outputPath, string watermarkText, WatermarkPosition position, int targetPage = -1)
    {
        using var doc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);
        var font = new XFont("Helvetica", 22, XFontStyle.Bold);
        var brush = new XSolidBrush(XColor.FromArgb(140, 220, 38, 38)); // Semi-transparent red

        for (int i = 0; i < doc.PageCount; i++)
        {
            int pageNum = i + 1;
            if (targetPage > 0 && targetPage != pageNum)
                continue;

            var page = doc.Pages[i];
            using var gfx = XGraphics.FromPdfPage(page);

            switch (position)
            {
                case WatermarkPosition.Header:
                {
                    var rect = new XRect(0, 20, page.Width, 30);
                    gfx.DrawString(watermarkText, font, brush, rect, XStringFormats.TopCenter);
                    break;
                }
                case WatermarkPosition.Footer:
                {
                    var rect = new XRect(0, page.Height - 40, page.Width, 30);
                    gfx.DrawString(watermarkText, font, brush, rect, XStringFormats.TopCenter);
                    break;
                }
                case WatermarkPosition.TopRight:
                {
                    var rect = new XRect(page.Width - 250, 20, 230, 30);
                    gfx.DrawString(watermarkText, font, brush, rect, XStringFormats.TopRight);
                    break;
                }
                case WatermarkPosition.CenterDiagonal:
                default:
                {
                    var state = gfx.Save();
                    gfx.TranslateTransform(page.Width / 2, page.Height / 2);
                    gfx.RotateTransform(-45);
                    var bigFont = new XFont("Helvetica", 48, XFontStyle.Bold);
                    var faintBrush = new XSolidBrush(XColor.FromArgb(60, 220, 38, 38));
                    gfx.DrawString(watermarkText, bigFont, faintBrush, new XPoint(0, 0), XStringFormats.Center);
                    gfx.Restore(state);
                    break;
                }
            }
        }

        doc.Save(outputPath);
    }

    public static void InsertBlankPage(string inputPath, string outputPath, int insertAfterPage)
    {
        using var doc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);
        var newPage = new PdfPage
        {
            Width = XUnit.FromPoint(595.28), // A4 default
            Height = XUnit.FromPoint(841.89)
        };

        int idx = Math.Clamp(insertAfterPage, 0, doc.PageCount);
        doc.Pages.Insert(idx, newPage);
        doc.Save(outputPath);
    }

    public static void CreateNewDocument(string outputPath, string title, string textContent)
    {
        using var doc = new PdfSharpCore.Pdf.PdfDocument();
        doc.Info.Title = title;
        doc.Info.Author = "AgduBugdu IDE";

        var page = doc.AddPage();
        page.Width = XUnit.FromPoint(595.28);
        page.Height = XUnit.FromPoint(841.89);

        using var gfx = XGraphics.FromPdfPage(page);
        var titleFont = new XFont("Helvetica", 18, XFontStyle.Bold);
        var bodyFont = new XFont("Helvetica", 11, XFontStyle.Regular);
        var textBrush = new XSolidBrush(XColor.FromArgb(255, 30, 30, 30));

        gfx.DrawString(title, titleFont, textBrush, new XPoint(40, 50));
        gfx.DrawLine(new XPen(XColor.FromArgb(255, 180, 180, 180), 1), 40, 60, page.Width - 40, 60);

        var lines = (textContent ?? string.Empty).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        double y = 80;
        foreach (var line in lines)
        {
            if (y > page.Height - 40)
            {
                page = doc.AddPage();
                page.Width = XUnit.FromPoint(595.28);
                page.Height = XUnit.FromPoint(841.89);
                y = 50;
            }

            gfx.DrawString(line, bodyFont, textBrush, new XPoint(40, y));
            y += 16;
        }

        doc.Save(outputPath);
    }

    public static string ToFormattedSummary(PdfDocumentData doc, int selectedPageNumber)
    {
        if (doc == null || doc.Pages.Count == 0)
        {
            return "No PDF document loaded.";
        }

        var sb = new StringBuilder();
        var fileName = Path.GetFileName(doc.FilePath);
        sb.AppendLine($"# 📄 PDF VIEWER & EDITOR: {fileName}");
        sb.AppendLine($"Total Pages: {doc.Metadata.PageCount} | Size: {doc.Metadata.FileSizeFormatted} | PDF Version: {doc.Metadata.PdfVersion}");
        
        // Metadata banner
        if (!string.IsNullOrEmpty(doc.Metadata.Title) || !string.IsNullOrEmpty(doc.Metadata.Author))
        {
            sb.AppendLine();
            sb.AppendLine("## ℹ️ Document Metadata");
            if (!string.IsNullOrEmpty(doc.Metadata.Title)) sb.AppendLine($"- **Title:** {doc.Metadata.Title}");
            if (!string.IsNullOrEmpty(doc.Metadata.Author)) sb.AppendLine($"- **Author:** {doc.Metadata.Author}");
            if (!string.IsNullOrEmpty(doc.Metadata.Subject)) sb.AppendLine($"- **Subject:** {doc.Metadata.Subject}");
            if (!string.IsNullOrEmpty(doc.Metadata.Keywords)) sb.AppendLine($"- **Keywords:** {doc.Metadata.Keywords}");
        }

        // Bookmarks / Outline if present
        if (doc.Bookmarks.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## 📑 Table of Contents");
            foreach (var bm in doc.Bookmarks)
            {
                string indent = new string(' ', bm.Level * 2);
                sb.AppendLine($"{indent}- [{bm.Title}] (Page {bm.PageNumber})");
            }
        }

        // Active page display
        int activePageIdx = Math.Clamp(selectedPageNumber - 1, 0, doc.Pages.Count - 1);
        var page = doc.Pages[activePageIdx];

        sb.AppendLine();
        sb.AppendLine(new string('=', 70));
        sb.AppendLine($"### [PAGE {page.PageNumber} OF {doc.Metadata.PageCount}] — Dimensions: {page.Width} x {page.Height} pt | Rotation: {page.RotationDegrees}° | Words: {page.WordCount}");
        sb.AppendLine(new string('=', 70));
        sb.AppendLine();

        if (string.IsNullOrWhiteSpace(page.Text))
        {
            sb.AppendLine("*(Page contains no selectable text or is an image/scanned page)*");
        }
        else
        {
            sb.AppendLine(page.Text);
        }

        return sb.ToString();
    }
}
