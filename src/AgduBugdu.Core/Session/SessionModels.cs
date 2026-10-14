using System;
using System.Collections.Generic;

namespace AgduBugdu.Core.Session;

/// <summary>
/// Represents persisted workspace and editor layout state for session restoration.
/// </summary>
public class WorkspaceSessionData
{
    public string? WorkspaceFolderPath { get; set; }
    public List<DocumentSessionData> PrimaryDocuments { get; set; } = new();
    public List<DocumentSessionData> SecondaryDocuments { get; set; } = new();
    public string? ActiveDocumentPath { get; set; }
    public bool IsSplitActive { get; set; }
    public string SplitOrientation { get; set; } = "Horizontal";
    public double PrimaryPaneProportion { get; set; } = 0.5;
    public double SecondaryPaneProportion { get; set; } = 0.5;
    public string? ActiveLeftToolId { get; set; }
    public string? ActiveBottomToolId { get; set; }
    public bool IsBottomPaneMinimized { get; set; }
    public DateTime LastSavedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// State for an individual document tab in a session.
/// </summary>
public class DocumentSessionData
{
    public string FilePath { get; set; } = string.Empty;
    public int CaretLine { get; set; } = 1;
    public int CaretColumn { get; set; } = 1;
    public double ScrollVerticalOffset { get; set; }
    public bool IsPinned { get; set; }
}
