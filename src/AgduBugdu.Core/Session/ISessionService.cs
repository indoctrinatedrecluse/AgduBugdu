using System;

namespace AgduBugdu.Core.Session;

public interface ISessionService
{
    event EventHandler<WorkspaceSessionData>? SessionSaved;
    WorkspaceSessionData? LoadSession();
    void SaveSession(WorkspaceSessionData session);
    void ClearSession();
}
