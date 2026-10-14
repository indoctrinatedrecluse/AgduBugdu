using System;
using System.Collections.Generic;

namespace AgduBugdu.Plugin.PdfLive;

public enum WatermarkPosition
{
    Header,
    Footer,
    CenterDiagonal,
    TopRight
}

public class PdfMetadata
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public string Producer { get; set; } = string.Empty;
    public string PdfVersion { get; set; } = "1.7";
    public int PageCount { get; set; }
    public long FileSizeBytes { get; set; }
    public string FileSizeFormatted => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
        _ => $"{FileSizeBytes / (1024.0 * 1024.0):F2} MB"
    };
}

public class PdfBookmarkItem
{
    public string Title { get; set; } = string.Empty;
    public int PageNumber { get; set; } = 1;
    public int Level { get; set; } = 0;
}

public class PdfPageModel
{
    public int PageNumber { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int RotationDegrees { get; set; }
    public string Text { get; set; } = string.Empty;
    public int WordCount { get; set; }
    public int CharacterCount { get; set; }
    public List<string> Lines { get; set; } = new();
}

public class PdfSearchResultItem
{
    public int PageNumber { get; set; }
    public int LineNumber { get; set; }
    public string Snippet { get; set; } = string.Empty;
    public int MatchIndexInLine { get; set; }
}

public class PdfDocumentData
{
    public string FilePath { get; set; } = string.Empty;
    public PdfMetadata Metadata { get; set; } = new();
    public List<PdfPageModel> Pages { get; set; } = new();
    public List<PdfBookmarkItem> Bookmarks { get; set; } = new();
}
