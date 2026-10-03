using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Extensibility.Services;

public class DefaultLanguageService : ILanguageService
{
    private readonly ConcurrentDictionary<string, LanguageDefinition> _languages = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Snippet> _snippets = new();
    private readonly object _snippetsLock = new();

    public event EventHandler<LanguageDefinition>? LanguageRegistered;
    public event EventHandler<Snippet>? SnippetRegistered;

    public void RegisterLanguage(LanguageDefinition language)
    {
        ArgumentNullException.ThrowIfNull(language);
        _languages[language.Id] = language;
        LanguageRegistered?.Invoke(this, language);
    }

    public IReadOnlyList<LanguageDefinition> GetLanguages()
    {
        return _languages.Values.OrderBy(l => l.Name).ToList();
    }

    public LanguageDefinition? GetLanguageById(string languageId)
    {
        if (string.IsNullOrWhiteSpace(languageId)) return null;
        return _languages.TryGetValue(languageId, out var def) ? def : null;
    }

    public LanguageDefinition? GetLanguageForFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext)) return null;

        return _languages.Values.FirstOrDefault(l =>
            l.FileExtensions.Any(e => string.Equals(e.StartsWith('.') ? e : $".{e}", ext, StringComparison.OrdinalIgnoreCase)));
    }

    public void RegisterSnippet(Snippet snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        lock (_snippetsLock)
        {
            _snippets.RemoveAll(s => s.Id == snippet.Id);
            _snippets.Add(snippet);
        }
        SnippetRegistered?.Invoke(this, snippet);
    }

    public IReadOnlyList<Snippet> GetSnippets(string? languageId = null)
    {
        lock (_snippetsLock)
        {
            if (string.IsNullOrWhiteSpace(languageId))
            {
                return _snippets.OrderBy(s => s.LanguageId).ThenBy(s => s.Prefix).ToList();
            }

            return _snippets
                .Where(s => string.Equals(s.LanguageId, languageId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Prefix)
                .ToList();
        }
    }

    public Snippet? GetSnippetById(string snippetId)
    {
        if (string.IsNullOrWhiteSpace(snippetId)) return null;
        lock (_snippetsLock)
        {
            return _snippets.FirstOrDefault(s => string.Equals(s.Id, snippetId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
