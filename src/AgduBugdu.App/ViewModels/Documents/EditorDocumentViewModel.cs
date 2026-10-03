using System;
using System.IO;
using AgduBugdu.Core.Text;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Documents;

public partial class EditorDocumentViewModel : Document
{
    private string _originalText = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileName = "Untitled";

    [ObservableProperty]
    private bool _isModified;

    [ObservableProperty]
    private TextDocument _textDocument = new();

    [ObservableProperty]
    private int _line = 1;

    [ObservableProperty]
    private int _column = 1;

    [ObservableProperty]
    private int _caretOffset;

    [ObservableProperty]
    private int _selectedLength;

    [ObservableProperty]
    private bool _wordWrap = false;

    // --- Search & Replace State ---
    [ObservableProperty]
    private bool _isFindVisible;

    [ObservableProperty]
    private bool _isReplaceVisible;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _replaceQuery = string.Empty;

    [ObservableProperty]
    private bool _matchCase;

    [ObservableProperty]
    private bool _matchWholeWord;

    [ObservableProperty]
    private bool _useRegex;

    [ObservableProperty]
    private string _searchMatchStatus = string.Empty;

    [ObservableProperty]
    private bool _hasSearchMatches;

    // --- Go To Line State ---
    [ObservableProperty]
    private bool _isGoToLineVisible;

    [ObservableProperty]
    private string _goToLineInput = string.Empty;

    [ObservableProperty]
    private string _goToLinePrompt = "Go to Line:Column (e.g. 10:5)";

    public event EventHandler? CaretMoved;
    public event EventHandler<int>? ScrollToLineRequested;
    public event EventHandler<(int Offset, int Length)>? SelectTextRequested;
    public event EventHandler? FocusSearchBoxRequested;
    public event EventHandler? FocusGoToLineRequested;
    public event EventHandler? FocusEditorRequested;

    public Func<string?>? GetSelectedTextFunc { get; set; }

    public EditorDocumentViewModel()
    {
        Id = Guid.NewGuid().ToString();
        Title = FileName;
        _originalText = TextDocument.Text;
        TextDocument.UndoStack.MarkAsOriginalFile();
    }

    partial void OnFileNameChanged(string value)
    {
        Title = IsModified ? $"{value}*" : value;
    }

    partial void OnTextDocumentChanged(TextDocument value)
    {
        if (value != null)
        {
            _originalText = value.Text;
            value.UndoStack.MarkAsOriginalFile();
        }
        else
        {
            _originalText = string.Empty;
        }
        IsModified = false;
        Title = FileName;
        UpdateSearchMatches();
    }

    partial void OnLineChanged(int value)
    {
        CaretMoved?.Invoke(this, EventArgs.Empty);
    }

    partial void OnColumnChanged(int value)
    {
        CaretMoved?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSearchQueryChanged(string value)
    {
        UpdateSearchMatches();
    }

    partial void OnMatchCaseChanged(bool value)
    {
        UpdateSearchMatches();
    }

    partial void OnMatchWholeWordChanged(bool value)
    {
        UpdateSearchMatches();
    }

    partial void OnUseRegexChanged(bool value)
    {
        UpdateSearchMatches();
    }

    public void NavigateTo(int line, int column = 1)
    {
        Line = line;
        Column = column;
        ScrollToLineRequested?.Invoke(this, line);
    }

    public void CheckModified()
    {
        bool modified = !TextDocument.UndoStack.IsOriginalFile && TextDocument.Text != _originalText;
        if (IsModified != modified)
        {
            IsModified = modified;
            Title = modified ? $"{FileName}*" : FileName;
        }
    }

    public void InsertText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        int offset;
        try
        {
            if (Line > 0 && Line <= TextDocument.LineCount)
            {
                var docLine = TextDocument.GetLineByNumber(Line);
                int colOffset = Math.Clamp(Column - 1, 0, docLine.Length);
                offset = docLine.Offset + colOffset;
            }
            else
            {
                offset = TextDocument.TextLength;
            }
        }
        catch
        {
            offset = TextDocument.TextLength;
        }

        TextDocument.Insert(offset, text);
        CheckModified();
    }

    public string GetText() => TextDocument.Text;

    public void SetText(string text)
    {
        TextDocument.Text = text ?? string.Empty;
        CheckModified();
    }

    // --- Find & Replace Logic ---

    public SearchOptions GetSearchOptions() => new(SearchQuery, MatchCase, MatchWholeWord, UseRegex);

    public void UpdateSearchMatches()
    {
        if (!IsFindVisible || string.IsNullOrEmpty(SearchQuery))
        {
            SearchMatchStatus = string.Empty;
            HasSearchMatches = false;
            return;
        }

        var matches = SearchEngine.FindAll(TextDocument.Text, GetSearchOptions());
        HasSearchMatches = matches.Count > 0;
        if (matches.Count == 0)
        {
            SearchMatchStatus = "No results";
        }
        else
        {
            int currentIndex = -1;
            for (int i = 0; i < matches.Count; i++)
            {
                if (matches[i].Offset <= CaretOffset && CaretOffset <= matches[i].Offset + matches[i].Length)
                {
                    currentIndex = i;
                    break;
                }
            }

            SearchMatchStatus = currentIndex >= 0 ? $"{currentIndex + 1} of {matches.Count}" : $"{matches.Count} found";
        }
    }

    [RelayCommand]
    public void OpenFind(string? initialQuery = null)
    {
        IsFindVisible = true;
        if (!string.IsNullOrEmpty(initialQuery))
        {
            SearchQuery = initialQuery;
        }
        UpdateSearchMatches();
        FocusSearchBoxRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void OpenReplace(string? initialQuery = null)
    {
        IsFindVisible = true;
        IsReplaceVisible = true;
        if (!string.IsNullOrEmpty(initialQuery))
        {
            SearchQuery = initialQuery;
        }
        UpdateSearchMatches();
        FocusSearchBoxRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void CloseFind()
    {
        IsFindVisible = false;
        IsReplaceVisible = false;
        SearchMatchStatus = string.Empty;
        FocusEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void ToggleReplace()
    {
        IsReplaceVisible = !IsReplaceVisible;
    }

    [RelayCommand]
    public void FindNext()
    {
        if (string.IsNullOrEmpty(SearchQuery))
            return;

        int searchStart = CaretOffset;
        if (SelectedLength > 0)
            searchStart = CaretOffset + SelectedLength;

        var match = SearchEngine.FindNext(TextDocument.Text, GetSearchOptions(), searchStart);
        if (match != null)
        {
            var m = match.Value;
            SelectTextRequested?.Invoke(this, (m.Result.Offset, m.Result.Length));
            CaretOffset = m.Result.Offset;
            SearchMatchStatus = $"{m.Index + 1} of {m.TotalCount}";
            HasSearchMatches = true;
        }
        else
        {
            SearchMatchStatus = "No results";
            HasSearchMatches = false;
        }
    }

    [RelayCommand]
    public void FindPrevious()
    {
        if (string.IsNullOrEmpty(SearchQuery))
            return;

        var match = SearchEngine.FindPrevious(TextDocument.Text, GetSearchOptions(), CaretOffset);
        if (match != null)
        {
            var m = match.Value;
            SelectTextRequested?.Invoke(this, (m.Result.Offset, m.Result.Length));
            CaretOffset = m.Result.Offset;
            SearchMatchStatus = $"{m.Index + 1} of {m.TotalCount}";
            HasSearchMatches = true;
        }
        else
        {
            SearchMatchStatus = "No results";
            HasSearchMatches = false;
        }
    }

    [RelayCommand]
    public void ReplaceCurrent()
    {
        if (string.IsNullOrEmpty(SearchQuery))
            return;

        var options = GetSearchOptions();
        var selectedText = GetSelectedTextFunc?.Invoke();
        bool isMatch = false;

        if (!string.IsNullOrEmpty(selectedText))
        {
            var matches = SearchEngine.FindAll(selectedText, options);
            if (matches.Count == 1 && matches[0].Length == selectedText.Length)
            {
                isMatch = true;
            }
        }

        if (isMatch)
        {
            TextDocument.Replace(CaretOffset, SelectedLength, ReplaceQuery ?? string.Empty);
            CheckModified();
            FindNext();
        }
        else
        {
            FindNext();
        }
    }

    [RelayCommand]
    public void ReplaceAll()
    {
        if (string.IsNullOrEmpty(SearchQuery))
            return;

        var (newText, count) = SearchEngine.ReplaceAll(TextDocument.Text, GetSearchOptions(), ReplaceQuery ?? string.Empty);
        if (count > 0)
        {
            TextDocument.Text = newText;
            CheckModified();
            SearchMatchStatus = $"{count} replaced";
            HasSearchMatches = false;
        }
        else
        {
            SearchMatchStatus = "No results";
            HasSearchMatches = false;
        }
    }

    // --- Go To Line Logic ---

    [RelayCommand]
    public void OpenGoToLine()
    {
        IsGoToLineVisible = true;
        GoToLineInput = $"{Line}:{Column}";
        GoToLinePrompt = $"Current: Line {Line}, Col {Column}. Range: 1 - {Math.Max(1, TextDocument.LineCount)}";
        FocusGoToLineRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void ConfirmGoToLine()
    {
        if (string.IsNullOrWhiteSpace(GoToLineInput))
        {
            IsGoToLineVisible = false;
            FocusEditorRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        var clean = GoToLineInput.Trim().ToLowerInvariant().Replace("line", "").Trim();
        var parts = clean.Split(new[] { ':', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        int targetLine = Line;
        int targetCol = 1;

        if (parts.Length > 0 && int.TryParse(parts[0], out int parsedLine))
        {
            targetLine = Math.Clamp(parsedLine, 1, Math.Max(1, TextDocument.LineCount));
        }
        if (parts.Length > 1 && int.TryParse(parts[1], out int parsedCol))
        {
            targetCol = Math.Max(1, parsedCol);
        }

        NavigateTo(targetLine, targetCol);
        IsGoToLineVisible = false;
        FocusEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void CancelGoToLine()
    {
        IsGoToLineVisible = false;
        FocusEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    // --- Matching Bracket Logic ---

    [RelayCommand]
    public void GoToMatchingBracket()
    {
        var targetOffset = BracketMatcher.FindMatchingBracket(TextDocument.Text, CaretOffset);
        if (targetOffset.HasValue)
        {
            SelectTextRequested?.Invoke(this, (targetOffset.Value, 0));
            CaretOffset = targetOffset.Value;
        }
    }

    public static EditorDocumentViewModel FromFile(string path)
    {
        var content = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        var textDoc = new TextDocument(content);
        textDoc.UndoStack.MarkAsOriginalFile();
        var vm = new EditorDocumentViewModel
        {
            FilePath = path,
            FileName = Path.GetFileName(path),
            Id = path,
            TextDocument = textDoc,
            IsModified = false
        };
        vm._originalText = textDoc.Text;
        vm.Title = vm.FileName;
        return vm;
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(FilePath))
            return;

        File.WriteAllText(FilePath, TextDocument.Text);
        _originalText = TextDocument.Text;
        TextDocument.UndoStack.MarkAsOriginalFile();
        IsModified = false;
        Title = FileName;
    }

    public void SaveAs(string newPath)
    {
        FilePath = newPath;
        FileName = Path.GetFileName(newPath);
        Id = newPath;
        Save();
    }
}
