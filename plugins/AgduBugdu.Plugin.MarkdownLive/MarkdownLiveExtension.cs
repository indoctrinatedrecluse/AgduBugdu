using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Plugin.MarkdownLive;

public class MarkdownLiveExtension : IExtension
{
    private IExtensionContext? _context;
    private MarkdownPreviewViewModel? _viewModel;

    public string Id => "agdubugdu.plugin.markdownlive";
    public string Name => "Markdown Live Viewer";
    public string Version => "1.0.0";

    public MarkdownPreviewViewModel? ViewModel => _viewModel;

    public string? GetPreviewDocument(string filePath)
    {
        _viewModel?.UpdateDocument(filePath);
        return _viewModel?.HtmlPreview;
    }

    public void Initialize(IExtensionContext context)
    {
        _context = context;
        _viewModel = new MarkdownPreviewViewModel();

        // 1. Register command in host Command Palette
        _context.Commands.RegisterCommand(
            "markdown.preview",
            "Markdown: Toggle Live Preview",
            () =>
            {
                if (_context.EditorService.ActiveDocumentPath != null)
                {
                    _viewModel.UpdateDocument(_context.EditorService.ActiveDocumentPath);
                }
                return Task.CompletedTask;
            }
        );

        // 2. Register tool window for dock manager
        _context.ToolWindows.RegisterToolWindow(
            "markdown.preview.tool",
            "Markdown Live Preview",
            () => _viewModel,
            vm => _viewModel
        );

        // 3. Listen to editor document lifecycle
        _context.EditorService.DocumentOpened += (s, e) =>
        {
            if (e.FilePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.UpdateDocument(e.FilePath);
            }
        };

        _context.EditorService.DocumentSaved += (s, e) =>
        {
            if (e.FilePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.UpdateDocument(e.FilePath);
            }
        };

        _context.Log("Markdown Live Viewer extension initialized successfully.");
    }

    public Task ActivateAsync()
    {
        return Task.CompletedTask;
    }

    public Task DeactivateAsync()
    {
        return Task.CompletedTask;
    }
}

public class MarkdownPreviewViewModel
{
    public string Title => "Markdown Live Preview";
    public string HtmlPreview { get; private set; } = "<h3>No markdown file loaded</h3>";

    public event EventHandler? ContentUpdated;

    public void UpdateDocument(string filePath)
    {
        try
        {
            if (System.IO.File.Exists(filePath))
            {
                var markdownText = System.IO.File.ReadAllText(filePath);
                HtmlPreview = ConvertMarkdownToHtml(markdownText);
                ContentUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            HtmlPreview = $"<p style='color:red;'>Error parsing markdown: {ex.Message}</p>";
            ContentUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string ConvertMarkdownToHtml(string md)
    {
        // Lightweight, self-contained Markdown-to-HTML parser with XSS protection
        var lines = md.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sb = new System.Text.StringBuilder();
        bool inCodeBlock = false;

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.Trim();

            // Code block toggle
            if (trimmed.StartsWith("```"))
            {
                if (!inCodeBlock)
                {
                    inCodeBlock = true;
                    var lang = System.Net.WebUtility.HtmlEncode(trimmed.Length > 3 ? trimmed.Substring(3).Trim() : string.Empty);
                    sb.AppendLine($"<pre><code class=\"{lang}\">");
                }
                else
                {
                    inCodeBlock = false;
                    sb.AppendLine("</code></pre>");
                }
                continue;
            }

            if (inCodeBlock)
            {
                sb.AppendLine(System.Net.WebUtility.HtmlEncode(rawLine));
                continue;
            }

            if (trimmed.StartsWith("#### "))
            {
                var content = System.Net.WebUtility.HtmlEncode(trimmed.Substring(5));
                sb.AppendLine($"<h4>{content}</h4>");
            }
            else if (trimmed.StartsWith("### "))
            {
                var content = System.Net.WebUtility.HtmlEncode(trimmed.Substring(4));
                sb.AppendLine($"<h3>{content}</h3>");
            }
            else if (trimmed.StartsWith("## "))
            {
                var content = System.Net.WebUtility.HtmlEncode(trimmed.Substring(3));
                sb.AppendLine($"<h2>{content}</h2>");
            }
            else if (trimmed.StartsWith("# "))
            {
                var content = System.Net.WebUtility.HtmlEncode(trimmed.Substring(2));
                sb.AppendLine($"<h1>{content}</h1>");
            }
            else if (trimmed.StartsWith("> "))
            {
                var content = System.Net.WebUtility.HtmlEncode(trimmed.Substring(2));
                sb.AppendLine($"<blockquote>{content}</blockquote>");
            }
            else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                var content = System.Net.WebUtility.HtmlEncode(trimmed.Substring(2));
                sb.AppendLine($"<li>{content}</li>");
            }
            else if (string.IsNullOrWhiteSpace(trimmed))
            {
                sb.AppendLine("<br/>");
            }
            else
            {
                var encoded = System.Net.WebUtility.HtmlEncode(trimmed);
                // Simple bold and code formatting
                var parsed = Regex.Replace(encoded, @"\*\*(.+?)\*\*", "<b>$1</b>");
                parsed = Regex.Replace(parsed, @"`(.+?)`", "<code>$1</code>");
                sb.AppendLine($"<p>{parsed}</p>");
            }
        }

        if (inCodeBlock)
        {
            sb.AppendLine("</code></pre>");
        }

        return sb.ToString();
    }
}
