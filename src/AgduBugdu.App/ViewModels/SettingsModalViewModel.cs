using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AgduBugdu.Core.Keymap;
using AgduBugdu.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgduBugdu.App.ViewModels;

public partial class SettingsModalViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;

    public event EventHandler? SettingsApplied;
    public event EventHandler? KeymapChanged;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _activeCategory = "Editor"; // "Editor", "Terminal", "Git", "Theme", "Keymap"

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    // --- Editor Category ---
    [ObservableProperty]
    private string _fontFamily = "Cascadia Code, Consolas, Courier New, monospace";

    [ObservableProperty]
    private double _fontSize = 14;

    [ObservableProperty]
    private int _tabSize = 4;

    [ObservableProperty]
    private bool _insertSpaces = true;

    [ObservableProperty]
    private bool _wordWrap = false;

    [ObservableProperty]
    private bool _showMinimap = true;

    [ObservableProperty]
    private bool _showLineNumbers = true;

    [ObservableProperty]
    private string _cursorStyle = "Line";

    // --- Terminal Category ---
    [ObservableProperty]
    private string _shellProgram = OperatingSystem.IsWindows() ? "powershell.exe" : "bash";

    [ObservableProperty]
    private string _customArguments = OperatingSystem.IsWindows() ? "-NoLogo" : "";

    [ObservableProperty]
    private string _startDirectory = string.Empty;

    // --- Git Category ---
    [ObservableProperty]
    private int _autoFetchIntervalMinutes = 0;

    [ObservableProperty]
    private string _defaultBranchName = "master";

    [ObservableProperty]
    private string _diffStyle = "Unified";

    // --- Theme Category ---
    [ObservableProperty]
    private string _themeMode = "LonelyDark";

    [ObservableProperty]
    private string _accentColorHex = "#8B5CF6";

    // --- Keymap Category ---
    public ObservableCollection<CommandDefinition> AllCommands { get; } = new();
    public ObservableCollection<CommandDefinition> FilteredCommands { get; } = new();

    [ObservableProperty]
    private CommandDefinition? _selectedCommand;

    [ObservableProperty]
    private bool _isEditingShortcut;

    [ObservableProperty]
    private string _editingGestureText = string.Empty;

    [ObservableProperty]
    private string _editingConflictWarning = string.Empty;

    [ObservableProperty]
    private bool _hasEditingConflict;

    [ObservableProperty]
    private string _statusFeedback = string.Empty;

    public SettingsModalViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeDefaultCommands();
    }

    public void InitializeDefaultCommands()
    {
        AllCommands.Clear();

        void AddCmd(string id, string category, string title, string defaultGesture)
        {
            AllCommands.Add(new CommandDefinition
            {
                Id = id,
                Category = category,
                Title = title,
                DefaultGesture = defaultGesture,
                CurrentGesture = defaultGesture
            });
        }

        // File
        AddCmd("file.new", "File", "New File", "Ctrl+N");
        AddCmd("file.open", "File", "Open File...", "Ctrl+O");
        AddCmd("file.openFolder", "File", "Open Folder...", "Ctrl+K, Ctrl+O");
        AddCmd("file.save", "File", "Save File", "Ctrl+S");
        AddCmd("file.saveAs", "File", "Save As...", "Ctrl+Shift+S");
        AddCmd("file.closeTab", "File", "Close Active Tab", "Ctrl+W");
        AddCmd("file.reopenClosedTab", "File", "Reopen Closed Tab", "Ctrl+Shift+T");
        AddCmd("file.restoreSession", "File", "Restore Previous Session", "");

        // Edit
        AddCmd("edit.undo", "Edit", "Undo", "Ctrl+Z");
        AddCmd("edit.redo", "Edit", "Redo", "Ctrl+Y");
        AddCmd("edit.cut", "Edit", "Cut Selection", "Ctrl+X");
        AddCmd("edit.copy", "Edit", "Copy Selection", "Ctrl+C");
        AddCmd("edit.paste", "Edit", "Paste Clipboard", "Ctrl+V");
        AddCmd("edit.selectAll", "Edit", "Select All", "Ctrl+A");
        AddCmd("edit.find", "Edit", "Find in File", "Ctrl+F");
        AddCmd("edit.replace", "Edit", "Replace in File", "Ctrl+H");
        AddCmd("edit.goToLine", "Edit", "Go to Line / Column", "Ctrl+G");
        AddCmd("edit.goToMatchingBracket", "Edit", "Go to Matching Bracket", "Ctrl+M");
        AddCmd("edit.toggleLineComment", "Edit", "Toggle Line Comment", "Ctrl+OemQuestion");
        AddCmd("edit.toggleBlockComment", "Edit", "Toggle Block Comment", "Shift+Alt+A");
        AddCmd("edit.moveLineUp", "Edit", "Move Line Up", "Alt+Up");
        AddCmd("edit.moveLineDown", "Edit", "Move Line Down", "Alt+Down");
        AddCmd("edit.duplicateLineUp", "Edit", "Duplicate Line Up", "Shift+Alt+Up");
        AddCmd("edit.duplicateLineDown", "Edit", "Duplicate Line Down", "Shift+Alt+Down");
        AddCmd("edit.deleteLine", "Edit", "Delete Line", "Ctrl+Shift+K");
        AddCmd("edit.joinLines", "Edit", "Join Lines", "Ctrl+Shift+J");
        AddCmd("edit.transformUppercase", "Edit", "Transform to Uppercase", "Ctrl+Shift+U");
        AddCmd("edit.transformLowercase", "Edit", "Transform to Lowercase", "Ctrl+U");

        // View
        AddCmd("view.commandpalette", "View", "Command Palette", "Ctrl+P");
        AddCmd("view.settings", "View", "Open Settings / Preferences", "Ctrl+OemComma");
        AddCmd("view.toggleMinimap", "View", "Toggle Code Minimap", "Alt+M");
        AddCmd("view.toggleWordWrap", "View", "Toggle Word Wrap", "Alt+Z");
        AddCmd("view.splitRight", "View", "Split Editor Right", "Ctrl+OemBackslash");
        AddCmd("view.moveDocumentToOtherPane", "View", "Move Tab to Other Pane", "Ctrl+Alt+Right");
        AddCmd("view.toggleBottomPanel", "View", "Toggle Bottom Panel", "Ctrl+J");
        AddCmd("view.toggleExplorer", "View", "Focus Explorer", "Ctrl+Shift+E");
        AddCmd("view.toggleSearch", "View", "Focus Search in Files", "Ctrl+Shift+F");
        AddCmd("view.toggleGit", "View", "Focus Source Control", "Ctrl+Shift+G");
        AddCmd("view.outline", "View", "Focus Symbol Outline", "Ctrl+Shift+O");
        AddCmd("view.toggleTerminal", "View", "Focus Terminal", "Ctrl+OemTilde");
        AddCmd("view.toggleDebugger", "View", "Focus Run & Debug", "Ctrl+Shift+D");
        AddCmd("view.extensions", "View", "Manage Extensions...", "");

        // Git
        AddCmd("git.refresh", "Git", "Refresh Git Status", "");
        AddCmd("git.stageAll", "Git", "Stage All Changes", "");
        AddCmd("git.unstageAll", "Git", "Unstage All Changes", "");
        AddCmd("git.commit", "Git", "Commit Staged Changes", "");

        // Terminal
        AddCmd("terminal.restart", "Terminal", "Restart Terminal in Workspace", "Ctrl+Shift+OemTilde");

        // Debug
        AddCmd("debug.start", "Debug", "Start or Continue Debugging", "F5");
        AddCmd("debug.stepOver", "Debug", "Step Over", "F10");
        AddCmd("debug.stepInto", "Debug", "Step Into", "F11");
        AddCmd("debug.stop", "Debug", "Stop Debugging", "Shift+F5");

        // Help
        AddCmd("app.license", "Help", "Software License & Entitlements", "");
        AddCmd("app.checkForUpdates", "Help", "Check for Updates...", "");
        AddCmd("app.about", "Help", "About AgduBugdu", "");

        FilterKeymap();
    }

    [RelayCommand]
    public void Open(string? initialCategory = null)
    {
        if (!string.IsNullOrEmpty(initialCategory))
        {
            ActiveCategory = initialCategory;
        }

        var s = _settingsService.CurrentSettings;
        FontFamily = s.Editor.FontFamily;
        FontSize = s.Editor.FontSize;
        TabSize = s.Editor.TabSize;
        InsertSpaces = s.Editor.InsertSpaces;
        WordWrap = s.Editor.WordWrap;
        ShowMinimap = s.Editor.ShowMinimap;
        ShowLineNumbers = s.Editor.ShowLineNumbers;
        CursorStyle = s.Editor.CursorStyle;

        ShellProgram = s.Terminal.ShellProgram;
        CustomArguments = s.Terminal.CustomArguments;
        StartDirectory = s.Terminal.StartDirectory;

        AutoFetchIntervalMinutes = s.Git.AutoFetchIntervalMinutes;
        DefaultBranchName = s.Git.DefaultBranchName;
        DiffStyle = s.Git.DiffStyle;

        ThemeMode = s.Theme.ThemeMode;
        AccentColorHex = s.Theme.AccentColorHex;

        // Apply custom bindings
        foreach (var cmd in AllCommands)
        {
            if (s.Keymap.CustomBindings.TryGetValue(cmd.Id, out var customGesture))
            {
                cmd.CurrentGesture = customGesture;
            }
            else
            {
                cmd.CurrentGesture = cmd.DefaultGesture;
            }
        }

        KeymapHelper.UpdateConflicts(AllCommands);
        FilterKeymap();

        SearchFilter = string.Empty;
        StatusFeedback = string.Empty;
        IsEditingShortcut = false;
        IsVisible = true;
    }

    [RelayCommand]
    public void Close()
    {
        IsVisible = false;
        IsEditingShortcut = false;
    }

    [RelayCommand]
    public void SelectCategory(string category)
    {
        ActiveCategory = category;
        IsEditingShortcut = false;
    }

    partial void OnSearchFilterChanged(string value)
    {
        FilterKeymap();
    }

    private void FilterKeymap()
    {
        FilteredCommands.Clear();
        var q = SearchFilter?.Trim() ?? string.Empty;

        foreach (var cmd in AllCommands)
        {
            if (string.IsNullOrEmpty(q) ||
                cmd.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                cmd.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                cmd.CurrentGesture.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                FilteredCommands.Add(cmd);
            }
        }
    }

    [RelayCommand]
    public void SetTabSize(string sizeStr)
    {
        if (int.TryParse(sizeStr, out var size))
        {
            TabSize = size;
        }
    }

    [RelayCommand]
    public void SetAccentColor(string hex)
    {
        AccentColorHex = hex;
    }

    [RelayCommand]
    public void SetThemeMode(string mode)
    {
        ThemeMode = mode;
    }

    // --- Keymap Rebinding Actions ---
    [RelayCommand]
    public void StartEditShortcut(CommandDefinition? cmd)
    {
        if (cmd == null) return;
        SelectedCommand = cmd;
        EditingGestureText = cmd.CurrentGesture;
        ValidateEditingGesture();
        IsEditingShortcut = true;
    }

    partial void OnEditingGestureTextChanged(string value)
    {
        ValidateEditingGesture();
    }

    private void ValidateEditingGesture()
    {
        if (SelectedCommand == null) return;

        var normalized = KeymapHelper.NormalizeGesture(EditingGestureText);
        if (string.IsNullOrEmpty(normalized))
        {
            HasEditingConflict = false;
            EditingConflictWarning = string.Empty;
            return;
        }

        var conflicting = AllCommands.FirstOrDefault(c =>
            c.Id != SelectedCommand.Id &&
            KeymapHelper.NormalizeGesture(c.CurrentGesture) == normalized);

        if (conflicting != null)
        {
            HasEditingConflict = true;
            EditingConflictWarning = $"⚠️ Conflicts with '{conflicting.Title}' ({conflicting.CurrentGesture})";
        }
        else
        {
            HasEditingConflict = false;
            EditingConflictWarning = "✓ Valid gesture (no conflicts)";
        }
    }

    [RelayCommand]
    public void SaveShortcut()
    {
        if (SelectedCommand == null) return;

        SelectedCommand.CurrentGesture = EditingGestureText?.Trim() ?? string.Empty;
        KeymapHelper.UpdateConflicts(AllCommands);
        FilterKeymap();
        IsEditingShortcut = false;
        StatusFeedback = $"Shortcut for '{SelectedCommand.Title}' updated to '{SelectedCommand.CurrentGesture}'.";
    }

    [RelayCommand]
    public void CancelEditShortcut()
    {
        IsEditingShortcut = false;
    }

    [RelayCommand]
    public void ResetShortcut(CommandDefinition? cmd)
    {
        if (cmd == null) return;
        cmd.ResetToDefault();
        KeymapHelper.UpdateConflicts(AllCommands);
        FilterKeymap();
        StatusFeedback = $"Reset '{cmd.Title}' to default ({cmd.DefaultGesture}).";
    }

    [RelayCommand]
    public void ClearShortcut(CommandDefinition? cmd)
    {
        if (cmd == null) return;
        cmd.CurrentGesture = string.Empty;
        KeymapHelper.UpdateConflicts(AllCommands);
        FilterKeymap();
        StatusFeedback = $"Cleared shortcut for '{cmd.Title}'.";
    }

    [RelayCommand]
    public void ResetAllKeymaps()
    {
        foreach (var cmd in AllCommands)
        {
            cmd.ResetToDefault();
        }
        KeymapHelper.UpdateConflicts(AllCommands);
        FilterKeymap();
        StatusFeedback = "All keyboard shortcuts reset to factory defaults.";
    }

    [RelayCommand]
    public void SaveAndApply()
    {
        var settings = new AppSettings
        {
            Editor = new EditorSettings
            {
                FontFamily = FontFamily,
                FontSize = FontSize,
                TabSize = TabSize,
                InsertSpaces = InsertSpaces,
                WordWrap = WordWrap,
                ShowMinimap = ShowMinimap,
                ShowLineNumbers = ShowLineNumbers,
                CursorStyle = CursorStyle
            },
            Terminal = new TerminalSettings
            {
                ShellProgram = ShellProgram,
                CustomArguments = CustomArguments,
                StartDirectory = StartDirectory
            },
            Git = new GitSettings
            {
                AutoFetchIntervalMinutes = AutoFetchIntervalMinutes,
                DefaultBranchName = DefaultBranchName,
                DiffStyle = DiffStyle
            },
            Theme = new ThemeSettings
            {
                ThemeMode = ThemeMode,
                AccentColorHex = AccentColorHex
            },
            Keymap = new KeymapSettings()
        };

        // Persist custom key gestures
        foreach (var cmd in AllCommands)
        {
            if (cmd.IsModifiedFromDefault)
            {
                settings.Keymap.CustomBindings[cmd.Id] = cmd.CurrentGesture;
            }
        }

        _settingsService.SaveSettings(settings);
        SettingsApplied?.Invoke(this, EventArgs.Empty);
        KeymapChanged?.Invoke(this, EventArgs.Empty);

        IsVisible = false;
    }

    [RelayCommand]
    public void ResetCategoryDefaults()
    {
        var defaults = new AppSettings();
        switch (ActiveCategory)
        {
            case "Editor":
                FontFamily = defaults.Editor.FontFamily;
                FontSize = defaults.Editor.FontSize;
                TabSize = defaults.Editor.TabSize;
                InsertSpaces = defaults.Editor.InsertSpaces;
                WordWrap = defaults.Editor.WordWrap;
                ShowMinimap = defaults.Editor.ShowMinimap;
                ShowLineNumbers = defaults.Editor.ShowLineNumbers;
                CursorStyle = defaults.Editor.CursorStyle;
                StatusFeedback = "Editor settings reset to defaults.";
                break;
            case "Terminal":
                ShellProgram = defaults.Terminal.ShellProgram;
                CustomArguments = defaults.Terminal.CustomArguments;
                StartDirectory = defaults.Terminal.StartDirectory;
                StatusFeedback = "Terminal settings reset to defaults.";
                break;
            case "Git":
                AutoFetchIntervalMinutes = defaults.Git.AutoFetchIntervalMinutes;
                DefaultBranchName = defaults.Git.DefaultBranchName;
                DiffStyle = defaults.Git.DiffStyle;
                StatusFeedback = "Git settings reset to defaults.";
                break;
            case "Theme":
                ThemeMode = defaults.Theme.ThemeMode;
                AccentColorHex = defaults.Theme.AccentColorHex;
                StatusFeedback = "Theme settings reset to defaults.";
                break;
            case "Keymap":
                ResetAllKeymaps();
                break;
        }
    }

    public string ExportKeymapJson()
    {
        var model = new KeymapExportModel();
        foreach (var cmd in AllCommands)
        {
            if (!string.IsNullOrWhiteSpace(cmd.CurrentGesture))
            {
                model.Bindings[cmd.Id] = cmd.CurrentGesture;
            }
        }

        return JsonSerializer.Serialize(model, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    public bool ImportKeymapJson(string json)
    {
        try
        {
            var model = JsonSerializer.Deserialize<KeymapExportModel>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (model?.Bindings != null)
            {
                foreach (var cmd in AllCommands)
                {
                    if (model.Bindings.TryGetValue(cmd.Id, out var gesture))
                    {
                        cmd.CurrentGesture = gesture;
                    }
                }
                KeymapHelper.UpdateConflicts(AllCommands);
                FilterKeymap();
                StatusFeedback = $"Imported {model.Bindings.Count} shortcut bindings successfully.";
                return true;
            }
        }
        catch (Exception ex)
        {
            StatusFeedback = $"Error importing keymap: {ex.Message}";
        }
        return false;
    }
}
