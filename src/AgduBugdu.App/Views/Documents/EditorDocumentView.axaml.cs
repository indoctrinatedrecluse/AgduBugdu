using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using AgduBugdu.App.ViewModels;
using AgduBugdu.App.ViewModels.Documents;
using AgduBugdu.App.Themes;
using AgduBugdu.PluginContracts;
using TextMateSharp.Grammars;
using AvaloniaEdit.Folding;
using System.Collections.Generic;
using System.Linq;
using AgduBugdu.Core.Snippets;
using AgduBugdu.Core.Text;
using AvaloniaEdit.Document;

namespace AgduBugdu.App.Views.Documents;

public class MultiCaretRenderer : IBackgroundRenderer
{
    private readonly MultiCaretManager _multiCaret;
    private readonly TextEditor _editor;

    public KnownLayer Layer => KnownLayer.Selection;

    public MultiCaretRenderer(MultiCaretManager multiCaret, TextEditor editor)
    {
        _multiCaret = multiCaret;
        _editor = editor;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!_multiCaret.HasSecondaryCarets || _editor.Document == null)
            return;

        var caretBrush = new SolidColorBrush(Color.FromArgb(240, 56, 189, 248)); // Bright Cyan caret
        var selectionBrush = new SolidColorBrush(Color.FromArgb(90, 56, 189, 248)); // Cyan selection highlight
        var caretPen = new Pen(caretBrush, 2.0);

        foreach (var c in _multiCaret.SecondaryCarets)
        {
            if (c.Offset < 0 || c.Offset > _editor.Document.TextLength) continue;

            if (c.HasSelection && c.SelectionStart >= 0 && c.SelectionStart + c.SelectionLength <= _editor.Document.TextLength)
            {
                var segment = new TextSegment { StartOffset = c.SelectionStart, Length = c.SelectionLength };
                foreach (var rc in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    drawingContext.FillRectangle(selectionBrush, rc);
                }
            }

            try
            {
                var loc = _editor.Document.GetLocation(c.Offset);
                var pos = new TextViewPosition(loc);
                var top = textView.GetVisualPosition(pos, VisualYPosition.LineTop);
                var bottom = textView.GetVisualPosition(pos, VisualYPosition.LineBottom);
                drawingContext.DrawLine(caretPen, top, bottom);
            }
            catch
            {
                // Ignore coordinates for out-of-view lines
            }
        }
    }
}

public class DebugMarkerRenderer : IBackgroundRenderer
{
    private readonly Func<string?> _getFilePath;
    private readonly Func<IDebugService?> _getDebugService;

    public KnownLayer Layer => KnownLayer.Background;

    public DebugMarkerRenderer(Func<string?> getFilePath, Func<IDebugService?> getDebugService)
    {
        _getFilePath = getFilePath;
        _getDebugService = getDebugService;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        var filePath = _getFilePath();
        var debugService = _getDebugService();
        if (string.IsNullOrEmpty(filePath) || debugService == null) return;

        foreach (var visualLine in textView.VisualLines)
        {
            int lineNum = visualLine.FirstDocumentLine.LineNumber;

            // Highlight current execution line (Yellow)
            if (debugService.CurrentExecutionFile != null &&
                string.Equals(debugService.CurrentExecutionFile, filePath, StringComparison.OrdinalIgnoreCase) &&
                debugService.CurrentExecutionLine == lineNum)
            {
                foreach (var rc in BackgroundGeometryBuilder.GetRectsForSegment(textView, visualLine.FirstDocumentLine))
                {
                    drawingContext.FillRectangle(new SolidColorBrush(Color.FromArgb(90, 234, 179, 8)), new Rect(0, rc.Y, textView.Bounds.Width, rc.Height));
                }
            }
            // Highlight breakpoint line (Red)
            else if (debugService.HasBreakpoint(filePath, lineNum))
            {
                foreach (var rc in BackgroundGeometryBuilder.GetRectsForSegment(textView, visualLine.FirstDocumentLine))
                {
                    drawingContext.FillRectangle(new SolidColorBrush(Color.FromArgb(60, 239, 68, 68)), new Rect(0, rc.Y, textView.Bounds.Width, rc.Height));
                }
            }
        }
    }
}

public class CodeLensRenderer : IBackgroundRenderer
{
    private readonly Func<EditorDocumentViewModel?> _getDoc;
    private readonly TextEditor _editor;
    private readonly Typeface _typeface = new("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.SemiBold);
    private readonly IBrush _lensBrush = new SolidColorBrush(Color.FromArgb(180, 148, 163, 184)); // Slate 400
    private readonly IBrush _testBadgeBrush = new SolidColorBrush(Color.FromArgb(220, 56, 189, 248)); // Cyan 400

    public KnownLayer Layer => KnownLayer.Selection;

    public CodeLensRenderer(Func<EditorDocumentViewModel?> getDoc, TextEditor editor)
    {
        _getDoc = getDoc;
        _editor = editor;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        var doc = _getDoc();
        if (doc == null || !doc.ShowCodeLens || doc.CodeLenses.Count == 0 || _editor.Document == null)
            return;

        foreach (var visualLine in textView.VisualLines)
        {
            int lineNum = visualLine.FirstDocumentLine.LineNumber;
            var match = doc.CodeLenses.FirstOrDefault(l => l.Line == lineNum);
            if (match == null) continue;

            try
            {
                var pos = new TextViewPosition(lineNum, 1);
                var visualPos = textView.GetVisualPosition(pos, VisualYPosition.LineTop);

                var formatted = new FormattedText(
                    match.DisplayText,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    _typeface,
                    10.0,
                    match.IsTest ? _testBadgeBrush : _lensBrush
                );

                double x = Math.Max(2, visualPos.X);
                double y = Math.Max(0, visualPos.Y - 14);
                drawingContext.DrawText(formatted, new Point(x, y));
            }
            catch
            {
                // Fallback for transient rendering states
            }
        }
    }
}

public class IndentAndBracketRenderer : IBackgroundRenderer
{
    private readonly Func<EditorDocumentViewModel?> _getDoc;
    private readonly TextEditor _editor;
    private readonly Pen _normalGuidePen = new(new SolidColorBrush(Color.FromArgb(40, 200, 200, 200)), 1.0);
    private readonly Pen _activeGuidePen = new(new SolidColorBrush(Color.FromArgb(140, 56, 189, 248)), 1.5);
    private readonly Dictionary<string, IBrush> _rainbowBrushes = new();

    public KnownLayer Layer => KnownLayer.Background;

    public IndentAndBracketRenderer(Func<EditorDocumentViewModel?> getDoc, TextEditor editor)
    {
        _getDoc = getDoc;
        _editor = editor;

        foreach (var hex in BracketColorizerEngine.RainbowPalette)
        {
            if (Color.TryParse(hex, out var color))
            {
                _rainbowBrushes[hex] = new SolidColorBrush(Color.FromArgb(50, color.R, color.G, color.B));
            }
        }
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        var doc = _getDoc();
        if (doc == null || _editor.Document == null || _editor.Document.TextLength == 0)
            return;

        int caretLine = _editor.TextArea.Caret.Line;
        int tabSize = _editor.Options.IndentationSize;

        // 1. Draw Indentation Guides
        try
        {
            var guides = BracketColorizerEngine.ComputeIndentationGuides(_editor.Document.Text, tabSize, caretLine);
            foreach (var visualLine in textView.VisualLines)
            {
                int lineNum = visualLine.FirstDocumentLine.LineNumber;
                foreach (var g in guides)
                {
                    if (lineNum >= g.StartLine && lineNum <= g.EndLine)
                    {
                        var pos = new TextViewPosition(lineNum, g.Column + 1);
                        var visualPos = textView.GetVisualPosition(pos, VisualYPosition.LineTop);
                        var pen = g.IsActiveScope ? _activeGuidePen : _normalGuidePen;

                        drawingContext.DrawLine(
                            pen,
                            new Point(visualPos.X, visualLine.VisualTop),
                            new Point(visualPos.X, visualLine.VisualTop + visualLine.Height)
                        );
                    }
                }
            }
        }
        catch
        {
            // Transient layout fallback
        }

        // 2. Draw Rainbow Bracket Pairs
        try
        {
            var brackets = BracketColorizerEngine.FindRainbowBrackets(_editor.Document.Text);
            foreach (var b in brackets)
            {
                if (b.OpenOffset >= 0 && b.OpenOffset < _editor.Document.TextLength)
                {
                    var segOpen = new TextSegment { StartOffset = b.OpenOffset, Length = 1 };
                    foreach (var rc in BackgroundGeometryBuilder.GetRectsForSegment(textView, segOpen))
                    {
                        if (_rainbowBrushes.TryGetValue(b.ColorHex, out var brush))
                        {
                            drawingContext.FillRectangle(brush, rc);
                        }
                    }
                }

                if (b.CloseOffset >= 0 && b.CloseOffset < _editor.Document.TextLength)
                {
                    var segClose = new TextSegment { StartOffset = b.CloseOffset, Length = 1 };
                    foreach (var rc in BackgroundGeometryBuilder.GetRectsForSegment(textView, segClose))
                    {
                        if (_rainbowBrushes.TryGetValue(b.ColorHex, out var brush))
                        {
                            drawingContext.FillRectangle(brush, rc);
                        }
                    }
                }
            }
        }
        catch
        {
            // Transient layout fallback
        }
    }
}

public partial class EditorDocumentView : UserControl
{
    private TextMate.Installation? _textMateInstallation;
    private RegistryOptions? _registryOptions;
    private bool _rendererAttached;
    private FoldingManager? _foldingManager;
    private readonly MultiCaretManager _multiCaretManager = new();

    public EditorDocumentView()
    {
        InitializeComponent();

        Editor.Options.EnableHyperlinks = true;
        Editor.Options.EnableEmailHyperlinks = true;
        Editor.Options.ConvertTabsToSpaces = true;
        Editor.Options.IndentationSize = 4;
        Editor.Options.HighlightCurrentLine = true;
        Editor.Options.ShowBoxForControlCharacters = true;

        DataContextChanged += OnDataContextChanged;
        Editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        Editor.TextArea.SelectionChanged += OnSelectionChanged;
        Editor.TextChanged += OnTextChanged;
        Editor.KeyDown += OnEditorKeyDown;
        Editor.TextArea.TextEntering += OnTextAreaTextEntering;
        Editor.TextArea.PointerPressed += OnEditorPointerPressed;

        Editor.TextArea.TextView.ScrollOffsetChanged += (s, e) =>
        {
            UpdateMinimap();
            UpdateStickyScroll();
        };
        MinimapCanvas.SizeChanged += (s, e) => UpdateMinimap();

        // Ctrl + MouseWheel to zoom font size
        Editor.PointerWheelChanged += OnPointerWheelChanged;

        if (ToggleBreakpointMenuItem != null)
        {
            ToggleBreakpointMenuItem.Click += (s, e) => ToggleBreakpoint();
        }

        // Overlay keyboard shortcuts
        SearchBox.KeyDown += OnSearchBoxKeyDown;
        ReplaceBox.KeyDown += OnReplaceBoxKeyDown;
        GoToLineBox.KeyDown += OnGoToLineBoxKeyDown;

        ThemeManager.ThemeChanged += OnThemeChanged;
        ApplyTheme(ThemeManager.CurrentTheme);
    }

    private void OnThemeChanged(object? sender, AgduBugdu.App.Themes.ThemeDefinition theme)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyTheme(theme));
    }

    private void ApplyTheme(AgduBugdu.App.Themes.ThemeDefinition theme)
    {
        var bg = new SolidColorBrush(theme.EditorBackground);
        var fg = new SolidColorBrush(theme.EditorForeground);
        Background = bg;
        Editor.Background = bg;
        Editor.Foreground = fg;
        if (_textMateInstallation != null && _registryOptions != null)
        {
            var tmTheme = theme.Mode == AgduBugdu.App.Themes.AppThemeMode.SolarizedContrast
                ? ThemeName.SolarizedDark
                : ThemeName.DarkPlus;
            _textMateInstallation.SetTheme(_registryOptions.LoadTheme(tmTheme));
        }
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not EditorDocumentViewModel doc) return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (e.Key == Key.C)
            {
                doc.MatchCase = !doc.MatchCase;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.W)
            {
                doc.MatchWholeWord = !doc.MatchWholeWord;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.R)
            {
                doc.UseRegex = !doc.UseRegex;
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Enter)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                doc.FindPrevious();
            }
            else
            {
                doc.FindNext();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            doc.CloseFind();
            e.Handled = true;
        }
    }

    private void OnReplaceBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not EditorDocumentViewModel doc) return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (e.Key == Key.C)
            {
                doc.MatchCase = !doc.MatchCase;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.W)
            {
                doc.MatchWholeWord = !doc.MatchWholeWord;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.R)
            {
                doc.UseRegex = !doc.UseRegex;
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Enter)
        {
            doc.ReplaceCurrent();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            doc.CloseFind();
            e.Handled = true;
        }
    }

    private void OnGoToLineBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not EditorDocumentViewModel doc) return;

        if (e.Key == Key.Enter)
        {
            doc.ConfirmGoToLine();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            doc.CancelGoToLine();
            e.Handled = true;
        }
    }

    private IDebugService? GetDebugService()
    {
        if (VisualRoot is MainWindow mw && mw.DataContext is MainViewModel mvm)
        {
            return mvm.DebugService;
        }
        return null;
    }

    private ILanguageService? GetLanguageService()
    {
        if (VisualRoot is MainWindow mw && mw.DataContext is MainViewModel mvm)
        {
            return mvm.LanguageService;
        }
        return null;
    }

    public void ToggleBreakpoint()
    {
        if (DataContext is EditorDocumentViewModel doc && !string.IsNullOrEmpty(doc.FilePath))
        {
            var debugService = GetDebugService();
            if (debugService != null)
            {
                debugService.ToggleBreakpoint(doc.FilePath, doc.Line);
                Editor.TextArea.TextView.InvalidateVisual();
            }
        }
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not EditorDocumentViewModel doc) return;

        // Escape clears multi-carets
        if (e.Key == Key.Escape && _multiCaretManager.HasSecondaryCarets)
        {
            _multiCaretManager.Clear();
            Editor.TextArea.TextView.InvalidateVisual();
            e.Handled = true;
            return;
        }

        // Ctrl + D -> Select Next Occurrence
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.D)
        {
            HandleSelectNextOccurrence();
            e.Handled = true;
            return;
        }

        // Ctrl + Alt + Up / Down -> Column Carets
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Alt))
        {
            if (e.Key == Key.Up)
            {
                HandleAddColumnCaret(moveUp: true);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Down)
            {
                HandleAddColumnCaret(moveUp: false);
                e.Handled = true;
                return;
            }
        }

        // Backspace with multi-carets
        if (e.Key == Key.Back && _multiCaretManager.HasSecondaryCarets && Editor.Document != null)
        {
            var (newText, newPrimary, _) = _multiCaretManager.ApplyBackspace(Editor.Document.Text, Editor.CaretOffset, Editor.SelectionStart, Editor.SelectionLength);
            Editor.Document.Text = newText;
            Editor.CaretOffset = Math.Clamp(newPrimary, 0, Editor.Document.TextLength);
            Editor.TextArea.TextView.InvalidateVisual();
            e.Handled = true;
            return;
        }

        // Delete with multi-carets
        if (e.Key == Key.Delete && _multiCaretManager.HasSecondaryCarets && Editor.Document != null)
        {
            var (newText, newPrimary, _) = _multiCaretManager.ApplyDelete(Editor.Document.Text, Editor.CaretOffset, Editor.SelectionStart, Editor.SelectionLength);
            Editor.Document.Text = newText;
            Editor.CaretOffset = Math.Clamp(newPrimary, 0, Editor.Document.TextLength);
            Editor.TextArea.TextView.InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F9)
        {
            ToggleBreakpoint();
            e.Handled = true;
            return;
        }

        // Tab key for Snippet Template Expansion
        if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None && Editor.TextArea.Selection.IsEmpty)
        {
            if (TryExpandSnippet())
            {
                e.Handled = true;
                return;
            }
        }

        // Shift + Alt combinations
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (e.Key == Key.A)
            {
                doc.ToggleBlockComment();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up)
            {
                doc.DuplicateLineUp();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Down)
            {
                doc.DuplicateLineDown();
                e.Handled = true;
                return;
            }
        }

        // Alt combinations (no Shift)
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (e.Key == Key.M)
            {
                doc.ToggleMinimap();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up)
            {
                doc.MoveLineUp();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Down)
            {
                doc.MoveLineDown();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Z)
            {
                doc.ToggleWordWrap();
                e.Handled = true;
                return;
            }
        }

        // Control combinations
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            // Toggle Line Comment (Ctrl + /)
            if (e.Key == Key.OemQuestion || e.Key == Key.Divide)
            {
                doc.ToggleLineComment();
                e.Handled = true;
                return;
            }

            // Ctrl + Shift combinations
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                if (e.Key == Key.K)
                {
                    doc.DeleteLine();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.J)
                {
                    doc.JoinLines();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.U)
                {
                    doc.TransformUppercase();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.OemOpenBrackets)
                {
                    FoldCurrentBlock();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.OemCloseBrackets)
                {
                    UnfoldCurrentBlock();
                    e.Handled = true;
                    return;
                }
            }
            else
            {
                // Ctrl only
                if (e.Key == Key.U)
                {
                    doc.TransformLowercase();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.F)
                {
                    var sel = Editor.SelectedText;
                    doc.OpenFind(!string.IsNullOrEmpty(sel) ? sel : null);
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.H)
                {
                    var sel = Editor.SelectedText;
                    doc.OpenReplace(!string.IsNullOrEmpty(sel) ? sel : null);
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.G)
                {
                    doc.OpenGoToLine();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.M)
                {
                    doc.GoToMatchingBracket();
                    e.Handled = true;
                    return;
                }
            }
        }

        if (e.Key == Key.F3)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                doc.FindPrevious();
            }
            else
            {
                doc.FindNext();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (doc.IsFindVisible || doc.IsGoToLineVisible)
            {
                doc.CloseFind();
                doc.CancelGoToLine();
                e.Handled = true;
                return;
            }
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Delta.Y > 0)
            {
                if (Editor.FontSize < 36)
                    Editor.FontSize += 1;
            }
            else if (e.Delta.Y < 0)
            {
                if (Editor.FontSize > 8)
                    Editor.FontSize -= 1;
            }
            e.Handled = true;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is EditorDocumentViewModel doc)
        {
            // Sync TextDocument directly if needed
            if (Editor.Document != doc.TextDocument)
            {
                Editor.Document = doc.TextDocument;
            }

            // Provide closure for getting current selected text
            doc.GetSelectedTextFunc = () => Editor.SelectedText;

            // Subscribe to programmatic text selection and navigation requests
            doc.SelectTextRequested += (s, req) =>
            {
                if (Editor.Document != null && req.Offset >= 0 && req.Offset + req.Length <= Editor.Document.TextLength)
                {
                    Editor.Select(req.Offset, req.Length);
                    Editor.CaretOffset = req.Offset + req.Length;
                    Editor.ScrollTo(Editor.TextArea.Caret.Line, Editor.TextArea.Caret.Column);
                }
            };

            doc.FocusSearchBoxRequested += (s, ev) =>
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            };

            doc.FocusGoToLineRequested += (s, ev) =>
            {
                GoToLineBox.Focus();
                GoToLineBox.SelectAll();
            };

            doc.FocusEditorRequested += (s, ev) =>
            {
                Editor.Focus();
            };

            doc.CutRequested += (s, ev) => Editor.Cut();
            doc.CopyRequested += (s, ev) => Editor.Copy();
            doc.PasteRequested += (s, ev) => Editor.Paste();
            doc.SelectAllRequested += (s, ev) => Editor.SelectAll();
            doc.FoldCurrentBlockRequested += (s, ev) => FoldCurrentBlock();
            doc.UnfoldCurrentBlockRequested += (s, ev) => UnfoldCurrentBlock();
            doc.FoldAllRequested += (s, ev) => FoldAll();
            doc.UnfoldAllRequested += (s, ev) => UnfoldAll();

            // Hook scroll requests
            doc.ScrollToLineRequested += (s, line) =>
            {
                try
                {
                    Editor.ScrollToLine(line);
                    Editor.TextArea.Caret.Line = line;
                }
                catch (Exception)
                {
                    // Fallback
                }
            };

            // Attach breakpoint and execution marker renderer
            if (!_rendererAttached)
            {
                Editor.TextArea.TextView.BackgroundRenderers.Add(new DebugMarkerRenderer(
                    () => (DataContext as EditorDocumentViewModel)?.FilePath,
                    () => GetDebugService()
                ));
                Editor.TextArea.TextView.BackgroundRenderers.Add(new MultiCaretRenderer(_multiCaretManager, Editor));
                Editor.TextArea.TextView.BackgroundRenderers.Add(new CodeLensRenderer(() => DataContext as EditorDocumentViewModel, Editor));
                Editor.TextArea.TextView.BackgroundRenderers.Add(new IndentAndBracketRenderer(() => DataContext as EditorDocumentViewModel, Editor));
                _rendererAttached = true;
            }

            // Setup TextMate syntax highlighting
            SetupTextMateGrammar(doc);

            // Install code folding manager
            if (_foldingManager == null && Editor.TextArea != null)
            {
                _foldingManager = FoldingManager.Install(Editor.TextArea);
            }
            UpdateCodeFoldings();

            // Synchronize formatting options with document viewmodel
            Editor.Options.IndentationSize = doc.TabSize;
            Editor.Options.ConvertTabsToSpaces = doc.UseSpacesForTabs;
            Editor.IsReadOnly = doc.IsReadOnly;
            doc.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(EditorDocumentViewModel.TabSize))
                {
                    Editor.Options.IndentationSize = doc.TabSize;
                }
                else if (e.PropertyName == nameof(EditorDocumentViewModel.UseSpacesForTabs))
                {
                    Editor.Options.ConvertTabsToSpaces = doc.UseSpacesForTabs;
                }
                else if (e.PropertyName == nameof(EditorDocumentViewModel.IsReadOnly))
                {
                    Editor.IsReadOnly = doc.IsReadOnly;
                }
                else if (e.PropertyName == nameof(EditorDocumentViewModel.FilePath))
                {
                    SetupTextMateGrammar(doc);
                }
            };
        }
    }

    private void SetupTextMateGrammar(EditorDocumentViewModel doc)
    {
        try
        {
            if (_textMateInstallation == null)
            {
                var initialTheme = ThemeManager.CurrentTheme.Mode == AgduBugdu.App.Themes.AppThemeMode.SolarizedContrast
                    ? ThemeName.SolarizedDark
                    : ThemeName.DarkPlus;
                _registryOptions = new RegistryOptions(initialTheme);
                _textMateInstallation = Editor.InstallTextMate(_registryOptions);
            }

            var ext = Path.GetExtension(doc.FilePath);
            if (!string.IsNullOrEmpty(ext))
            {
                string? scope = null;
                var langService = GetLanguageService();
                var customLang = langService?.GetLanguageForFile(doc.FilePath);
                if (customLang != null && !string.IsNullOrEmpty(customLang.GrammarScope))
                {
                    scope = customLang.GrammarScope;
                }
                else
                {
                    var language = _registryOptions?.GetLanguageByExtension(ext);
                    if (language != null)
                    {
                        scope = _registryOptions?.GetScopeByLanguageId(language.Id);
                    }
                }

                if (!string.IsNullOrEmpty(scope))
                {
                    _textMateInstallation.SetGrammar(scope);
                }
            }
        }
        catch (Exception)
        {
            // Fallback gracefully if language grammar is unavailable
        }
    }

    private void OnTextAreaTextEntering(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
            return;

        // Replicate insertion across all multi-carets simultaneously
        if (_multiCaretManager.HasSecondaryCarets && Editor.Document != null)
        {
            var (newText, newPrimary, _) = _multiCaretManager.ApplyInsertion(Editor.Document.Text, e.Text, Editor.CaretOffset, Editor.SelectionStart, Editor.SelectionLength);
            Editor.Document.Text = newText;
            Editor.CaretOffset = Math.Clamp(newPrimary, 0, Editor.Document.TextLength);
            Editor.TextArea.TextView.InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (e.Text.Length != 1)
            return;

        char typed = e.Text[0];

        // 1. Overtype closing character if already positioned immediately before it
        if (BracketMatcher.IsClosingChar(typed) && Editor.Document != null && Editor.CaretOffset < Editor.Document.TextLength)
        {
            char nextChar = Editor.Document.GetCharAt(Editor.CaretOffset);
            if (nextChar == typed)
            {
                Editor.CaretOffset++;
                e.Handled = true;
                return;
            }
        }

        // 2. Auto-close opening brackets and quotes
        char closingPartner = BracketMatcher.GetAutoClosingPartner(typed);
        if (closingPartner != '\0' && Editor.Document != null)
        {
            int offset = Editor.CaretOffset;
            int selLen = Editor.SelectionLength;

            if (selLen > 0)
            {
                // Wrap selection: e.g. [selected_text] or "selected_text"
                string selText = Editor.SelectedText;
                Editor.Document.Replace(offset, selLen, $"{typed}{selText}{closingPartner}");
                Editor.Select(offset + 1, selLen);
                e.Handled = true;
                return;
            }

            // Normal insertion with auto-closed partner
            Editor.Document.Insert(offset, $"{typed}{closingPartner}");
            Editor.CaretOffset = offset + 1;
            e.Handled = true;
        }
    }

    private void UpdateCodeFoldings()
    {
        if (_foldingManager == null || Editor.Document == null)
            return;

        try
        {
            var ext = (DataContext as EditorDocumentViewModel)?.FilePath != null
                ? Path.GetExtension((DataContext as EditorDocumentViewModel)!.FilePath)
                : null;
            var detected = AgduBugdu.Core.Folding.CodeFoldingEngine.FindFoldings(Editor.Document.Text, ext);
            var foldings = new List<NewFolding>();

            foreach (var d in detected)
            {
                if (d.StartOffset >= 0 && d.EndOffset <= Editor.Document.TextLength && d.EndOffset > d.StartOffset)
                {
                    foldings.Add(new NewFolding(d.StartOffset, d.EndOffset)
                    {
                        Name = d.HeaderText,
                        DefaultClosed = d.IsDefaultFolded
                    });
                }
            }

            foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
            _foldingManager.UpdateFoldings(foldings, -1);
        }
        catch
        {
            // Ignore folding errors during dynamic edits
        }
    }

    public void FoldCurrentBlock()
    {
        if (_foldingManager == null || Editor.Document == null) return;
        int offset = Editor.CaretOffset;
        var section = _foldingManager.GetFoldingsContaining(offset).LastOrDefault();
        if (section != null)
        {
            section.IsFolded = true;
        }
    }

    public void UnfoldCurrentBlock()
    {
        if (_foldingManager == null || Editor.Document == null) return;
        int offset = Editor.CaretOffset;
        var section = _foldingManager.GetFoldingsContaining(offset).LastOrDefault();
        if (section != null)
        {
            section.IsFolded = false;
        }
    }

    public void FoldAll()
    {
        if (_foldingManager == null) return;
        foreach (var f in _foldingManager.AllFoldings)
        {
            f.IsFolded = true;
        }
    }

    public void UnfoldAll()
    {
        if (_foldingManager == null) return;
        foreach (var f in _foldingManager.AllFoldings)
        {
            f.IsFolded = false;
        }
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (DataContext is EditorDocumentViewModel doc)
        {
            doc.Line = Editor.TextArea.Caret.Line;
            doc.Column = Editor.TextArea.Caret.Column;
            doc.CaretOffset = Editor.CaretOffset;
            doc.SelectionStart = Editor.SelectionStart;
            doc.SelectedLength = Editor.SelectionLength;
        }
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (DataContext is EditorDocumentViewModel doc)
        {
            doc.SelectionStart = Editor.SelectionStart;
            doc.SelectedLength = Editor.SelectionLength;
            doc.CaretOffset = Editor.CaretOffset;
        }
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (DataContext is EditorDocumentViewModel doc)
        {
            doc.CheckModified();
            if (doc.IsFindVisible)
            {
                doc.UpdateSearchMatches();
            }
            UpdateCodeFoldings();
            UpdateMinimap();
            doc.RefreshBreadcrumbs();
            doc.RefreshCodeLenses();
            UpdateStickyScroll();
        }
    }

    private void UpdateStickyScroll()
    {
        if (DataContext is not EditorDocumentViewModel doc || !doc.ShowStickyScroll)
            return;

        try
        {
            var firstVisualLine = Editor.TextArea.TextView.VisualLines.FirstOrDefault();
            if (firstVisualLine != null)
            {
                int topVisibleLine = firstVisualLine.FirstDocumentLine.LineNumber;
                doc.UpdateStickyContext(topVisibleLine);
            }
            else
            {
                doc.IsStickyHeaderVisible = false;
            }
        }
        catch
        {
            doc.IsStickyHeaderVisible = false;
        }
    }

    private bool TryExpandSnippet()
    {
        if (DataContext is not EditorDocumentViewModel doc || Editor.Document == null)
            return false;

        int offset = Editor.CaretOffset;
        if (offset <= 0)
            return false;

        var line = Editor.Document.GetLineByOffset(offset);
        string lineText = Editor.Document.GetText(line.Offset, offset - line.Offset);

        int wordStart = lineText.Length - 1;
        while (wordStart >= 0 && (char.IsLetterOrDigit(lineText[wordStart]) || lineText[wordStart] == '_'))
        {
            wordStart--;
        }
        wordStart++;

        if (wordStart >= lineText.Length)
            return false;

        string prefix = lineText.Substring(wordStart);
        if (string.IsNullOrWhiteSpace(prefix))
            return false;

        var langDef = GetLanguageService()?.GetLanguageForFile(doc.FilePath);
        string langId = langDef?.Id ?? Path.GetExtension(doc.FilePath).TrimStart('.').ToLowerInvariant();

        var snippets = GetLanguageService()?.GetSnippets(langId);
        var match = snippets?.FirstOrDefault(s => string.Equals(s.Prefix, prefix, StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            match = BuiltInSnippets.GetDefaultSnippets().FirstOrDefault(s =>
                (string.IsNullOrEmpty(langId) || string.Equals(s.LanguageId, langId, StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(s.Prefix, prefix, StringComparison.OrdinalIgnoreCase));
        }

        if (match != null)
        {
            int prefixStart = line.Offset + wordStart;
            int prefixLength = prefix.Length;
            string indent = new string(lineText.TakeWhile(char.IsWhiteSpace).ToArray());

            string normalizedBody = match.Body.Replace("\r\n", "\n").Replace("\r", "\n");
            var bodyLines = normalizedBody.Split('\n');
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < bodyLines.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(indent);
                }
                sb.Append(bodyLines[i]);
                if (i < bodyLines.Length - 1)
                {
                    sb.AppendLine();
                }
            }

            string expanded = sb.ToString();
            int cursorRelOffset = expanded.IndexOf("$0", StringComparison.Ordinal);
            if (cursorRelOffset >= 0)
            {
                expanded = expanded.Remove(cursorRelOffset, 2);
            }
            else
            {
                int pStart = expanded.IndexOf("${", StringComparison.Ordinal);
                if (pStart >= 0)
                {
                    int pEnd = expanded.IndexOf('}', pStart);
                    if (pEnd > pStart)
                    {
                        string defaultVal = expanded.Substring(pStart + 2, pEnd - pStart - 2);
                        cursorRelOffset = pStart;
                        expanded = expanded.Remove(pStart, pEnd - pStart + 1).Insert(pStart, defaultVal);
                    }
                    else
                    {
                        cursorRelOffset = expanded.Length;
                    }
                }
                else
                {
                    cursorRelOffset = expanded.Length;
                }
            }

            Editor.Document.Replace(prefixStart, prefixLength, expanded);
            Editor.CaretOffset = prefixStart + cursorRelOffset;
            return true;
        }

        return false;
    }

    private void OnMinimapPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HandleMinimapPointer(e.GetPosition(MinimapCanvas));
        e.Handled = true;
    }

    private void OnMinimapPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.GetCurrentPoint(MinimapCanvas).Properties.IsLeftButtonPressed)
        {
            HandleMinimapPointer(e.GetPosition(MinimapCanvas));
            e.Handled = true;
        }
    }

    private void HandleMinimapPointer(Point pos)
    {
        if (Editor.Document == null || MinimapCanvas.Bounds.Height <= 0) return;
        int lineCount = Editor.Document.LineCount;
        if (lineCount <= 0) return;

        double ratio = Math.Clamp(pos.Y / MinimapCanvas.Bounds.Height, 0.0, 1.0);
        int targetLine = (int)Math.Round(ratio * (lineCount - 1)) + 1;
        targetLine = Math.Clamp(targetLine, 1, lineCount);

        Editor.ScrollToLine(targetLine);
        UpdateMinimap();
    }

    private void UpdateMinimap()
    {
        if (MinimapCanvas == null || Editor.Document == null) return;
        double canvasHeight = MinimapCanvas.Bounds.Height;
        double canvasWidth = MinimapCanvas.Bounds.Width;
        if (canvasHeight <= 10 || canvasWidth <= 10) return;

        MinimapCanvas.Children.Clear();

        int totalLines = Editor.Document.LineCount;
        if (totalLines <= 0) return;

        double lineH = Math.Max(1.0, Math.Min(3.0, canvasHeight / totalLines));
        double maxLineWidth = canvasWidth - 10;

        int step = Math.Max(1, totalLines / 250);
        for (int i = 1; i <= totalLines; i += step)
        {
            var docLine = Editor.Document.GetLineByNumber(i);
            int len = docLine.Length;
            if (len == 0) continue;

            double width = Math.Min(maxLineWidth, Math.Max(4, len * 0.8));
            double y = ((double)(i - 1) / totalLines) * canvasHeight;

            var lineBorder = new Border
            {
                Width = width,
                Height = Math.Max(1, lineH - 0.5),
                Background = new SolidColorBrush(Color.FromArgb(90, 150, 150, 150)),
                CornerRadius = new CornerRadius(0.5)
            };
            Canvas.SetLeft(lineBorder, 4);
            Canvas.SetTop(lineBorder, y);
            MinimapCanvas.Children.Add(lineBorder);
        }

        var textView = Editor.TextArea.TextView;
        if (textView != null && textView.DocumentHeight > 0)
        {
            double scrollRatio = textView.ScrollOffset.Y / Math.Max(1, textView.DocumentHeight);
            double visibleRatio = textView.Bounds.Height / Math.Max(1, textView.DocumentHeight);

            double viewTop = Math.Clamp(scrollRatio * canvasHeight, 0, canvasHeight - 16);
            double viewHeight = Math.Clamp(visibleRatio * canvasHeight, 16, canvasHeight);

            var viewRect = new Border
            {
                Width = canvasWidth,
                Height = viewHeight,
                Background = new SolidColorBrush(Color.FromArgb(45, 56, 189, 248)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(140, 56, 189, 248)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(viewRect, 0);
            Canvas.SetTop(viewRect, viewTop);
            MinimapCanvas.Children.Add(viewRect);
        }
    }

    private void OnEditorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            var point = e.GetPosition(Editor.TextArea.TextView);
            var pos = Editor.GetPositionFromPoint(point);
            if (pos.HasValue && Editor.Document != null)
            {
                int offset = Editor.Document.GetOffset(pos.Value.Location);
                _multiCaretManager.ToggleCaret(offset);
                Editor.TextArea.TextView.InvalidateVisual();
                e.Handled = true;
            }
        }
        else if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (_multiCaretManager.HasSecondaryCarets)
            {
                _multiCaretManager.Clear();
                Editor.TextArea.TextView.InvalidateVisual();
            }
        }
    }

    private void HandleSelectNextOccurrence()
    {
        if (Editor.Document == null) return;

        string targetText = Editor.SelectedText;
        if (string.IsNullOrEmpty(targetText))
        {
            int offset = Editor.CaretOffset;
            var line = Editor.Document.GetLineByOffset(offset);
            string lineText = Editor.Document.GetText(line.Offset, line.Length);
            int col = offset - line.Offset;

            int start = col;
            while (start > 0 && (char.IsLetterOrDigit(lineText[start - 1]) || lineText[start - 1] == '_'))
                start--;

            int end = col;
            while (end < lineText.Length && (char.IsLetterOrDigit(lineText[end]) || lineText[end] == '_'))
                end++;

            if (end > start)
            {
                Editor.Select(line.Offset + start, end - start);
                Editor.CaretOffset = line.Offset + end;
                return;
            }
        }
        else
        {
            if (_multiCaretManager.SelectNextOccurrence(Editor.Document.Text, targetText, Editor.SelectionStart, Editor.SelectionLength, out _))
            {
                Editor.TextArea.TextView.InvalidateVisual();
            }
        }
    }

    private void HandleAddColumnCaret(bool moveUp)
    {
        if (Editor.Document == null) return;
        var lines = Editor.Document.Text.Replace("\r\n", "\n").Split('\n');
        int currentLine = Editor.TextArea.Caret.Line;
        int currentCol = Editor.TextArea.Caret.Column;

        if (_multiCaretManager.AddColumnCaret(lines, currentLine, currentCol, moveUp, out _))
        {
            int nextLine = moveUp ? currentLine - 1 : currentLine + 1;
            if (nextLine >= 1 && nextLine <= Editor.Document.LineCount)
            {
                var lineSeg = Editor.Document.GetLineByNumber(nextLine);
                int newOffset = lineSeg.Offset + Math.Min(currentCol - 1, lineSeg.Length);
                Editor.CaretOffset = newOffset;
            }
            Editor.TextArea.TextView.InvalidateVisual();
        }
    }
}
