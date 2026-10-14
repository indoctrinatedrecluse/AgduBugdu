using System;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Plugin.ColorPicker;

public class ColorPickerExtension : IExtension
{
    private IExtensionContext? _context;
    private ColorPickerViewModel? _viewModel;

    public string Id => "agdubugdu.plugin.colorpicker";
    public string Name => "Color Palette & Picker";
    public string Version => "1.0.0";

    public ColorPickerViewModel? ViewModel => _viewModel;

    public void Initialize(IExtensionContext context)
    {
        _context = context;
        _viewModel = new ColorPickerViewModel();

        // 1. Register Commands
        _context.Commands.RegisterCommand(
            "color.insertHex",
            "Color Picker: Insert Hex Color Code (#RRGGBB)",
            () =>
            {
                if (_viewModel != null)
                {
                    _context.EditorService.InsertText(_viewModel.Hex6);
                    _context.Log($"[ColorPicker] Inserted {_viewModel.Hex6} into active document.");
                }
                return Task.CompletedTask;
            },
            "Ctrl+Alt+C"
        );

        _context.Commands.RegisterCommand(
            "color.insertRgb",
            "Color Picker: Insert RGB Color Code (rgb(...))",
            () =>
            {
                if (_viewModel != null)
                {
                    _context.EditorService.InsertText(_viewModel.RgbText);
                    _context.Log($"[ColorPicker] Inserted {_viewModel.RgbText} into active document.");
                }
                return Task.CompletedTask;
            }
        );

        _context.Commands.RegisterCommand(
            "color.insertHsl",
            "Color Picker: Insert HSL Color Code (hsl(...))",
            () =>
            {
                if (_viewModel != null)
                {
                    _context.EditorService.InsertText(_viewModel.HslText);
                    _context.Log($"[ColorPicker] Inserted {_viewModel.HslText} into active document.");
                }
                return Task.CompletedTask;
            }
        );

        // 2. Register Tool Window
        _context.ToolWindows.RegisterToolWindow(
            "color.picker.tool",
            "Color Picker",
            () => _viewModel,
            vm => _viewModel,
            ToolDockPosition.Left
        );

        _context.Log("Color Picker extension initialized.");
    }

    public Task ActivateAsync() => Task.CompletedTask;
    public Task DeactivateAsync() => Task.CompletedTask;
}
