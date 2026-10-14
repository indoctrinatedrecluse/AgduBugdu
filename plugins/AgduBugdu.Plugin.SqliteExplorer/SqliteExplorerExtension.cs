using System;
using System.IO;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Plugin.SqliteExplorer;

public class SqliteExplorerExtension : IExtension
{
    private IExtensionContext? _context;
    private SqliteExplorerViewModel? _viewModel;

    public string Id => "agdubugdu.plugin.sqliteexplorer";
    public string Name => "SQLite Database Explorer";
    public string Version => "1.0.0";

    public SqliteExplorerViewModel? ViewModel => _viewModel;

    public void Initialize(IExtensionContext context)
    {
        _context = context;
        _viewModel = new SqliteExplorerViewModel();

        // 1. Register commands
        _context.Commands.RegisterCommand(
            "sqlite.query",
            "SQLite: Run SQL Query",
            async () =>
            {
                if (_viewModel != null)
                {
                    // If editor has SQL query selected or open
                    string? text = _context.EditorService.GetActiveDocumentText();
                    string? path = _context.EditorService.ActiveDocumentPath;

                    if (!string.IsNullOrEmpty(text) && path?.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        var result = await _viewModel.ExecuteQueryAsync(text);
                        _context.Log($"[SQLite] Executed SQL query: {result.StatusBadge}");
                        return;
                    }

                    var defaultResult = await _viewModel.ExecuteQueryAsync();
                    _context.Log($"[SQLite] Query: {defaultResult.StatusBadge}");
                }
            },
            "F5"
        );

        _context.Commands.RegisterCommand(
            "sqlite.exportMarkdown",
            "SQLite: Export Query Results as Markdown Table",
            () =>
            {
                if (_viewModel != null)
                {
                    string md = _viewModel.FormatResultAsMarkdownTable();
                    _context.EditorService.NewDocument("QueryResults.md", md);
                    _context.Log("[SQLite] Exported query results to Markdown file.");
                }
                return Task.CompletedTask;
            }
        );

        // 2. Register Tool Window
        _context.ToolWindows.RegisterToolWindow(
            "sqlite.explorer.tool",
            "SQLite Explorer",
            () => _viewModel,
            vm => _viewModel,
            ToolDockPosition.Left
        );

        // 3. Register SQL language & snippets
        _context.Languages.RegisterLanguage(new LanguageDefinition(
            "sql",
            "SQL Query",
            new[] { ".sql", ".sqlite", ".sqlite3", ".db" },
            "source.sql",
            "--",
            "/*",
            "*/"
        ));

        _context.Languages.RegisterSnippet(new Snippet(
            "sql.select",
            "Select Query",
            "select",
            "SELECT * FROM table query",
            "SELECT ${*} FROM ${tableName}\nWHERE ${1=1}\nORDER BY ${id} ASC\nLIMIT 100;",
            "sql"
        ));

        _context.Languages.RegisterSnippet(new Snippet(
            "sql.create",
            "Create Table",
            "create",
            "CREATE TABLE DDL statement",
            "CREATE TABLE IF NOT EXISTS ${TableName} (\n    id INTEGER PRIMARY KEY AUTOINCREMENT,\n    name TEXT NOT NULL,\n    created_at DATETIME DEFAULT CURRENT_TIMESTAMP\n);",
            "sql"
        ));

        // 4. Listen to document opened for .db / .sqlite
        _context.EditorService.DocumentOpened += (s, e) =>
        {
            if (e.FilePath.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
                e.FilePath.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase) ||
                e.FilePath.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    _viewModel.OpenDatabase(e.FilePath);
                    _context.Log($"[SQLite] Auto-opened database {Path.GetFileName(e.FilePath)}");
                }
                catch (Exception ex)
                {
                    _context.Log($"[SQLite] Error opening database: {ex.Message}", "Error");
                }
            }
        };

        _context.Log("SQLite Explorer extension initialized.");
    }

    public Task ActivateAsync() => Task.CompletedTask;
    public Task DeactivateAsync() => Task.CompletedTask;
}
