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
}
