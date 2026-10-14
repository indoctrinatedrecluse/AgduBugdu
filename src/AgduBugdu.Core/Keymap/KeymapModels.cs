using System;
using System.Collections.Generic;
using System.Linq;

namespace AgduBugdu.Core.Keymap;

public class CommandDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DefaultGesture { get; set; } = string.Empty;
    public string CurrentGesture { get; set; } = string.Empty;
    public bool HasConflict { get; set; }
    public string ConflictCommandName { get; set; } = string.Empty;

    public bool IsModifiedFromDefault =>
        !string.Equals(DefaultGesture?.Trim(), CurrentGesture?.Trim(), StringComparison.OrdinalIgnoreCase);

    public void ResetToDefault()
    {
        CurrentGesture = DefaultGesture;
    }
}

public class KeymapExportModel
{
    public string Application { get; set; } = "AgduBugdu";
    public string SchemaVersion { get; set; } = "1.0";
    public DateTime ExportedUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Bindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class KeymapHelper
{
    public static void UpdateConflicts(IEnumerable<CommandDefinition> commands)
    {
        var list = commands.ToList();
        
        // Clear previous conflict states
        foreach (var cmd in list)
        {
            cmd.HasConflict = false;
            cmd.ConflictCommandName = string.Empty;
        }

        // Group by normalized gesture (excluding empty/cleared gestures)
        var groups = list
            .Where(c => !string.IsNullOrWhiteSpace(c.CurrentGesture))
            .GroupBy(c => NormalizeGesture(c.CurrentGesture), StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var conflictingList = group.ToList();
            if (conflictingList.Count > 1)
            {
                for (int i = 0; i < conflictingList.Count; i++)
                {
                    conflictingList[i].HasConflict = true;
                    var others = conflictingList.Where((_, idx) => idx != i).Select(c => c.Title);
                    conflictingList[i].ConflictCommandName = string.Join(", ", others);
                }
            }
        }
    }

    public static string NormalizeGesture(string? gesture)
    {
        if (string.IsNullOrWhiteSpace(gesture))
            return string.Empty;

        return gesture.Trim().Replace(" ", "").ToUpperInvariant();
    }
}
