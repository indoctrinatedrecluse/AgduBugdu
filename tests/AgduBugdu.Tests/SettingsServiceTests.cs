using System;
using System.IO;
using System.Linq;
using AgduBugdu.App.ViewModels;
using AgduBugdu.Core.Keymap;
using AgduBugdu.Core.Settings;
using AgduBugdu.Infrastructure.Settings;
using Xunit;

namespace AgduBugdu.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempSettingsPath;

    public SettingsServiceTests()
    {
        _tempSettingsPath = Path.Combine(Path.GetTempPath(), $"agdubugdu_settings_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempSettingsPath))
            {
                File.Delete(_tempSettingsPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void SettingsService_DefaultSettings_AreSensible()
    {
        var service = new SettingsService(_tempSettingsPath);
        var s = service.CurrentSettings;

        Assert.NotNull(s);
        Assert.Equal(14.0, s.Editor.FontSize);
        Assert.Equal(4, s.Editor.TabSize);
        Assert.True(s.Editor.InsertSpaces);
        Assert.False(s.Editor.WordWrap);
        Assert.True(s.Editor.ShowMinimap);
        Assert.True(s.Editor.ShowLineNumbers);
        Assert.Contains("Cascadia Code", s.Editor.FontFamily);
        Assert.Equal("master", s.Git.DefaultBranchName);
        Assert.Equal(0, s.Git.AutoFetchIntervalMinutes);
        Assert.Equal("LonelyDark", s.Theme.ThemeMode);
    }

    [Fact]
    public void SettingsService_SaveAndLoad_RoundTripsAccurately()
    {
        var service = new SettingsService(_tempSettingsPath);

        var updated = new AppSettings
        {
            Editor = new EditorSettings
            {
                FontFamily = "Consolas",
                FontSize = 18.0,
                TabSize = 2,
                InsertSpaces = false,
                WordWrap = true,
                ShowMinimap = false,
                ShowLineNumbers = false,
                CursorStyle = "Block"
            },
            Terminal = new TerminalSettings
            {
                ShellProgram = "cmd.exe",
                CustomArguments = "/k echo Hello",
                StartDirectory = "C:\\Temp"
            },
            Git = new GitSettings
            {
                DefaultBranchName = "main",
                AutoFetchIntervalMinutes = 15,
                DiffStyle = "SideBySide"
            },
            Theme = new ThemeSettings
            {
                ThemeMode = "SolarizedContrast",
                AccentColorHex = "#2AA198"
            },
            Keymap = new KeymapSettings()
        };
        updated.Keymap.CustomBindings["file.save"] = "Ctrl+Shift+S";

        bool eventFired = false;
        service.SettingsChanged += (s, e) => { eventFired = true; };

        service.SaveSettings(updated);

        Assert.True(eventFired);
        Assert.True(File.Exists(_tempSettingsPath));

        // Create new service instance to reload from disk
        var service2 = new SettingsService(_tempSettingsPath);
        var loaded = service2.CurrentSettings;

        Assert.Equal("Consolas", loaded.Editor.FontFamily);
        Assert.Equal(18.0, loaded.Editor.FontSize);
        Assert.Equal(2, loaded.Editor.TabSize);
        Assert.False(loaded.Editor.InsertSpaces);
        Assert.True(loaded.Editor.WordWrap);
        Assert.False(loaded.Editor.ShowMinimap);
        Assert.False(loaded.Editor.ShowLineNumbers);
        Assert.Equal("cmd.exe", loaded.Terminal.ShellProgram);
        Assert.Equal("main", loaded.Git.DefaultBranchName);
        Assert.Equal(15, loaded.Git.AutoFetchIntervalMinutes);
        Assert.Equal("SolarizedContrast", loaded.Theme.ThemeMode);
        Assert.Equal("#2AA198", loaded.Theme.AccentColorHex);
        Assert.True(loaded.Keymap.CustomBindings.ContainsKey("file.save"));
        Assert.Equal("Ctrl+Shift+S", loaded.Keymap.CustomBindings["file.save"]);
    }

    [Fact]
    public void SettingsService_SanitizesExtremeValues()
    {
        var service = new SettingsService(_tempSettingsPath);
        var badSettings = new AppSettings
        {
            Editor = new EditorSettings
            {
                FontSize = 999.0, // should clamp to 48
                TabSize = -5,     // should clamp to 1
                FontFamily = ""   // should default to Cascadia Code
            },
            Git = new GitSettings
            {
                AutoFetchIntervalMinutes = -10,
                DefaultBranchName = ""
            }
        };

        service.SaveSettings(badSettings);

        var loaded = service.CurrentSettings;
        Assert.Equal(48.0, loaded.Editor.FontSize);
        Assert.Equal(1, loaded.Editor.TabSize);
        Assert.False(string.IsNullOrWhiteSpace(loaded.Editor.FontFamily));
        Assert.Equal(0, loaded.Git.AutoFetchIntervalMinutes);
        Assert.Equal("master", loaded.Git.DefaultBranchName);
    }

    [Fact]
    public void SettingsService_ResetToDefaults_RestoresOriginalState()
    {
        var service = new SettingsService(_tempSettingsPath);
        service.SaveSettings(new AppSettings
        {
            Editor = new EditorSettings { FontSize = 32.0, TabSize = 8 }
        });

        Assert.Equal(32.0, service.CurrentSettings.Editor.FontSize);

        service.ResetToDefaults();
        Assert.Equal(14.0, service.CurrentSettings.Editor.FontSize);
        Assert.Equal(4, service.CurrentSettings.Editor.TabSize);
    }

    [Fact]
    public void KeymapHelper_DetectsConflictsProperly()
    {
        var cmd1 = new CommandDefinition { Id = "file.save", Title = "Save", CurrentGesture = "Ctrl+S" };
        var cmd2 = new CommandDefinition { Id = "file.saveAs", Title = "Save As", CurrentGesture = "ctrl + s " };
        var cmd3 = new CommandDefinition { Id = "edit.find", Title = "Find", CurrentGesture = "Ctrl+F" };

        var list = new[] { cmd1, cmd2, cmd3 };
        KeymapHelper.UpdateConflicts(list);

        Assert.True(cmd1.HasConflict);
        Assert.True(cmd2.HasConflict);
        Assert.False(cmd3.HasConflict);
        Assert.Contains("Save As", cmd1.ConflictCommandName);
        Assert.Contains("Save", cmd2.ConflictCommandName);
    }

    [Fact]
    public void KeymapHelper_NormalizesWhitespaceAndCasing()
    {
        Assert.Equal("CTRL+SHIFT+P", KeymapHelper.NormalizeGesture("  ctrl + shift + p  "));
        Assert.Equal("ALT+Z", KeymapHelper.NormalizeGesture("alt+z"));
        Assert.Equal(string.Empty, KeymapHelper.NormalizeGesture(null));
        Assert.Equal(string.Empty, KeymapHelper.NormalizeGesture("   "));
    }

    [Fact]
    public void SettingsModalViewModel_ExportAndImportKeymap_WorksCorrectly()
    {
        var service = new SettingsService(_tempSettingsPath);
        var vm = new SettingsModalViewModel(service);
        vm.Open("Keymap");

        // Change a command shortcut
        var saveCmd = vm.AllCommands.First(c => c.Id == "file.save");
        saveCmd.CurrentGesture = "Ctrl+Alt+S";

        var json = vm.ExportKeymapJson();
        Assert.Contains("file.save", json);
        Assert.Contains("Ctrl+Alt+S", json);

        // Reset all
        vm.ResetAllKeymaps();
        Assert.Equal("Ctrl+S", saveCmd.CurrentGesture);

        // Re-import
        bool success = vm.ImportKeymapJson(json);
        Assert.True(success);
        Assert.Equal("Ctrl+Alt+S", saveCmd.CurrentGesture);
    }

    [Fact]
    public void SettingsModalViewModel_ResetCategoryDefaults_ResetsOnlyActiveCategory()
    {
        var service = new SettingsService(_tempSettingsPath);
        var vm = new SettingsModalViewModel(service);
        vm.Open("Editor");

        vm.FontSize = 24.0;
        vm.ShellProgram = "custom_shell.exe";

        vm.ResetCategoryDefaults();

        // Editor was active, so FontSize should be reset to 14.0
        Assert.Equal(14.0, vm.FontSize);
        // Terminal was NOT active, so custom_shell.exe should remain untouched
        Assert.Equal("custom_shell.exe", vm.ShellProgram);
    }
}
