using System;
using System.Collections.Generic;

namespace AgduBugdu.PluginContracts;

/// <summary>
/// Service for registering and querying language definitions, syntax configurations, and snippets.
/// </summary>
public interface ILanguageService
{
    /// <summary>
    /// Event fired when a new language definition is registered.
    /// </summary>
    event EventHandler<LanguageDefinition>? LanguageRegistered;

    /// <summary>
    /// Event fired when a new snippet is registered.
    /// </summary>
    event EventHandler<Snippet>? SnippetRegistered;

    /// <summary>
    /// Register a language definition with its grammar scope, comment symbols, and file extensions.
    /// </summary>
    void RegisterLanguage(LanguageDefinition language);

    /// <summary>
    /// Get all registered language definitions.
    /// </summary>
    IReadOnlyList<LanguageDefinition> GetLanguages();

    /// <summary>
    /// Get a language definition by its identifier (e.g., "cpp", "csharp", "python").
    /// </summary>
    LanguageDefinition? GetLanguageById(string languageId);

    /// <summary>
    /// Find the language definition matching the file extension or file path.
    /// </summary>
    LanguageDefinition? GetLanguageForFile(string filePath);

    /// <summary>
    /// Register a code snippet.
    /// </summary>
    void RegisterSnippet(Snippet snippet);

    /// <summary>
    /// Get registered snippets, optionally filtered by language identifier.
    /// </summary>
    IReadOnlyList<Snippet> GetSnippets(string? languageId = null);

    /// <summary>
    /// Get a specific snippet by unique identifier.
    /// </summary>
    Snippet? GetSnippetById(string snippetId);
}
