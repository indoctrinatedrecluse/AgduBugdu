using System.Collections.Generic;
using AgduBugdu.Core.Text;
using Xunit;

namespace AgduBugdu.Tests;

public class MultiCaretTests
{
    [Fact]
    public void MultiCaret_AddAndToggle_ManagesCaretCollection()
    {
        var manager = new MultiCaretManager();
        Assert.False(manager.HasSecondaryCarets);
        Assert.Equal(0, manager.SecondaryCaretCount);

        manager.AddCaret(10);
        manager.AddCaret(25);
        Assert.True(manager.HasSecondaryCarets);
        Assert.Equal(2, manager.SecondaryCaretCount);

        // Toggle near offset 10 removes it
        bool added = manager.ToggleCaret(10, tolerance: 1);
        Assert.False(added);
        Assert.Equal(1, manager.SecondaryCaretCount);

        // Toggle at new offset 50 adds it
        added = manager.ToggleCaret(50, tolerance: 1);
        Assert.True(added);
        Assert.Equal(2, manager.SecondaryCaretCount);

        manager.Clear();
        Assert.False(manager.HasSecondaryCarets);
    }

    [Fact]
    public void MultiCaret_SelectNextOccurrence_FindsAndAddsMatches()
    {
        var manager = new MultiCaretManager();
        string text = "var count = 0; count++; if (count > 10) return count;";
        string target = "count";

        // First occurrence is at index 4 (primary selection)
        int primaryOffset = 4;
        int primaryLen = target.Length;

        // Press Ctrl+D -> Selects second occurrence at index 15
        bool found1 = manager.SelectNextOccurrence(text, target, primaryOffset, primaryLen, out var caret1);
        Assert.True(found1);
        Assert.NotNull(caret1);
        Assert.Equal(15, caret1.EffectiveStart);
        Assert.Equal(target.Length, caret1.SelectionLength);
        Assert.Equal(1, manager.SecondaryCaretCount);

        // Press Ctrl+D again -> Selects third occurrence at index 28
        bool found2 = manager.SelectNextOccurrence(text, target, primaryOffset, primaryLen, out var caret2);
        Assert.True(found2);
        Assert.NotNull(caret2);
        Assert.Equal(28, caret2.EffectiveStart);
        Assert.Equal(2, manager.SecondaryCaretCount);

        // Press Ctrl+D again -> Selects fourth occurrence at index 47
        bool found3 = manager.SelectNextOccurrence(text, target, primaryOffset, primaryLen, out var caret3);
        Assert.True(found3);
        Assert.NotNull(caret3);
        Assert.Equal(47, caret3.EffectiveStart);
        Assert.Equal(3, manager.SecondaryCaretCount);

        // All occurrences are selected; next call returns false
        bool found4 = manager.SelectNextOccurrence(text, target, primaryOffset, primaryLen, out _);
        Assert.False(found4);
    }

    [Fact]
    public void MultiCaret_AddColumnCaret_SpawnsDirectlyAboveAndBelow()
    {
        var manager = new MultiCaretManager();
        var lines = new List<string>
        {
            "alpha = 1;",
            "beta  = 2;",
            "gamma = 3;"
        };

        // Primary caret is on Line 2, Column 7 (at '=')
        int line2 = 2;
        int col = 7;

        // Move Down (to line 3)
        bool down = manager.AddColumnCaret(lines, line2, col, moveUp: false, out var caretDown);
        Assert.True(down);
        Assert.NotNull(caretDown);
        Assert.Equal(3, caretDown.Line);
        Assert.Equal(7, caretDown.Column);

        // Move Up (to line 1)
        bool up = manager.AddColumnCaret(lines, line2, col, moveUp: true, out var caretUp);
        Assert.True(up);
        Assert.NotNull(caretUp);
        Assert.Equal(1, caretUp.Line);
        Assert.Equal(7, caretUp.Column);

        Assert.Equal(2, manager.SecondaryCaretCount);
    }

    [Fact]
    public void MultiCaret_ApplyInsertion_InsertsAtAllCaretsSimultaneously()
    {
        var manager = new MultiCaretManager();
        string initial = "int a = 0;\nint b = 0;\nint c = 0;";

        // Primary caret at end of line 1 (offset 10)
        int primaryOffset = 10;

        // Secondary carets at end of line 2 (offset 21) and line 3 (offset 32)
        manager.AddCaret(21);
        manager.AddCaret(32);

        // Simultaneously type " // init"
        var (newText, newPrimary, newSecondary) = manager.ApplyInsertion(initial, " // init", primaryOffset);

        string expected = "int a = 0; // init\nint b = 0; // init\nint c = 0; // init";
        Assert.Equal(expected, newText);
        Assert.Equal(18, newPrimary); // 10 + 8 chars
        Assert.Equal(2, newSecondary.Count);
    }

    [Fact]
    public void MultiCaret_ApplyBackspace_DeletesAcrossAllCaretsSimultaneously()
    {
        var manager = new MultiCaretManager();
        string initial = "item1;\nitem2;\nitem3;";

        // Primary caret at index 5 (after '1')
        int primaryOffset = 5;

        // Secondary carets after '2' (offset 12) and '3' (offset 19)
        manager.AddCaret(12);
        manager.AddCaret(19);

        // Hit backspace
        var (newText, newPrimary, newSecondary) = manager.ApplyBackspace(initial, primaryOffset);

        string expected = "item;\nitem;\nitem;";
        Assert.Equal(expected, newText);
        Assert.Equal(4, newPrimary);
        Assert.Equal(2, newSecondary.Count);
    }

    [Fact]
    public void MultiCaret_ApplyDelete_ForwardDeletesAcrossAllCaretsSimultaneously()
    {
        var manager = new MultiCaretManager();
        string initial = "x = 10;\ny = 20;\nz = 30;";

        // Carets right before '=': primary offset 2, secondaries offset 10 and 18
        int primaryOffset = 2;
        manager.AddCaret(10);
        manager.AddCaret(18);

        // Forward delete '='
        var (newText, newPrimary, newSecondary) = manager.ApplyDelete(initial, primaryOffset);

        string expected = "x  10;\ny  20;\nz  30;";
        Assert.Equal(expected, newText);
        Assert.Equal(2, newPrimary);
        Assert.Equal(2, newSecondary.Count);
    }
}
