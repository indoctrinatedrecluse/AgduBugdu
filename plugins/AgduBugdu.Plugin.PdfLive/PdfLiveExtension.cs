using System;
using System.IO;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Plugin.PdfLive;

public class PdfLiveExtension : IExtension
{
    private IExtensionContext? _context;
    private PdfLiveViewModel? _viewModel;

    public string Id => "agdubugdu.plugin.pdflive";
    public string Name => "Live PDF Viewer & Editor";
    public string Version => "1.0.0";

    public PdfLiveViewModel? ViewModel => _viewModel;

    public string? GetPreviewDocument(string filePath)
    {
        _viewModel?.LoadFile(filePath);
        return _viewModel?.ToFormattedText();
    }

    public void Initialize(IExtensionContext context)
    {
        _context = context;
        _viewModel = new PdfLiveViewModel();

        // 1. Register commands in host Command Palette
        _context.Commands.RegisterCommand(
            "pdf.view",
            "PDF: View Document in Live Viewer",
            () =>
            {
                if (_context.EditorService.ActiveDocumentPath != null &&
                    _context.EditorService.ActiveDocumentPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    _viewModel.LoadFile(_context.EditorService.ActiveDocumentPath);
                }
                return Task.CompletedTask;
            }
        );

        _context.Commands.RegisterCommand(
            "pdf.nextPage",
            "PDF: Next Page",
            () =>
            {
                _viewModel.NextPage();
                return Task.CompletedTask;
            }
        );

        _context.Commands.RegisterCommand(
            "pdf.prevPage",
            "PDF: Previous Page",
            () =>
            {
                _viewModel.PreviousPage();
                return Task.CompletedTask;
            }
        );

        _context.Commands.RegisterCommand(
            "pdf.rotate",
            "PDF: Rotate Current Page 90° Clockwise",
            () =>
            {
                _viewModel.RotateCurrentPage(90);
                return Task.CompletedTask;
            }
        );

        _context.Commands.RegisterCommand(
            "pdf.deletePage",
            "PDF: Delete Current Page",
            () =>
            {
                _viewModel.DeleteCurrentPage();
                return Task.CompletedTask;
            }
        );

        _context.Commands.RegisterCommand(
            "pdf.watermark",
            "PDF: Add 'CONFIDENTIAL' Watermark",
            () =>
            {
                _viewModel.AddWatermark("CONFIDENTIAL", WatermarkPosition.CenterDiagonal);
                return Task.CompletedTask;
            }
        );

        // 2. Register tool window for dock manager
        _context.ToolWindows.RegisterToolWindow(
            "pdf.live.tool",
            "Live PDF Viewer & Editor",
            () => _viewModel,
            vm => _viewModel
        );

        // 3. Listen to editor document lifecycle
        _context.EditorService.DocumentOpened += (s, e) =>
        {
            if (e.FilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.LoadFile(e.FilePath);
            }
        };

        _context.EditorService.DocumentSaved += (s, e) =>
        {
            if (e.FilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.LoadFile(e.FilePath);
            }
        };

        _context.Log("Live PDF Viewer & Editor extension initialized successfully.");
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
