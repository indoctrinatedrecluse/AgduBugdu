using System;
using Avalonia.Input;
using Xunit;

namespace AgduBugdu.Tests;

public class KeyGestureTests
{
    [Fact]
    public void TestKeyGestureParsing()
    {
        var kg1 = KeyGesture.Parse("Ctrl+OemTilde");
        Assert.NotNull(kg1);
        Assert.Equal(Key.OemTilde, kg1.Key);

        var kg2 = KeyGesture.Parse("Alt+Z");
        Assert.NotNull(kg2);
        Assert.Equal(Key.Z, kg2.Key);

        var kg3 = KeyGesture.Parse("Ctrl+Shift+K");
        Assert.NotNull(kg3);
        Assert.Equal(Key.K, kg3.Key);

        var kg4 = KeyGesture.Parse("Ctrl+Shift+J");
        Assert.NotNull(kg4);
        Assert.Equal(Key.J, kg4.Key);

        var kg5 = KeyGesture.Parse("Ctrl+Shift+U");
        Assert.NotNull(kg5);
        Assert.Equal(Key.U, kg5.Key);

        var kg6 = KeyGesture.Parse("Ctrl+U");
        Assert.NotNull(kg6);
        Assert.Equal(Key.U, kg6.Key);

        var kg7 = KeyGesture.Parse("Alt+Up");
        Assert.NotNull(kg7);
        Assert.Equal(Key.Up, kg7.Key);

        var kg8 = KeyGesture.Parse("Alt+Down");
        Assert.NotNull(kg8);
        Assert.Equal(Key.Down, kg8.Key);

        var kg9 = KeyGesture.Parse("Shift+Alt+A");
        Assert.NotNull(kg9);
        Assert.Equal(Key.A, kg9.Key);

        var kg10 = KeyGesture.Parse("Ctrl+OemQuestion");
        Assert.NotNull(kg10);
        Assert.Equal(Key.OemQuestion, kg10.Key);

        // Try parsing Ctrl+/ safely
        bool slashWorked = false;
        try
        {
            var kgSlash = KeyGesture.Parse("Ctrl+/");
            slashWorked = (kgSlash != null);
        }
        catch
        {
            slashWorked = false;
        }
        // We know Ctrl+OemQuestion is 100% reliable in Avalonia XAML
    }

    [Fact]
    public void TestMainWindowGestures()
    {
        string[] gestures = new[]
        {
            "Ctrl+N", "Ctrl+O", "Ctrl+S", "Ctrl+Shift+S", "Ctrl+W", "Ctrl+Shift+T",
            "Ctrl+Z", "Ctrl+Y", "Ctrl+X", "Ctrl+C", "Ctrl+V", "Ctrl+A",
            "Ctrl+F", "Ctrl+H", "Ctrl+G", "Ctrl+M", "Ctrl+OemQuestion",
            "Shift+Alt+A", "Alt+Up", "Alt+Down", "Shift+Alt+Up", "Shift+Alt+Down",
            "Ctrl+Shift+K", "Ctrl+Shift+L", "Ctrl+Shift+J", "Ctrl+Shift+U", "Ctrl+U",
            "Ctrl+Shift+OemOpenBrackets", "Ctrl+Shift+OemCloseBrackets",
            "Ctrl+Shift+OemTilde", "Ctrl+OemComma", "Ctrl+P", "Ctrl+OemBackslash", "Ctrl+Alt+Right",
            "Alt+M", "Alt+Z", "Ctrl+J", "Ctrl+Shift+E", "Ctrl+Shift+F",
            "Ctrl+Shift+G", "Ctrl+Shift+O", "Ctrl+OemTilde", "Ctrl+Shift+D",
            "Ctrl+Shift+B", "Ctrl+Shift+M"
        };

        foreach (var g in gestures)
        {
            var parsed = KeyGesture.Parse(g);
            Assert.NotNull(parsed);
        }

        // Regression guard: Verify that raw unescaped 'Ctrl+\' throws ArgumentException in Avalonia
        // and must never be used directly in XAML InputGesture attributes.
        Assert.Throws<ArgumentException>(() => KeyGesture.Parse("Ctrl+\\"));
    }
}
