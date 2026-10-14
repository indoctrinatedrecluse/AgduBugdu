using System;
using System.Collections.Generic;
using System.Linq;

namespace AgduBugdu.Core.Text;

/// <summary>
/// Represents a single caret position with optional selection span.
/// </summary>
public record CaretSnapshot(int Offset, int SelectionStart = -1, int SelectionLength = 0, int Line = 1, int Column = 1)
{
    public bool HasSelection => SelectionLength > 0;
    public int EffectiveStart => SelectionLength > 0 ? (SelectionStart >= 0 ? SelectionStart : Offset) : Offset;
    public int EffectiveEnd => EffectiveStart + Math.Max(0, SelectionLength);
}

/// <summary>
/// Manages secondary carets, column block selection, and synchronized multi-caret editing.
/// </summary>
public class MultiCaretManager
{
    private readonly List<CaretSnapshot> _secondaryCarets = new();

    public IReadOnlyList<CaretSnapshot> SecondaryCarets => _secondaryCarets;
    public int SecondaryCaretCount => _secondaryCarets.Count;
    public bool HasSecondaryCarets => _secondaryCarets.Count > 0;

    /// <summary>
    /// Adds a secondary caret location.
    /// </summary>
    public void AddCaret(int offset, int selectionStart = -1, int selectionLength = 0, int line = 1, int column = 1)
    {
        if (offset < 0) offset = 0;
        int selStart = selectionStart >= 0 ? selectionStart : offset;
        int selLen = Math.Max(0, selectionLength);

        // Don't add duplicate caret within 1 character
        if (_secondaryCarets.Any(c => Math.Abs(c.Offset - offset) <= 0 && c.SelectionLength == selLen))
            return;

        _secondaryCarets.Add(new CaretSnapshot(offset, selStart, selLen, line, column));
    }

    /// <summary>
    /// Removes a secondary caret within tolerance distance, or returns false if none found.
    /// </summary>
    public bool ToggleCaret(int offset, int tolerance = 1)
    {
        int idx = _secondaryCarets.FindIndex(c => Math.Abs(c.Offset - offset) <= tolerance);
        if (idx >= 0)
        {
            _secondaryCarets.RemoveAt(idx);
            return false; // Removed
        }
        else
        {
            AddCaret(offset);
            return true; // Added
        }
    }

    /// <summary>
    /// Clears all secondary carets (e.g. on Escape or normal mouse click).
    /// </summary>
    public void Clear()
    {
        _secondaryCarets.Clear();
    }

    /// <summary>
    /// Retrieves all carets (primary + secondary) sorted descending by effective offset.
    /// Processing from highest to lowest offset ensures text modifications do not displace subsequent edits.
    /// </summary>
    public List<CaretSnapshot> GetAllCaretsDescending(int primaryOffset, int primarySelStart = -1, int primarySelLength = 0, int primaryLine = 1, int primaryCol = 1)
    {
        var list = new List<CaretSnapshot>(_secondaryCarets.Count + 1);
        int pStart = primarySelStart >= 0 ? primarySelStart : primaryOffset;
        list.Add(new CaretSnapshot(primaryOffset, pStart, Math.Max(0, primarySelLength), primaryLine, primaryCol));
        list.AddRange(_secondaryCarets);

        // Remove exact duplicates and sort descending by start
        return list
            .OrderByDescending(c => c.EffectiveStart)
            .DistinctBy(c => (c.EffectiveStart, c.EffectiveEnd))
            .ToList();
    }

    /// <summary>
    /// Select Next Occurrence (Ctrl+D): Finds the next instance of <paramref name="targetText"/> in <paramref name="docText"/>
    /// and adds a secondary caret across it.
    /// </summary>
    public bool SelectNextOccurrence(string docText, string targetText, int primaryOffset, int primarySelLength, out CaretSnapshot? newCaret)
    {
        newCaret = null;
        if (string.IsNullOrEmpty(docText) || string.IsNullOrEmpty(targetText))
            return false;

        // Current search start: after the latest caret
        int searchStart = primaryOffset + Math.Max(0, primarySelLength);
        if (_secondaryCarets.Count > 0)
        {
            searchStart = Math.Max(searchStart, _secondaryCarets.Max(c => c.EffectiveEnd));
        }

        int foundIdx = docText.IndexOf(targetText, searchStart, StringComparison.Ordinal);
        if (foundIdx < 0 && searchStart > 0)
        {
            // Wrap around to start of document
            foundIdx = docText.IndexOf(targetText, 0, searchStart, StringComparison.Ordinal);
        }

        if (foundIdx >= 0)
        {
            // Check if this match is already covered by primary or secondary carets
            bool alreadySelected = (foundIdx == primaryOffset && targetText.Length == primarySelLength) ||
                                   _secondaryCarets.Any(c => c.EffectiveStart == foundIdx && c.SelectionLength == targetText.Length);

            if (!alreadySelected)
            {
                newCaret = new CaretSnapshot(foundIdx + targetText.Length, foundIdx, targetText.Length);
                _secondaryCarets.Add(newCaret);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Adds a column caret directly above or below the current line at the same visual column.
    /// </summary>
    public bool AddColumnCaret(IReadOnlyList<string> lines, int currentLineOneBased, int currentColumnOneBased, bool moveUp, out CaretSnapshot? newCaret)
    {
        newCaret = null;
        if (lines == null || lines.Count == 0) return false;

        int targetLineOneBased = moveUp ? currentLineOneBased - 1 : currentLineOneBased + 1;
        if (targetLineOneBased < 1 || targetLineOneBased > lines.Count)
            return false;

        // Compute offset of target line
        int lineStartOffset = 0;
        for (int i = 0; i < targetLineOneBased - 1; i++)
        {
            lineStartOffset += lines[i].Length + 1; // Assuming \n or normalised
        }

        string targetLineText = lines[targetLineOneBased - 1];
        int clampedCol = Math.Clamp(currentColumnOneBased - 1, 0, targetLineText.Length);
        int targetOffset = lineStartOffset + clampedCol;

        newCaret = new CaretSnapshot(targetOffset, targetOffset, 0, targetLineOneBased, clampedCol + 1);
        _secondaryCarets.Add(newCaret);
        return true;
    }

    /// <summary>
    /// Applies synchronized text insertion across all active carets simultaneously.
    /// Returns the updated document text, new primary offset, and updated secondary carets.
    /// </summary>
    public (string NewText, int NewPrimaryOffset, List<CaretSnapshot> NewSecondaryCarets) ApplyInsertion(
        string docText,
        string insertedText,
        int primaryOffset,
        int primarySelStart = -1,
        int primarySelLength = 0)
    {
        var allCarets = GetAllCaretsDescending(primaryOffset, primarySelStart, primarySelLength);
        var sb = new System.Text.StringBuilder(docText);

        var updatedCarets = new List<(CaretSnapshot Original, int NewOffset)>();

        foreach (var c in allCarets)
        {
            int replaceStart = c.EffectiveStart;
            int replaceLen = c.SelectionLength;

            if (replaceStart > sb.Length) replaceStart = sb.Length;
            if (replaceStart + replaceLen > sb.Length) replaceLen = sb.Length - replaceStart;

            sb.Remove(replaceStart, replaceLen);
            sb.Insert(replaceStart, insertedText);

            int newCaretOffset = replaceStart + insertedText.Length;
            updatedCarets.Add((c, newCaretOffset));
        }

        // Separate primary from secondary carets
        int newPrimary = primaryOffset;
        var newSecondary = new List<CaretSnapshot>();

        foreach (var (orig, newOff) in updatedCarets)
        {
            if (orig.EffectiveStart == (primarySelStart >= 0 ? primarySelStart : primaryOffset) &&
                orig.SelectionLength == primarySelLength)
            {
                newPrimary = newOff;
            }
            else
            {
                newSecondary.Add(new CaretSnapshot(newOff, newOff, 0));
            }
        }

        _secondaryCarets.Clear();
        _secondaryCarets.AddRange(newSecondary);

        return (sb.ToString(), newPrimary, newSecondary);
    }

    /// <summary>
    /// Applies synchronized backspace deletion across all active carets simultaneously.
    /// </summary>
    public (string NewText, int NewPrimaryOffset, List<CaretSnapshot> NewSecondaryCarets) ApplyBackspace(
        string docText,
        int primaryOffset,
        int primarySelStart = -1,
        int primarySelLength = 0)
    {
        var allCarets = GetAllCaretsDescending(primaryOffset, primarySelStart, primarySelLength);
        var sb = new System.Text.StringBuilder(docText);

        var updatedCarets = new List<(CaretSnapshot Original, int NewOffset)>();

        foreach (var c in allCarets)
        {
            int delStart;
            int delLen;

            if (c.HasSelection)
            {
                delStart = c.EffectiveStart;
                delLen = c.SelectionLength;
            }
            else
            {
                if (c.Offset <= 0)
                {
                    updatedCarets.Add((c, 0));
                    continue;
                }
                delStart = c.Offset - 1;
                delLen = 1;
            }

            if (delStart < sb.Length)
            {
                delLen = Math.Min(delLen, sb.Length - delStart);
                sb.Remove(delStart, delLen);
            }

            updatedCarets.Add((c, delStart));
        }

        int newPrimary = primaryOffset;
        var newSecondary = new List<CaretSnapshot>();

        foreach (var (orig, newOff) in updatedCarets)
        {
            if (orig.EffectiveStart == (primarySelStart >= 0 ? primarySelStart : primaryOffset) &&
                orig.SelectionLength == primarySelLength)
            {
                newPrimary = newOff;
            }
            else
            {
                newSecondary.Add(new CaretSnapshot(newOff, newOff, 0));
            }
        }

        _secondaryCarets.Clear();
        _secondaryCarets.AddRange(newSecondary);

        return (sb.ToString(), newPrimary, newSecondary);
    }

    /// <summary>
    /// Applies synchronized forward delete across all active carets simultaneously.
    /// </summary>
    public (string NewText, int NewPrimaryOffset, List<CaretSnapshot> NewSecondaryCarets) ApplyDelete(
        string docText,
        int primaryOffset,
        int primarySelStart = -1,
        int primarySelLength = 0)
    {
        var allCarets = GetAllCaretsDescending(primaryOffset, primarySelStart, primarySelLength);
        var sb = new System.Text.StringBuilder(docText);

        var updatedCarets = new List<(CaretSnapshot Original, int NewOffset)>();

        foreach (var c in allCarets)
        {
            int delStart;
            int delLen;

            if (c.HasSelection)
            {
                delStart = c.EffectiveStart;
                delLen = c.SelectionLength;
            }
            else
            {
                if (c.Offset >= sb.Length)
                {
                    updatedCarets.Add((c, sb.Length));
                    continue;
                }
                delStart = c.Offset;
                delLen = 1;
            }

            if (delStart < sb.Length)
            {
                delLen = Math.Min(delLen, sb.Length - delStart);
                sb.Remove(delStart, delLen);
            }

            updatedCarets.Add((c, delStart));
        }

        int newPrimary = primaryOffset;
        var newSecondary = new List<CaretSnapshot>();

        foreach (var (orig, newOff) in updatedCarets)
        {
            if (orig.EffectiveStart == (primarySelStart >= 0 ? primarySelStart : primaryOffset) &&
                orig.SelectionLength == primarySelLength)
            {
                newPrimary = newOff;
            }
            else
            {
                newSecondary.Add(new CaretSnapshot(newOff, newOff, 0));
            }
        }

        _secondaryCarets.Clear();
        _secondaryCarets.AddRange(newSecondary);

        return (sb.ToString(), newPrimary, newSecondary);
    }
}
