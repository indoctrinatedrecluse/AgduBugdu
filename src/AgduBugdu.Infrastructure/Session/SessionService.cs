using System;
using System.IO;
using System.Text.Json;
using AgduBugdu.Core.Session;

namespace AgduBugdu.Infrastructure.Session;

public class SessionService : ISessionService
{
    private readonly string _sessionFilePath;
    private readonly object _lock = new();

    public event EventHandler<WorkspaceSessionData>? SessionSaved;

    public SessionService(string? customFilePath = null)
    {
        if (!string.IsNullOrEmpty(customFilePath))
        {
            _sessionFilePath = customFilePath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "AgduBugdu");
            Directory.CreateDirectory(folder);
            _sessionFilePath = Path.Combine(folder, "session.json");
        }
    }

    public WorkspaceSessionData? LoadSession()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_sessionFilePath))
                    return null;

                string json = File.ReadAllText(_sessionFilePath);
                return JsonSerializer.Deserialize<WorkspaceSessionData>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }
    }

    public void SaveSession(WorkspaceSessionData session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_lock)
        {
            try
            {
                session.LastSavedUtc = DateTime.UtcNow;
                string dir = Path.GetDirectoryName(_sessionFilePath)!;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(session, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Atomic write via temporary file
                string tempFile = _sessionFilePath + ".tmp";
                File.WriteAllText(tempFile, json);
                File.Move(tempFile, _sessionFilePath, overwrite: true);

                SessionSaved?.Invoke(this, session);
            }
            catch
            {
                // Non-fatal if session cannot be saved
            }
        }
    }

    public void ClearSession()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_sessionFilePath))
                {
                    File.Delete(_sessionFilePath);
                }
            }
            catch { }
        }
    }
}
