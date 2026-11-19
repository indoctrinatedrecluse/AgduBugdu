using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using AgduBugdu.Plugin.SqliteExplorer;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Tools;

public partial class SqliteExplorerToolViewModel : Tool
{
    private SqliteExplorerViewModel? _model;

    public event EventHandler? ResetSizeRequested;
    public event EventHandler? MinimizeRequested;

    [ObservableProperty]
    private string _databaseName = "No Database Opened";

    [ObservableProperty]
    private string _databasePath = string.Empty;

    [ObservableProperty]
    private string _queryText = "SELECT * FROM sqlite_master LIMIT 50;";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _resultFormattedText = "Execute a query or select a table to view data.";

    [ObservableProperty]
    private SqliteTableSchema? _selectedTable;

    public ObservableCollection<SqliteTableSchema> Tables { get; } = new();

    public SqliteExplorerToolViewModel()
    {
        Id = "SqliteExplorer";
        Title = "SQLite Explorer";
    }

    public void AttachModel(SqliteExplorerViewModel model)
    {
        _model = model;
        _model.DatabaseOpened += (s, e) => Avalonia.Threading.Dispatcher.UIThread.Post(SyncFromModel);
        _model.QueryExecuted += (s, e) => Avalonia.Threading.Dispatcher.UIThread.Post(SyncFromModel);
        SyncFromModel();
    }

    public void OpenDatabase(string path)
    {
        if (_model == null) _model = new SqliteExplorerViewModel();
        _model.OpenDatabase(path);
        SyncFromModel();
    }

    [RelayCommand]
    public async Task RunQueryAsync()
    {
        if (_model == null) _model = new SqliteExplorerViewModel();
        _model.CurrentQueryText = QueryText;
        var res = await _model.ExecuteQueryAsync(QueryText);
        SyncFromModel();
    }

    [RelayCommand]
    public void SelectTable(SqliteTableSchema? table)
    {
        if (table == null || _model == null) return;
        SelectedTable = table;
        QueryText = $"SELECT * FROM \"{table.TableName}\" LIMIT 100;";
        _ = RunQueryAsync();
    }

    private void SyncFromModel()
    {
        if (_model == null) return;

        DatabaseName = _model.DatabaseName;
        DatabasePath = _model.DatabasePath ?? string.Empty;
        StatusMessage = _model.StatusMessage;

        if (_model.LastQueryResult != null)
        {
            ResultFormattedText = _model.FormatResultAsMarkdownTable();
        }

        Tables.Clear();
        foreach (var t in _model.Tables)
        {
            Tables.Add(t);
        }
    }

    [RelayCommand]
    public void ResetSize() => ResetSizeRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    public void Minimize() => MinimizeRequested?.Invoke(this, EventArgs.Empty);
}
