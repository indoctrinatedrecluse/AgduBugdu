using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AgduBugdu.Core.Database;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Tools;

public partial class SqlStudioToolViewModel : Tool
{
    private readonly SqlStudioEngine _engine;

    [ObservableProperty]
    private string _sqlQuery = "SELECT id, username, email, role, created_at FROM users;";

    [ObservableProperty]
    private string _statusText = "Connected to Embedded SQLite Database (Memory/Workspace)";

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private SqlQueryResult? _lastResult;

    [ObservableProperty]
    private SqlTableSchema? _selectedTable;

    [ObservableProperty]
    private string _exportPreview = string.Empty;

    public ObservableCollection<SqlTableSchema> Tables { get; } = new();
    public ObservableCollection<SqlQueryHistoryItem> History { get; } = new();

    public SqlStudioToolViewModel()
    {
        Id = "SqlStudioTool";
        Title = "SQL & Database Studio";
        _engine = new SqlStudioEngine();

        RefreshSchema();
        ExecuteQuery();
    }

    [RelayCommand]
    public void ExecuteQuery()
    {
        if (string.IsNullOrWhiteSpace(SqlQuery))
        {
            StatusText = "Please write a SQL statement to execute.";
            return;
        }

        IsExecuting = true;
        try
        {
            var res = _engine.Execute(SqlQuery);
            LastResult = res;

            if (res.Success)
            {
                StatusText = $"Success: {res.RowsAffected} row(s) returned / affected in {res.ExecutionDurationMs} ms";
            }
            else
            {
                StatusText = $"SQL Error: {res.ErrorMessage}";
            }

            History.Insert(0, new SqlQueryHistoryItem
            {
                Query = SqlQuery.Trim(),
                Timestamp = res.ExecutedAt,
                Success = res.Success,
                RowCount = res.RowsAffected,
                DurationMs = res.ExecutionDurationMs
            });

            // If DDL or table structure changed, refresh schema
            if (SqlQuery.Contains("CREATE", StringComparison.OrdinalIgnoreCase) ||
                SqlQuery.Contains("DROP", StringComparison.OrdinalIgnoreCase) ||
                SqlQuery.Contains("ALTER", StringComparison.OrdinalIgnoreCase))
            {
                RefreshSchema();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Execution error: {ex.Message}";
        }
        finally
        {
            IsExecuting = false;
        }
    }

    [RelayCommand]
    public void RefreshSchema()
    {
        try
        {
            var schema = _engine.GetSchema();
            Tables.Clear();
            foreach (var t in schema)
            {
                Tables.Add(t);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading schema: {ex.Message}";
        }
    }

    partial void OnSelectedTableChanged(SqlTableSchema? value)
    {
        if (value != null)
        {
            SqlQuery = $"SELECT * FROM {value.TableName} LIMIT 100;";
            ExecuteQuery();
        }
    }

    [RelayCommand]
    public void ExportCsv()
    {
        if (LastResult != null)
        {
            ExportPreview = SqlStudioEngine.ExportToCsv(LastResult);
            StatusText = "Exported results to CSV buffer.";
        }
    }

    [RelayCommand]
    public void ExportJson()
    {
        if (LastResult != null)
        {
            ExportPreview = SqlStudioEngine.ExportToJson(LastResult);
            StatusText = "Exported results to JSON buffer.";
        }
    }

    [RelayCommand]
    public void ExportMarkdown()
    {
        if (LastResult != null)
        {
            ExportPreview = SqlStudioEngine.ExportToMarkdown(LastResult);
            StatusText = "Exported results to Markdown Table buffer.";
        }
    }

    [RelayCommand]
    public void ClearQuery()
    {
        SqlQuery = string.Empty;
        StatusText = "Query cleared.";
    }
}
