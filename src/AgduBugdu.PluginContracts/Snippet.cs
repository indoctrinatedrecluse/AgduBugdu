using System.Collections.Generic;

namespace AgduBugdu.PluginContracts;

/// <summary>
/// Code snippet with trigger prefix, description, template body, and language association.
/// </summary>
public record Snippet(
    string Id,
    string Name,
    string Prefix,
    string Description,
    string Body,
    string LanguageId,
    IReadOnlyList<string>? Tags = null
);
