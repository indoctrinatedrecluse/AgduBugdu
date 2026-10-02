using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Plugin.DataGridLive;

public class CsvRowItem
{
    public int Index { get; set; }
    public IReadOnlyList<string> Values { get; set; } = Array.Empty<string>();
}

public class CsvTableViewModel
{
    private CsvTableData _data = new(Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>(), ',');
    private string _searchQuery = string.Empty;

    public string Title { get; private set; } = "Data Table";
    public string FilePath { get; private set; } = string.Empty;
    public IReadOnlyList<string> Headers => _data.Headers;
    public int TotalRows => _data.Rows.Count;
    public int TotalColumns => _data.Headers.Count;
    public string DelimiterDisplay => _data.Delimiter switch
    {
        '\t' => "Tab (TSV)",
        ';' => "Semicolon",
        '|' => "Pipe",
        _ => "Comma (CSV)"
    };

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            _searchQuery = value;
            ApplyFilter();
        }
    }

    public List<CsvRowItem> FilteredRows { get; private set; } = new();

    public event EventHandler? DataChanged;

    public void LoadFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;

            FilePath = filePath;
            Title = $"Data Table: {Path.GetFileName(filePath)}";

            var content = File.ReadAllText(filePath);
            _data = CsvParser.Parse(content);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _data = new CsvTableData(new[] { "Error" }, new[] { new[] { ex.Message } }, ',');
            ApplyFilter();
        }
    }

    public void ApplyFilter()
    {
        var list = new List<CsvRowItem>();
        int idx = 1;
        var q = _searchQuery?.Trim() ?? string.Empty;

        foreach (var r in _data.Rows)
        {
            if (string.IsNullOrEmpty(q) || r.Any(v => v.Contains(q, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(new CsvRowItem { Index = idx, Values = r });
            }
            idx++;
        }

        FilteredRows = list;
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public string ToFormattedText()
    {
        if (_data.Headers.Count == 0) return "No data loaded.";

        var sb = new StringBuilder();
        sb.AppendLine($"# {Title}");
        sb.AppendLine($"Total Rows: {TotalRows} | Columns: {TotalColumns} | Delimiter: {DelimiterDisplay}");
        if (!string.IsNullOrEmpty(_searchQuery))
        {
            sb.AppendLine($"Filtered: {FilteredRows.Count} matching '{_searchQuery}'");
        }
        sb.AppendLine(new string('-', 80));

        // Format grid columns
        var colWidths = new int[_data.Headers.Count];
        for (int i = 0; i < _data.Headers.Count; i++)
        {
            colWidths[i] = Math.Clamp(_data.Headers[i].Length, 4, 30);
        }

        foreach (var r in FilteredRows.Take(100))
        {
            for (int i = 0; i < Math.Min(r.Values.Count, colWidths.Length); i++)
            {
                colWidths[i] = Math.Clamp(Math.Max(colWidths[i], r.Values[i].Length), 4, 30);
            }
        }

        // Header
        sb.Append("#   | ");
        for (int i = 0; i < _data.Headers.Count; i++)
        {
            var h = _data.Headers[i].PadRight(colWidths[i]);
            if (h.Length > colWidths[i]) h = h.Substring(0, colWidths[i]);
            sb.Append(h + " | ");
        }
        sb.AppendLine();
        sb.AppendLine(new string('=', Math.Min(sb.Length, 100)));

        // Rows
        foreach (var r in FilteredRows)
        {
            sb.Append($"{r.Index,3} | ");
            for (int i = 0; i < colWidths.Length; i++)
            {
                var val = i < r.Values.Count ? r.Values[i] : "";
                var display = val.PadRight(colWidths[i]);
                if (display.Length > colWidths[i]) display = display.Substring(0, colWidths[i]);
                sb.Append(display + " | ");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public class DataGridLiveExtension : IExtension
{
    private IExtensionContext? _context;
    private CsvTableViewModel? _viewModel;

    public string Id => "agdubugdu.plugin.datagridlive";
    public string Name => "CSV / TSV Data Table Viewer";
    public string Version => "1.0.0";

    public void Initialize(IExtensionContext context)
    {
        _context = context;
        _viewModel = new CsvTableViewModel();

        _context.Commands.RegisterCommand(
            "csv.viewer",
            "CSV: View Active File as Data Table",
            () =>
            {
                if (_context.EditorService.ActiveDocumentPath != null)
                {
                    _viewModel.LoadFile(_context.EditorService.ActiveDocumentPath);
                }
                return Task.CompletedTask;
            }
        );

        _context.ToolWindows.RegisterToolWindow(
            "csv.viewer.tool",
            "CSV Data Table",
            () => _viewModel,
            vm => _viewModel
        );

        _context.EditorService.DocumentOpened += (s, e) =>
        {
            if (e.FilePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
                e.FilePath.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.LoadFile(e.FilePath);
            }
        };

        _context.EditorService.DocumentSaved += (s, e) =>
        {
            if (e.FilePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
                e.FilePath.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.LoadFile(e.FilePath);
            }
        };

        _context.Log("CSV / TSV Data Table extension initialized.");
    }

    public Task ActivateAsync() => Task.CompletedTask;
    public Task DeactivateAsync() => Task.CompletedTask;
}
