using System;

namespace AgduBugdu.PluginContracts;

public class WorkspaceChangedEventArgs : EventArgs
{
    public string? NewWorkspacePath { get; }
    public WorkspaceChangedEventArgs(string? newWorkspacePath) => NewWorkspacePath = newWorkspacePath;
}

public interface IWorkspaceService
{
    string? CurrentDirectory { get; }
    event EventHandler<WorkspaceChangedEventArgs>? WorkspaceChanged;
    void OpenFolder(string folderPath);
}
