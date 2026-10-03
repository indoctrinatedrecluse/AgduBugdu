using System;

namespace AgduBugdu.PluginContracts;

public class DocumentEventArgs : EventArgs
{
    public string FilePath { get; }
    public DocumentEventArgs(string filePath) => FilePath = filePath;
}

public class LineNavigationEventArgs : EventArgs
{
    public string FilePath { get; }
    public int Line { get; }
    public int Column { get; }

    public LineNavigationEventArgs(string filePath, int line, int column = 1)
    {
        FilePath = filePath;
        Line = line;
        Column = column;
    }
}

public interface IEditorService
{
    event EventHandler<DocumentEventArgs>? DocumentOpened;
    event EventHandler<DocumentEventArgs>? DocumentSaved;
    event EventHandler<DocumentEventArgs>? DocumentClosed;
    event EventHandler<LineNavigationEventArgs>? LineNavigationRequested;

    void OpenFile(string filePath);
    void OpenFile(string filePath, int line, int column = 1);
    string? ActiveDocumentPath { get; }
    int? ActiveLine { get; }
    int? ActiveColumn { get; }

    /// <summary>
    /// Inserts text into the active document at the current caret position.
    /// </summary>
    void InsertText(string text);

    /// <summary>
    /// Gets the full text content of the currently active document buffer.
    /// </summary>
    string? GetActiveDocumentText();

    /// <summary>
    /// Sets or replaces the full text content of the currently active document buffer.
    /// </summary>
    void SetActiveDocumentText(string text);

    /// <summary>
    /// Creates and opens a new untitled or named document in the editor dock.
    /// </summary>
    void NewDocument(string? defaultFileName = null, string? initialContent = null);
}
