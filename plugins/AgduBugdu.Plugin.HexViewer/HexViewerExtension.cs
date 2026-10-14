using System;
using System.IO;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Plugin.HexViewer;

public class HexViewerExtension : IExtension
{
    private IExtensionContext? _context;
    private HexViewerViewModel? _viewModel;

    public string Id => "agdubugdu.plugin.hexviewer";
    public string Name => "Binary & Hex Inspector";
    public string Version => "1.0.0";

    public HexViewerViewModel? ViewModel => _viewModel;

    public void Initialize(IExtensionContext context)
    {
        _context = context;
        _viewModel = new HexViewerViewModel();

        // 1. Register commands
        _context.Commands.RegisterCommand(
            "hex.view",
            "Hex Viewer: Inspect Current File in Hex",
            () =>
            {
                string? path = _context.EditorService.ActiveDocumentPath;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    _viewModel.LoadFile(path);
                    _context.Log($"[HexViewer] Loaded {Path.GetFileName(path)} ({_viewModel.Stats.SummaryText})");
                }
                else
                {
                    _context.Log("[HexViewer] No active file on disk to inspect.", "Warning");
                }
                return Task.CompletedTask;
            },
            "Ctrl+Shift+H"
        );

        _context.Commands.RegisterCommand(
            "hex.export",
            "Hex Viewer: Export Current Hex Dump to Editor",
            () =>
            {
                if (!string.IsNullOrEmpty(_viewModel.FormattedDumpText))
                {
                    string dumpName = $"{_viewModel.FileName}.hex";
                    _context.EditorService.NewDocument(dumpName, _viewModel.FormattedDumpText);
                    _context.Log($"[HexViewer] Exported dump to {dumpName}");
                }
                return Task.CompletedTask;
            }
        );

        // 2. Register Tool Window
        _context.ToolWindows.RegisterToolWindow(
            "hex.viewer.tool",
            "Hex Viewer",
            () => _viewModel,
            vm => _viewModel,
            ToolDockPosition.Bottom
        );

        // 3. Register Language & File Association
        _context.Languages.RegisterLanguage(new LanguageDefinition(
            "hex",
            "Hex Dump",
            new[] { ".hex", ".dump" },
            "source.hex",
            ";",
            null,
            null
        ));

        _context.Log("Hex Viewer extension initialized.");
    }

    public Task ActivateAsync() => Task.CompletedTask;
    public Task DeactivateAsync() => Task.CompletedTask;
}
