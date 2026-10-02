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
        Editor.TextChanged += OnTextChanged;
        Editor.KeyDown += OnEditorKeyDown;

        // Ctrl + MouseWheel to zoom font size
        Editor.PointerWheelChanged += OnPointerWheelChanged;

        if (ToggleBreakpointMenuItem != null)
        {
            ToggleBreakpointMenuItem.Click += (s, e) => ToggleBreakpoint();
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
        if (e.Key == Key.F9)
        {
            ToggleBreakpoint();
            e.Handled = true;
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

            doc.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(EditorDocumentViewModel.TextDocument))
                {
                    if (Editor.Document != doc.TextDocument)
                    {
                        Editor.Document = doc.TextDocument;
                    }
                }
            };

            doc.ScrollToLineRequested += (s, line) =>
            {
                try
                {
                    if (line > 0 && line <= Editor.Document.LineCount)
                    {
                        Editor.ScrollToLine(line);
                        Editor.TextArea.Caret.Line = line;
                        Editor.TextArea.Caret.Column = doc.Column > 0 ? doc.Column : 1;
                    }
                }
                catch { }
            };

            if (doc.Line > 1)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        if (doc.Line <= Editor.Document.LineCount)
                        {
                            Editor.ScrollToLine(doc.Line);
                            Editor.TextArea.Caret.Line = doc.Line;
                            Editor.TextArea.Caret.Column = doc.Column > 0 ? doc.Column : 1;
                        }
                    }
                    catch { }
                });
            }

            if (!_rendererAttached)
            {
                Editor.TextArea.TextView.BackgroundRenderers.Add(
                    new DebugMarkerRenderer(
                        () => (DataContext as EditorDocumentViewModel)?.FilePath,
                        GetDebugService
                    )
                );
                _rendererAttached = true;

                var debugService = GetDebugService();
                if (debugService != null)
                {
                    debugService.BreakpointChanged += (s, args) =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => Editor.TextArea.TextView.InvalidateVisual());
                    };
                    debugService.StateChanged += (s, args) =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => Editor.TextArea.TextView.InvalidateVisual());
                    };
                }
            }
        }
        SetupSyntaxHighlighting();
    }

    private void SetupSyntaxHighlighting()
    {
        if (DataContext is not EditorDocumentViewModel doc)
            return;

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
                var language = _registryOptions?.GetLanguageByExtension(ext);
                if (language != null)
                {
                    _textMateInstallation.SetGrammar(_registryOptions?.GetScopeByLanguageId(language.Id));
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
        }
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (DataContext is EditorDocumentViewModel doc)
        {
            doc.CheckModified();
        }
    }
}
