using System;

namespace AgduBugdu.PluginContracts;

public class DocumentEventArgs : EventArgs
{
    public string FilePath { get; }
    public DocumentEventArgs(string filePath) => FilePath = filePath;
}

public interface IEditorService
{
    event EventHandler<DocumentEventArgs>? DocumentOpened;
    event EventHandler<DocumentEventArgs>? DocumentSaved;
    event EventHandler<DocumentEventArgs>? DocumentClosed;

    void OpenFile(string filePath);
    string? ActiveDocumentPath { get; }
}
