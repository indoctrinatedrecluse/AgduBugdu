using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AgduBugdu.Plugin.PdfLive;

public class PdfLiveViewModel
{
    private PdfDocumentData? _document;
    private int _currentPageNumber = 1;
    private string _searchQuery = string.Empty;
    private List<PdfSearchResultItem> _searchResults = new();

    public string Title => _document != null 
        ? $"PDF: {Path.GetFileName(_document.FilePath)}" 
        : "Live PDF Viewer & Editor";

    public string FilePath => _document?.FilePath ?? string.Empty;
    public int TotalPages => _document?.Pages.Count ?? 0;

    public int CurrentPageNumber
    {
        get => _currentPageNumber;
        set
        {
            if (_document == null || _document.Pages.Count == 0)
            {
                _currentPageNumber = 1;
                return;
            }

            int clamped = Math.Clamp(value, 1, _document.Pages.Count);
            if (_currentPageNumber != clamped)
            {
                _currentPageNumber = clamped;
                PageChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public PdfPageModel? CurrentPage
    {
        get
        {
            if (_document == null || _document.Pages.Count == 0) return null;
            int idx = Math.Clamp(_currentPageNumber - 1, 0, _document.Pages.Count - 1);
            return _document.Pages[idx];
        }
    }

    public PdfMetadata? Metadata => _document?.Metadata;
    public IReadOnlyList<PdfBookmarkItem> Bookmarks => _document?.Bookmarks ?? (IReadOnlyList<PdfBookmarkItem>)Array.Empty<PdfBookmarkItem>();
    public IReadOnlyList<PdfSearchResultItem> SearchResults => _searchResults;

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            _searchQuery = value;
            PerformSearch();
        }
    }

    public string SearchStatus { get; private set; } = string.Empty;

    public event EventHandler? DocumentChanged;
    public event EventHandler? PageChanged;

    public void LoadFile(string filePath)
    {
        if (!File.Exists(filePath)) return;

        _document = PdfEngine.LoadDocument(filePath);
        _currentPageNumber = 1;
        _searchResults.Clear();
        _searchQuery = string.Empty;
        SearchStatus = string.Empty;

        DocumentChanged?.Invoke(this, EventArgs.Empty);
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NextPage()
    {
        if (CurrentPageNumber < TotalPages)
        {
            CurrentPageNumber++;
        }
    }

    public void PreviousPage()
    {
        if (CurrentPageNumber > 1)
        {
            CurrentPageNumber--;
        }
    }

    public void FirstPage() => CurrentPageNumber = 1;
    public void LastPage() => CurrentPageNumber = TotalPages;

    public void PerformSearch(bool matchCase = false)
    {
        if (_document == null || string.IsNullOrWhiteSpace(_searchQuery))
        {
            _searchResults = new List<PdfSearchResultItem>();
            SearchStatus = string.Empty;
            return;
        }

        _searchResults = PdfEngine.Search(_document, _searchQuery.Trim(), matchCase);
        SearchStatus = _searchResults.Count > 0 
            ? $"{_searchResults.Count} matches across document" 
            : "No matches found";
    }

    public void RotateCurrentPage(int degrees = 90)
    {
        if (_document == null || string.IsNullOrEmpty(FilePath)) return;

        PdfEngine.RotatePage(FilePath, FilePath, CurrentPageNumber, degrees);
        // Reload document to reflect change
        LoadFile(FilePath);
    }

    public void DeleteCurrentPage()
    {
        if (_document == null || string.IsNullOrEmpty(FilePath) || TotalPages <= 1) return;

        int pageToDelete = CurrentPageNumber;
        PdfEngine.DeletePage(FilePath, FilePath, pageToDelete);
        
        int nextTarget = Math.Clamp(pageToDelete, 1, TotalPages - 1);
        LoadFile(FilePath);
        CurrentPageNumber = nextTarget;
    }

    public void AddWatermark(string text, WatermarkPosition position = WatermarkPosition.CenterDiagonal, bool allPages = true)
    {
        if (_document == null || string.IsNullOrEmpty(FilePath)) return;

        int target = allPages ? -1 : CurrentPageNumber;
        PdfEngine.AddWatermark(FilePath, FilePath, text, position, target);
        LoadFile(FilePath);
    }

    public void UpdateMetadata(string? title, string? author, string? subject, string? keywords)
    {
        if (_document == null || string.IsNullOrEmpty(FilePath)) return;

        PdfEngine.UpdateMetadata(FilePath, FilePath, info =>
        {
            if (title != null) info.Title = title;
            if (author != null) info.Author = author;
            if (subject != null) info.Subject = subject;
            if (keywords != null) info.Keywords = keywords;
        });

        LoadFile(FilePath);
    }

    public void ExtractPages(int startPage, int endPage, string outputPath)
    {
        if (_document == null || string.IsNullOrEmpty(FilePath)) return;
        PdfEngine.ExtractPages(FilePath, outputPath, startPage, endPage);
    }

    public void MergeWith(IEnumerable<string> additionalFiles, string outputPath)
    {
        if (string.IsNullOrEmpty(FilePath)) return;

        var allFiles = new List<string> { FilePath };
        allFiles.AddRange(additionalFiles);
        PdfEngine.MergeDocuments(allFiles, outputPath);
    }

    public string ToFormattedText()
    {
        if (_document == null)
        {
            return "# 📄 Live PDF Viewer & Editor\n\nNo PDF loaded. Open a .pdf file from the explorer or menu.";
        }

        return PdfEngine.ToFormattedSummary(_document, CurrentPageNumber);
    }
}
