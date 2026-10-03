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
using AgduBugdu.PluginContracts;
using TextMateSharp.Grammars;

namespace AgduBugdu.App.Views.Documents;

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

public partial class EditorDocumentView : UserControl
{
    private TextMate.Installation? _textMateInstallation;
    private RegistryOptions? _registryOptions;
    private bool _rendererAttached;

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
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not EditorDocumentViewModel doc) return;

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

        if (e.Key == Key.F9)
        {
            ToggleBreakpoint();
            e.Handled = true;
            return;
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
                _rendererAttached = true;
            }

            // Setup TextMate syntax highlighting
            SetupTextMateGrammar(doc);
        }
    }

    private void SetupTextMateGrammar(EditorDocumentViewModel doc)
    {
        try
        {
            if (_textMateInstallation == null)
            {
                _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
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
        }
    }
}
