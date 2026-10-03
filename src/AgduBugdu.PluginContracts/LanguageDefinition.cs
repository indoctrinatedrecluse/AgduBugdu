using System.Collections.Generic;

namespace AgduBugdu.PluginContracts;

/// <summary>
/// Metadata describing a supported programming language, its grammar scope, file extensions, and syntax rules.
/// </summary>
public record LanguageDefinition(
    string Id,
    string Name,
    IReadOnlyList<string> FileExtensions,
    string GrammarScope,
    string LineCommentPrefix,
    string? BlockCommentStart = null,
    string? BlockCommentEnd = null
);
