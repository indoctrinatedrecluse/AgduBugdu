using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using AgduBugdu.App.ViewModels.Documents;
using TextMateSharp.Grammars;

namespace AgduBugdu.App.Views.Documents;

public partial class EditorDocumentView : UserControl
{
    private TextMate.Installation? _textMateInstallation;
    private RegistryOptions? _registryOptions;

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

        // Ctrl + MouseWheel to zoom font size
        Editor.PointerWheelChanged += OnPointerWheelChanged;
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
            doc.IsModified = true;
            if (!doc.Title.EndsWith("*", StringComparison.Ordinal))
            {
                doc.Title = doc.FileName + "*";
            }
        }
    }
}
