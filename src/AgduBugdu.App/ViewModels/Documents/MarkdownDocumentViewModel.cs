using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Documents;

public abstract class MarkdownRenderBlock
{
}

public class MarkdownHeadingBlock : MarkdownRenderBlock
{
    public string Text { get; init; } = string.Empty;
    public int Level { get; init; } = 1;

    public double FontSize => Level switch
    {
        1 => 22,
        2 => 18,
        3 => 15,
        _ => 13
    };

    public FontWeight Weight => Level <= 2 ? FontWeight.Bold : FontWeight.SemiBold;

    public Thickness Margin => Level switch
    {
        1 => new Thickness(0, 16, 0, 8),
        2 => new Thickness(0, 12, 0, 6),
        _ => new Thickness(0, 8, 0, 4)
    };

    public IBrush Color => Level switch
    {
        1 => new SolidColorBrush(Avalonia.Media.Color.Parse("#4EC9B0")),
        2 => new SolidColorBrush(Avalonia.Media.Color.Parse("#569CD6")),
        _ => new SolidColorBrush(Avalonia.Media.Color.Parse("#DCDCDC"))
    };
}

public class MarkdownParagraphBlock : MarkdownRenderBlock
{
    public string Text { get; init; } = string.Empty;
}

public class MarkdownCodeBlock : MarkdownRenderBlock
{
    public string Language { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
}

public class MarkdownQuoteBlock : MarkdownRenderBlock
{
    public string Text { get; init; } = string.Empty;
}

public class MarkdownListItemBlock : MarkdownRenderBlock
{
    public string Marker { get; init; } = "•";
    public string Text { get; init; } = string.Empty;
}

public class MarkdownDividerBlock : MarkdownRenderBlock
{
}

public partial class MarkdownDocumentViewModel : Document
{
    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _rawMarkdown = string.Empty;

    [ObservableProperty]
    private bool _isVisualMode = true;

    [ObservableProperty]
    private string _statusSummary = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public ObservableCollection<MarkdownRenderBlock> Blocks { get; } = new();

    public MarkdownDocumentViewModel()
    {
        Id = "markdown.document";
        Title = "Markdown Preview";
    }

    public void LoadFile(string path)
    {
        FilePath = path;
        FileName = Path.GetFileName(path);
        Title = $"Preview: {FileName}";
        Reload();
    }

    [RelayCommand]
    public void ToggleViewMode()
    {
        IsVisualMode = !IsVisualMode;
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
            HasError = false;
            ErrorMessage = string.Empty;

            RawMarkdown = File.ReadAllText(FilePath);
            ParseMarkdown(RawMarkdown);
            StatusSummary = $"{Blocks.Count} blocks rendered • {RawMarkdown.Length:N0} bytes";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Error loading Markdown: {ex.Message}";
        }
    }

    private void ParseMarkdown(string md)
    {
        Blocks.Clear();
        var lines = md.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        bool inCodeBlock = false;
        string codeLang = string.Empty;
        var codeBuilder = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.Trim();

            // 1. Code blocks (```)
            if (trimmed.StartsWith("```"))
            {
                if (!inCodeBlock)
                {
                    inCodeBlock = true;
                    codeLang = trimmed.Length > 3 ? trimmed.Substring(3).Trim() : "text";
                    codeBuilder.Clear();
                }
                else
                {
                    inCodeBlock = false;
                    Blocks.Add(new MarkdownCodeBlock
                    {
                        Language = codeLang,
                        Code = codeBuilder.ToString().TrimEnd()
                    });
                    codeBuilder.Clear();
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBuilder.AppendLine(rawLine);
                continue;
            }

            // 2. Horizontal divider
            if (trimmed == "---" || trimmed == "***" || trimmed == "___")
            {
                Blocks.Add(new MarkdownDividerBlock());
                continue;
            }

            // 3. Headings
            if (trimmed.StartsWith("#### "))
            {
                Blocks.Add(new MarkdownHeadingBlock { Text = trimmed.Substring(5), Level = 4 });
            }
            else if (trimmed.StartsWith("### "))
            {
                Blocks.Add(new MarkdownHeadingBlock { Text = trimmed.Substring(4), Level = 3 });
            }
            else if (trimmed.StartsWith("## "))
            {
                Blocks.Add(new MarkdownHeadingBlock { Text = trimmed.Substring(3), Level = 2 });
            }
            else if (trimmed.StartsWith("# "))
            {
                Blocks.Add(new MarkdownHeadingBlock { Text = trimmed.Substring(2), Level = 1 });
            }
            // 4. Blockquotes
            else if (trimmed.StartsWith("> "))
            {
                Blocks.Add(new MarkdownQuoteBlock { Text = trimmed.Substring(2) });
            }
            // 5. Unordered / ordered lists
            else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ "))
            {
                Blocks.Add(new MarkdownListItemBlock { Marker = "•", Text = trimmed.Substring(2) });
            }
            else if (char.IsDigit(trimmed.FirstOrDefault()) && trimmed.Contains(". "))
            {
                var dotIdx = trimmed.IndexOf(". ");
                var marker = trimmed.Substring(0, dotIdx + 1);
                Blocks.Add(new MarkdownListItemBlock { Marker = marker, Text = trimmed.Substring(dotIdx + 2) });
            }
            // 6. Paragraphs
            else if (!string.IsNullOrWhiteSpace(trimmed))
            {
                Blocks.Add(new MarkdownParagraphBlock { Text = trimmed });
            }
        }

        if (inCodeBlock)
        {
            Blocks.Add(new MarkdownCodeBlock
            {
                Language = codeLang,
                Code = codeBuilder.ToString().TrimEnd()
            });
        }
    }
}
