using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgduBugdu.Plugin.DataGridLive;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Documents;

public partial class CsvDocumentViewModel : Document
{
    private readonly CsvTableViewModel _tableVm = new();

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _statusSummary = string.Empty;

    [ObservableProperty]
    private string _delimiterDisplay = string.Empty;

    [ObservableProperty]
    private int _totalRows;

    [ObservableProperty]
    private int _filteredCount;

    [ObservableProperty]
    private int _totalColumns;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public ObservableCollection<string> Headers { get; } = new();
    public ObservableCollection<CsvRowItem> DisplayRows { get; } = new();

    public CsvDocumentViewModel()
    {
        Id = "csv.document";
        Title = "Data Table";
    }

    public void LoadFile(string path)
    {
        FilePath = path;
        FileName = Path.GetFileName(path);
        Title = $"Table: {FileName}";
        Reload();
    }

    [RelayCommand]
    public void Reload()
    {
        if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
        {
            HasError = true;
            ErrorMessage = "File does not exist or invalid path.";
            return;
        }

        try
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;

            _tableVm.LoadFile(FilePath);
            DelimiterDisplay = _tableVm.DelimiterDisplay;
            TotalRows = _tableVm.TotalRows;
            TotalColumns = _tableVm.TotalColumns;

            Headers.Clear();
            foreach (var header in _tableVm.Headers)
            {
                Headers.Add(header);
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Error loading data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    public void ClearFilter()
    {
        SearchQuery = string.Empty;
    }

    private void ApplyFilter()
    {
        _tableVm.SearchQuery = SearchQuery;
        DisplayRows.Clear();

        foreach (var row in _tableVm.FilteredRows)
        {
            DisplayRows.Add(row);
        }

        FilteredCount = DisplayRows.Count;
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            StatusSummary = $"{TotalRows:N0} rows, {TotalColumns} columns ({DelimiterDisplay})";
        }
        else
        {
            StatusSummary = $"{FilteredCount:N0} of {TotalRows:N0} rows matching \"{SearchQuery}\" ({DelimiterDisplay})";
        }
    }
}
