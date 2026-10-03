using System;
using System.Collections.Generic;
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
    private int _selectionStart;

    [ObservableProperty]
    private bool _wordWrap = false;

    // --- Phase 3: Status & Formatting Controls ---
    [ObservableProperty]
    private string _lineEnding = "CRLF";

    [ObservableProperty]
    private int _tabSize = 4;

    [ObservableProperty]
    private bool _useSpacesForTabs = true;

    [ObservableProperty]
    private string _encodingName = "UTF-8";

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

    public static Func<string, (string LinePrefix, string? BlockStart, string? BlockEnd)>? CommentSyntaxResolver { get; set; }

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

    // --- Editing & Line Manipulations (Phase 2) ---

    private (string LinePrefix, string? BlockStart, string? BlockEnd) ResolveCommentSyntax()
    {
        if (CommentSyntaxResolver != null)
        {
            try
            {
                return CommentSyntaxResolver(FilePath);
            }
            catch
            {
                // Fall back to built-in rules
            }
        }
        return CommentSyntax.GetCommentSyntax(FilePath);
    }

    private void RequestSelectText(int offset, int length)
    {
        SelectTextRequested?.Invoke(this, (offset, length));
        SelectionStart = offset;
        SelectedLength = length;
        CaretOffset = offset + length;
    }

    [RelayCommand]
    public void ToggleLineComment()
    {
        if (TextDocument.LineCount == 0)
            return;

        int selStart = SelectedLength > 0 ? SelectionStart : CaretOffset;
        int selLen = SelectedLength;

        selStart = Math.Clamp(selStart, 0, TextDocument.TextLength);
        int selEnd = Math.Clamp(selStart + selLen, 0, TextDocument.TextLength);

        var startDocLine = TextDocument.GetLineByOffset(selStart);
        var endDocLine = TextDocument.GetLineByOffset(selEnd);

        int startLineNum = startDocLine.LineNumber;
        int endLineNum = endDocLine.LineNumber;

        if (selLen > 0 && endDocLine.Offset == selEnd && endLineNum > startLineNum)
        {
            endLineNum--;
        }

        var (linePrefix, _, _) = ResolveCommentSyntax();

        var lineTexts = new List<string>();
        for (int i = startLineNum; i <= endLineNum; i++)
        {
            var l = TextDocument.GetLineByNumber(i);
            lineTexts.Add(TextDocument.GetText(l.Offset, l.Length));
        }

        var (updatedLines, _) = TextManipulations.ToggleLineComments(lineTexts, linePrefix);

        TextDocument.BeginUpdate();
        try
        {
            for (int i = 0; i < updatedLines.Count; i++)
            {
                var lineNum = startLineNum + i;
                var l = TextDocument.GetLineByNumber(lineNum);
                TextDocument.Replace(l.Offset, l.Length, updatedLines[i]);
            }
        }
        finally
        {
            TextDocument.EndUpdate();
        }

        CheckModified();

        var firstLine = TextDocument.GetLineByNumber(startLineNum);
        var lastLine = TextDocument.GetLineByNumber(endLineNum);
        if (selLen > 0)
        {
            int newSelStart = firstLine.Offset;
            int newSelLen = (lastLine.Offset + lastLine.Length) - firstLine.Offset;
            RequestSelectText(newSelStart, newSelLen);
        }
        else
        {
            int targetCol = Math.Clamp(Column, 1, firstLine.Length + 1);
            int newCaret = firstLine.Offset + (targetCol - 1);
            RequestSelectText(newCaret, 0);
        }
    }

    [RelayCommand]
    public void ToggleBlockComment()
    {
        if (TextDocument.LineCount == 0)
            return;

        var (_, blockStart, blockEnd) = ResolveCommentSyntax();
        blockStart ??= "/*";
        blockEnd ??= "*/";

        int selStart = SelectedLength > 0 ? SelectionStart : CaretOffset;
        int selLen = SelectedLength;

        selStart = Math.Clamp(selStart, 0, TextDocument.TextLength);
        selLen = Math.Clamp(selLen, 0, TextDocument.TextLength - selStart);

        if (selLen > 0)
        {
            string selectedText = TextDocument.GetText(selStart, selLen);
            var (newText, _) = TextManipulations.ToggleBlockComment(selectedText, blockStart, blockEnd);

            TextDocument.BeginUpdate();
            try
            {
                TextDocument.Replace(selStart, selLen, newText);
            }
            finally
            {
                TextDocument.EndUpdate();
            }

            CheckModified();
            RequestSelectText(selStart, newText.Length);
        }
        else
        {
            var line = TextDocument.GetLineByOffset(selStart);
            string lineText = TextDocument.GetText(line.Offset, line.Length);
            if (!string.IsNullOrWhiteSpace(lineText))
            {
                var (newText, _) = TextManipulations.ToggleBlockComment(lineText, blockStart, blockEnd);
                TextDocument.BeginUpdate();
                try
                {
                    TextDocument.Replace(line.Offset, line.Length, newText);
                }
                finally
                {
                    TextDocument.EndUpdate();
                }

                CheckModified();
                RequestSelectText(line.Offset, newText.Length);
            }
            else
            {
                string emptyComment = $"{blockStart}  {blockEnd}";
                TextDocument.BeginUpdate();
                try
                {
                    TextDocument.Insert(selStart, emptyComment);
                }
                finally
                {
                    TextDocument.EndUpdate();
                }

                CheckModified();
                RequestSelectText(selStart + blockStart.Length + 1, 0);
            }
        }
    }

    [RelayCommand]
    public void MoveLineUp()
    {
        if (TextDocument.LineCount <= 1) return;

        int selStart = SelectedLength > 0 ? SelectionStart : CaretOffset;
        int selEnd = selStart + SelectedLength;
        selStart = Math.Clamp(selStart, 0, TextDocument.TextLength);
        selEnd = Math.Clamp(selEnd, 0, TextDocument.TextLength);

        var startDocLine = TextDocument.GetLineByOffset(selStart);
        var endDocLine = TextDocument.GetLineByOffset(selEnd);
        int startLineNum = startDocLine.LineNumber;
        int endLineNum = endDocLine.LineNumber;
        if (SelectedLength > 0 && endDocLine.Offset == selEnd && endLineNum > startLineNum)
            endLineNum--;

        if (startLineNum <= 1) return;

        var prevLine = TextDocument.GetLineByNumber(startLineNum - 1);
        string prevLineText = TextDocument.GetText(prevLine.Offset, prevLine.Length);

        var blockStartOffset = startDocLine.Offset;
        var blockLastLine = TextDocument.GetLineByNumber(endLineNum);
        int blockLength = (blockLastLine.Offset + blockLastLine.Length) - blockStartOffset;
        string blockText = TextDocument.GetText(blockStartOffset, blockLength);

        string delimiter = prevLine.DelimiterLength > 0
            ? TextDocument.GetText(prevLine.Offset + prevLine.Length, prevLine.DelimiterLength)
            : "\n";

        TextDocument.BeginUpdate();
        try
        {
            int totalReplaceOffset = prevLine.Offset;
            int totalReplaceLength = (blockLastLine.Offset + blockLastLine.Length) - prevLine.Offset;
            string combined = blockText + delimiter + prevLineText;
            TextDocument.Replace(totalReplaceOffset, totalReplaceLength, combined);
        }
        finally
        {
            TextDocument.EndUpdate();
        }

        CheckModified();

        var newStart = TextDocument.GetLineByNumber(startLineNum - 1);
        var newEnd = TextDocument.GetLineByNumber(endLineNum - 1);
        int newSelStart = newStart.Offset;
        int newSelLength = (newEnd.Offset + newEnd.Length) - newStart.Offset;
        RequestSelectText(newSelStart, SelectedLength > 0 ? newSelLength : 0);
    }

    [RelayCommand]
    public void MoveLineDown()
    {
        if (TextDocument.LineCount <= 1) return;

        int selStart = SelectedLength > 0 ? SelectionStart : CaretOffset;
        int selEnd = selStart + SelectedLength;
        selStart = Math.Clamp(selStart, 0, TextDocument.TextLength);
        selEnd = Math.Clamp(selEnd, 0, TextDocument.TextLength);

        var startDocLine = TextDocument.GetLineByOffset(selStart);
        var endDocLine = TextDocument.GetLineByOffset(selEnd);
        int startLineNum = startDocLine.LineNumber;
        int endLineNum = endDocLine.LineNumber;
        if (SelectedLength > 0 && endDocLine.Offset == selEnd && endLineNum > startLineNum)
            endLineNum--;

        if (endLineNum >= TextDocument.LineCount) return;

        var nextLine = TextDocument.GetLineByNumber(endLineNum + 1);
        string nextLineText = TextDocument.GetText(nextLine.Offset, nextLine.Length);

        var blockStartOffset = startDocLine.Offset;
        var blockLastLine = TextDocument.GetLineByNumber(endLineNum);
        int blockLength = (blockLastLine.Offset + blockLastLine.Length) - blockStartOffset;
        string blockText = TextDocument.GetText(blockStartOffset, blockLength);

        string delimiter = blockLastLine.DelimiterLength > 0
            ? TextDocument.GetText(blockLastLine.Offset + blockLastLine.Length, blockLastLine.DelimiterLength)
            : "\n";

        TextDocument.BeginUpdate();
        try
        {
            int totalReplaceOffset = blockStartOffset;
            int totalReplaceLength = (nextLine.Offset + nextLine.Length) - blockStartOffset;
            string combined = nextLineText + delimiter + blockText;
            TextDocument.Replace(totalReplaceOffset, totalReplaceLength, combined);
        }
        finally
        {
            TextDocument.EndUpdate();
        }

        CheckModified();

        var newStart = TextDocument.GetLineByNumber(startLineNum + 1);
        var newEnd = TextDocument.GetLineByNumber(endLineNum + 1);
        int newSelStart = newStart.Offset;
        int newSelLength = (newEnd.Offset + newEnd.Length) - newStart.Offset;
        RequestSelectText(newSelStart, SelectedLength > 0 ? newSelLength : 0);
    }

    [RelayCommand]
    public void DuplicateLineDown()
    {
        if (TextDocument.LineCount == 0) return;

        int selStart = SelectedLength > 0 ? SelectionStart : CaretOffset;
        int selEnd = selStart + SelectedLength;
        selStart = Math.Clamp(selStart, 0, TextDocument.TextLength);
        selEnd = Math.Clamp(selEnd, 0, TextDocument.TextLength);

        var startDocLine = TextDocument.GetLineByOffset(selStart);
        var endDocLine = TextDocument.GetLineByOffset(selEnd);
        int startLineNum = startDocLine.LineNumber;
        int endLineNum = endDocLine.LineNumber;
        if (SelectedLength > 0 && endDocLine.Offset == selEnd && endLineNum > startLineNum)
            endLineNum--;

        var blockLastLine = TextDocument.GetLineByNumber(endLineNum);
        int blockLength = (blockLastLine.Offset + blockLastLine.Length) - startDocLine.Offset;
        string blockText = TextDocument.GetText(startDocLine.Offset, blockLength);

        string delimiter = blockLastLine.DelimiterLength > 0
            ? TextDocument.GetText(blockLastLine.Offset + blockLastLine.Length, blockLastLine.DelimiterLength)
            : "\n";

        TextDocument.BeginUpdate();
        try
        {
            if (endLineNum < TextDocument.LineCount)
            {
                int insertOffset = blockLastLine.Offset + blockLastLine.TotalLength;
                TextDocument.Insert(insertOffset, blockText + delimiter);
            }
            else
            {
                TextDocument.Insert(TextDocument.TextLength, delimiter + blockText);
            }
        }
        finally
        {
            TextDocument.EndUpdate();
        }

        CheckModified();

        int lineSpan = endLineNum - startLineNum + 1;
        var newStart = TextDocument.GetLineByNumber(startLineNum + lineSpan);
        var newEnd = TextDocument.GetLineByNumber(endLineNum + lineSpan);
        RequestSelectText(newStart.Offset, SelectedLength > 0 ? (newEnd.Offset + newEnd.Length) - newStart.Offset : 0);
    }

    [RelayCommand]
    public void DuplicateLineUp()
    {
        if (TextDocument.LineCount == 0) return;

        int selStart = SelectedLength > 0 ? SelectionStart : CaretOffset;
        int selEnd = selStart + SelectedLength;
        selStart = Math.Clamp(selStart, 0, TextDocument.TextLength);
        selEnd = Math.Clamp(selEnd, 0, TextDocument.TextLength);

        var startDocLine = TextDocument.GetLineByOffset(selStart);
        var endDocLine = TextDocument.GetLineByOffset(selEnd);
        int startLineNum = startDocLine.LineNumber;
        int endLineNum = endDocLine.LineNumber;
        if (SelectedLength > 0 && endDocLine.Offset == selEnd && endLineNum > startLineNum)
            endLineNum--;

        var blockLastLine = TextDocument.GetLineByNumber(endLineNum);
        int blockLength = (blockLastLine.Offset + blockLastLine.Length) - startDocLine.Offset;
        string blockText = TextDocument.GetText(startDocLine.Offset, blockLength);

        string delimiter = startDocLine.DelimiterLength > 0
            ? TextDocument.GetText(startDocLine.Offset + startDocLine.Length, startDocLine.DelimiterLength)
            : "\n";

        TextDocument.BeginUpdate();
        try
        {
            TextDocument.Insert(startDocLine.Offset, blockText + delimiter);
        }
        finally
        {
            TextDocument.EndUpdate();
        }

        CheckModified();

        var newStart = TextDocument.GetLineByNumber(startLineNum);
        var newEnd = TextDocument.GetLineByNumber(endLineNum);
        RequestSelectText(newStart.Offset, SelectedLength > 0 ? (newEnd.Offset + newEnd.Length) - newStart.Offset : 0);
    }

    [RelayCommand]
    public void DeleteLine()
    {
        if (TextDocument.LineCount == 0) return;
        if (TextDocument.LineCount == 1)
        {
            TextDocument.Text = string.Empty;
            CheckModified();
            RequestSelectText(0, 0);
            return;
        }

        int selStart = SelectedLength > 0 ? SelectionStart : CaretOffset;
        int selEnd = selStart + SelectedLength;
        selStart = Math.Clamp(selStart, 0, TextDocument.TextLength);
        selEnd = Math.Clamp(selEnd, 0, TextDocument.TextLength);

        var startDocLine = TextDocument.GetLineByOffset(selStart);
        var endDocLine = TextDocument.GetLineByOffset(selEnd);
        int startLineNum = startDocLine.LineNumber;
        int endLineNum = endDocLine.LineNumber;
        if (SelectedLength > 0 && endDocLine.Offset == selEnd && endLineNum > startLineNum)
            endLineNum--;

        var blockLastLine = TextDocument.GetLineByNumber(endLineNum);

        TextDocument.BeginUpdate();
        try
        {
            if (endLineNum < TextDocument.LineCount)
            {
                int removeOffset = startDocLine.Offset;
                int removeLength = (blockLastLine.Offset + blockLastLine.TotalLength) - removeOffset;
                TextDocument.Remove(removeOffset, removeLength);
            }
            else
            {
                if (startLineNum > 1)
                {
                    var prevLine = TextDocument.GetLineByNumber(startLineNum - 1);
                    int removeOffset = prevLine.Offset + prevLine.Length;
                    int removeLength = TextDocument.TextLength - removeOffset;
                    TextDocument.Remove(removeOffset, removeLength);
                }
                else
                {
                    TextDocument.Text = string.Empty;
                }
            }
        }
        finally
        {
            TextDocument.EndUpdate();
        }

        CheckModified();

        int targetLine = Math.Clamp(startLineNum, 1, TextDocument.LineCount);
        var line = TextDocument.GetLineByNumber(targetLine);
        RequestSelectText(line.Offset, 0);
    }

    [RelayCommand]
    public void JoinLines()
    {
        if (TextDocument.LineCount <= 1) return;

        int selStart = SelectedLength > 0 ? SelectionStart : CaretOffset;
        int selEnd = selStart + SelectedLength;
        selStart = Math.Clamp(selStart, 0, TextDocument.TextLength);
        selEnd = Math.Clamp(selEnd, 0, TextDocument.TextLength);

        var startDocLine = TextDocument.GetLineByOffset(selStart);
        var endDocLine = TextDocument.GetLineByOffset(selEnd);
        int startLineNum = startDocLine.LineNumber;
        int endLineNum = endDocLine.LineNumber;

        if (SelectedLength == 0 || startLineNum == endLineNum)
        {
            if (startLineNum >= TextDocument.LineCount) return;
            endLineNum = startLineNum + 1;
        }

        var lines = new List<string>();
        for (int i = startLineNum; i <= endLineNum; i++)
        {
            var l = TextDocument.GetLineByNumber(i);
            lines.Add(TextDocument.GetText(l.Offset, l.Length));
        }

        string joined = TextManipulations.JoinLines(lines);

        var first = TextDocument.GetLineByNumber(startLineNum);
        var last = TextDocument.GetLineByNumber(endLineNum);

        TextDocument.BeginUpdate();
        try
        {
            int replaceOffset = first.Offset;
            int replaceLength = (last.Offset + last.Length) - first.Offset;
            TextDocument.Replace(replaceOffset, replaceLength, joined);
        }
        finally
        {
            TextDocument.EndUpdate();
        }

        CheckModified();
        RequestSelectText(first.Offset + joined.Length, 0);
    }

    [RelayCommand]
    public void TransformUppercase() => TransformCase(true);

    [RelayCommand]
    public void TransformLowercase() => TransformCase(false);

    private void TransformCase(bool toUpper)
    {
        if (TextDocument.TextLength == 0) return;

        if (SelectedLength > 0)
        {
            int selStart = Math.Clamp(SelectionStart, 0, TextDocument.TextLength);
            int selLen = Math.Clamp(SelectedLength, 0, TextDocument.TextLength - selStart);
            string text = TextDocument.GetText(selStart, selLen);
            string transformed = toUpper ? text.ToUpperInvariant() : text.ToLowerInvariant();
            TextDocument.BeginUpdate();
            try
            {
                TextDocument.Replace(selStart, selLen, transformed);
            }
            finally
            {
                TextDocument.EndUpdate();
            }

            CheckModified();
            RequestSelectText(selStart, selLen);
        }
        else
        {
            var (wordStart, wordLen) = TextManipulations.FindWordBoundaries(TextDocument.Text, CaretOffset);
            if (wordLen > 0)
            {
                string word = TextDocument.GetText(wordStart, wordLen);
                string transformed = toUpper ? word.ToUpperInvariant() : word.ToLowerInvariant();
                TextDocument.BeginUpdate();
                try
                {
                    TextDocument.Replace(wordStart, wordLen, transformed);
                }
                finally
                {
                    TextDocument.EndUpdate();
                }

                CheckModified();
                RequestSelectText(wordStart + wordLen, 0);
            }
        }
    }

    [RelayCommand]
    public void ToggleWordWrap()
    {
        WordWrap = !WordWrap;
    }

    // --- Phase 3 Formatting & Status Methods ---

    public void ConvertToCrlf()
    {
        var text = TextDocument.Text;
        var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        var crlf = normalized.Replace("\n", "\r\n");
        if (crlf != text)
        {
            TextDocument.Text = crlf;
            CheckModified();
        }
        LineEnding = "CRLF";
    }

    public void ConvertToLf()
    {
        var text = TextDocument.Text;
        var lf = text.Replace("\r\n", "\n").Replace("\r", "\n");
        if (lf != text)
        {
            TextDocument.Text = lf;
            CheckModified();
        }
        LineEnding = "LF";
    }

    public void SetTabSize(int size)
    {
        TabSize = Math.Clamp(size, 1, 16);
    }

    public void ConvertTabsToSpaces()
    {
        var text = TextDocument.Text;
        string spaces = new string(' ', TabSize);
        var converted = text.Replace("\t", spaces);
        if (converted != text)
        {
            TextDocument.Text = converted;
            CheckModified();
        }
        UseSpacesForTabs = true;
    }

    public void ConvertSpacesToTabs()
    {
        var text = TextDocument.Text;
        string spaces = new string(' ', TabSize);
        var converted = text.Replace(spaces, "\t");
        if (converted != text)
        {
            TextDocument.Text = converted;
            CheckModified();
        }
        UseSpacesForTabs = false;
    }

    public void SetEncoding(string encoding)
    {
        EncodingName = encoding;
    }

    public void DetectLineEndings()
    {
        var text = TextDocument.Text;
        if (text.Contains("\r\n"))
        {
            LineEnding = "CRLF";
        }
        else if (text.Contains("\n"))
        {
            LineEnding = "LF";
        }
        else
        {
            LineEnding = Environment.NewLine == "\r\n" ? "CRLF" : "LF";
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
        vm.DetectLineEndings();
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
