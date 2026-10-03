using System;
using System.Collections.Generic;
using AgduBugdu.App.ViewModels.Documents;
using AgduBugdu.Core.Text;
using AgduBugdu.PluginContracts;
using AvaloniaEdit.Document;
using Xunit;

namespace AgduBugdu.Tests;

public class TextEditingTests
{
    [Theory]
    [InlineData("func(a, b)", 4, 9)]       // '(' at index 4 -> ')' at index 9
    [InlineData("func(a, b)", 9, 4)]       // ')' at index 9 -> '(' at index 4
    [InlineData("func(a, b)", 5, 9)]       // immediately after '(' -> ')' at index 9
    [InlineData("items[10]", 5, 8)]        // '[' at index 5 -> ']' at index 8
    [InlineData("{ int x = 1; }", 0, 13)]  // '{' at 0 -> '}' at 13
    [InlineData("vector<int>", 6, 10)]     // '<' at 6 -> '>' at 10
    public void BracketMatcher_Finds_Matching_Brackets(string code, int caretOffset, int expectedMatch)
    {
        var result = BracketMatcher.FindMatchingBracket(code, caretOffset);
        Assert.NotNull(result);
        Assert.Equal(expectedMatch, result.Value);
    }

    [Fact]
    public void BracketMatcher_Handles_Nested_Brackets()
    {
        string code = "((1 + 2) * (3 + 4))";
        // Outer opening bracket at index 0
        var outerClose = BracketMatcher.FindMatchingBracket(code, 0);
        Assert.Equal(code.Length - 1, outerClose);

        // First inner bracket at index 1 -> 7
        var inner1Close = BracketMatcher.FindMatchingBracket(code, 1);
        Assert.Equal(7, inner1Close);

        // Second inner bracket at index 11 -> 17
        var inner2Close = BracketMatcher.FindMatchingBracket(code, 11);
        Assert.Equal(17, inner2Close);
    }

    [Fact]
    public void BracketMatcher_Returns_Null_When_No_Bracket_Or_Unmatched()
    {
        Assert.Null(BracketMatcher.FindMatchingBracket("hello world", 3));
        Assert.Null(BracketMatcher.FindMatchingBracket("unmatched (", 10));
    }

    [Fact]
    public void SearchEngine_FindAll_CaseSensitive_And_Insensitive()
    {
        string text = "Hello world, hello universe, HELLO everyone!";

        var sensitive = SearchEngine.FindAll(text, new SearchOptions("hello", MatchCase: true));
        Assert.Single(sensitive);
        Assert.Equal(13, sensitive[0].Offset);

        var insensitive = SearchEngine.FindAll(text, new SearchOptions("hello", MatchCase: false));
        Assert.Equal(3, insensitive.Count);
    }

    [Fact]
    public void SearchEngine_FindAll_WholeWord()
    {
        string text = "cat catalog concatenate cat dog";

        var matches = SearchEngine.FindAll(text, new SearchOptions("cat", WholeWord: true));
        Assert.Equal(2, matches.Count);
        Assert.Equal(0, matches[0].Offset);
        Assert.Equal(24, matches[1].Offset);
    }

    [Fact]
    public void SearchEngine_FindAll_Regex()
    {
        string text = "item123 item456 something else item789";

        var matches = SearchEngine.FindAll(text, new SearchOptions(@"item\d+", UseRegex: true));
        Assert.Equal(3, matches.Count);
        Assert.Equal("item123", text.Substring(matches[0].Offset, matches[0].Length));
        Assert.Equal("item456", text.Substring(matches[1].Offset, matches[1].Length));
        Assert.Equal("item789", text.Substring(matches[2].Offset, matches[2].Length));
    }

    [Fact]
    public void SearchEngine_FindNext_And_FindPrevious_Wraps_Around()
    {
        string text = "one two one three one";
        var options = new SearchOptions("one");

        // Next from beginning -> offset 0
        var m1 = SearchEngine.FindNext(text, options, 0);
        Assert.NotNull(m1);
        Assert.Equal(0, m1.Value.Result.Offset);
        Assert.Equal(0, m1.Value.Index);

        // Next from offset 1 -> offset 8
        var m2 = SearchEngine.FindNext(text, options, 1);
        Assert.NotNull(m2);
        Assert.Equal(8, m2.Value.Result.Offset);
        Assert.Equal(1, m2.Value.Index);

        // Next from after last match (offset 19) -> wraps back to 0
        var m3 = SearchEngine.FindNext(text, options, 19);
        Assert.NotNull(m3);
        Assert.Equal(0, m3.Value.Result.Offset);

        // Previous from offset 0 -> wraps to last match (offset 18)
        var p1 = SearchEngine.FindPrevious(text, options, 0);
        Assert.NotNull(p1);
        Assert.Equal(18, p1.Value.Result.Offset);
        Assert.Equal(2, p1.Value.Index);
    }

    [Fact]
    public void SearchEngine_ReplaceAll_Replaces_All_Occurrences()
    {
        string text = "apple banana apple cherry apple";
        var options = new SearchOptions("apple");

        var (newText, count) = SearchEngine.ReplaceAll(text, options, "orange");
        Assert.Equal(3, count);
        Assert.Equal("orange banana orange cherry orange", newText);
    }

    [Fact]
    public void EditorDocumentViewModel_SearchCommands_And_Navigation()
    {
        var doc = new EditorDocumentViewModel
        {
            TextDocument = new TextDocument("first item, second item, third item")
        };

        (int Offset, int Length)? selected = null;
        doc.SelectTextRequested += (s, req) => selected = (req.Offset, req.Length);

        doc.OpenFind("item");
        Assert.True(doc.IsFindVisible);
        Assert.Equal("item", doc.SearchQuery);
        Assert.True(doc.HasSearchMatches);

        doc.FindNext();
        Assert.NotNull(selected);
        Assert.Equal(6, selected.Value.Offset);
        Assert.Equal(4, selected.Value.Length);
        Assert.Contains("1 of 3", doc.SearchMatchStatus);

        doc.SelectedLength = 4;
        doc.FindNext();
        Assert.Equal(19, selected.Value.Offset);
        Assert.Contains("2 of 3", doc.SearchMatchStatus);

        doc.FindPrevious();
        Assert.Equal(6, selected.Value.Offset);
        Assert.Contains("1 of 3", doc.SearchMatchStatus);
    }

    [Fact]
    public void EditorDocumentViewModel_ReplaceAll_Works_Correctly()
    {
        var doc = new EditorDocumentViewModel
        {
            TextDocument = new TextDocument("alpha beta alpha gamma alpha")
        };

        doc.OpenReplace("alpha");
        doc.ReplaceQuery = "omega";
        doc.ReplaceAll();

        Assert.Equal("omega beta omega gamma omega", doc.GetText());
        Assert.True(doc.IsModified);
        Assert.Contains("3 replaced", doc.SearchMatchStatus);
    }

    [Fact]
    public void EditorDocumentViewModel_GoToLine_Parses_Line_And_Column()
    {
        var doc = new EditorDocumentViewModel
        {
            TextDocument = new TextDocument("line 1\nline 2\nline 3\nline 4\nline 5")
        };

        int? scrolledLine = null;
        doc.ScrollToLineRequested += (s, l) => scrolledLine = l;

        doc.OpenGoToLine();
        Assert.True(doc.IsGoToLineVisible);

        doc.GoToLineInput = "3:4";
        doc.ConfirmGoToLine();

        Assert.False(doc.IsGoToLineVisible);
        Assert.Equal(3, doc.Line);
        Assert.Equal(4, doc.Column);
        Assert.Equal(3, scrolledLine);
    }

    [Fact]
    public void EditorDocumentViewModel_MatchingBracket_Navigation()
    {
        var doc = new EditorDocumentViewModel
        {
            TextDocument = new TextDocument("void test() { return; }")
        };

        (int Offset, int Length)? selected = null;
        doc.SelectTextRequested += (s, req) => selected = (req.Offset, req.Length);

        // Position caret at '(' (index 9)
        doc.CaretOffset = 9;
        doc.GoToMatchingBracket();

        Assert.NotNull(selected);
        Assert.Equal(10, selected.Value.Offset); // ')' is at 10

        // Position caret at '{' (index 12)
        doc.CaretOffset = 12;
        doc.GoToMatchingBracket();

        Assert.NotNull(selected);
        Assert.Equal(22, selected.Value.Offset); // '}' is at 22
    }

    // --- Phase 2: Editing Ergonomics Tests ---

    [Fact]
    public void CommentSyntax_Resolves_Extensions_And_CustomDefinitions()
    {
        var cs = CommentSyntax.GetCommentSyntax("App.cs");
        Assert.Equal("//", cs.LinePrefix);
        Assert.Equal("/*", cs.BlockStart);
        Assert.Equal("*/", cs.BlockEnd);

        var py = CommentSyntax.GetCommentSyntax("script.py");
        Assert.Equal("#", py.LinePrefix);
        Assert.Equal("\"\"\"", py.BlockStart);

        var html = CommentSyntax.GetCommentSyntax("index.html");
        Assert.Equal("<!--", html.LinePrefix);
        Assert.Equal("-->", html.BlockEnd);

        // Custom definition
        var customDef = new LanguageDefinition("powershell", "PowerShell", new[] { ".ps1" }, "source.powershell", "#", "<#", "#>");
        var resolved = CommentSyntax.Resolve(customDef, "script.ps1");
        Assert.Equal("#", resolved.LinePrefix);
        Assert.Equal("<#", resolved.BlockStart);
        Assert.Equal("#>", resolved.BlockEnd);
    }

    [Fact]
    public void TextManipulations_ToggleLineComments_IndentedAndEmptyLines()
    {
        var lines = new List<string>
        {
            "    int a = 1;",
            "",
            "    int b = 2;"
        };

        // Comment
        var (commented, wasCommented) = TextManipulations.ToggleLineComments(lines, "//");
        Assert.True(wasCommented);
        Assert.Equal("    // int a = 1;", commented[0]);
        Assert.Equal("", commented[1]);
        Assert.Equal("    // int b = 2;", commented[2]);

        // Uncomment
        var (uncommented, wasCommented2) = TextManipulations.ToggleLineComments(commented, "//");
        Assert.False(wasCommented2);
        Assert.Equal("    int a = 1;", uncommented[0]);
        Assert.Equal("", uncommented[1]);
        Assert.Equal("    int b = 2;", uncommented[2]);
    }

    [Fact]
    public void TextManipulations_ToggleBlockComment_WrapsAndUnwraps()
    {
        string code = "int result = x + y;";

        var (commented, wasCommented) = TextManipulations.ToggleBlockComment(code, "/*", "*/");
        Assert.True(wasCommented);
        Assert.Equal("/* int result = x + y; */", commented);

        var (uncommented, wasCommented2) = TextManipulations.ToggleBlockComment(commented, "/*", "*/");
        Assert.False(wasCommented2);
        Assert.Equal("int result = x + y;", uncommented);
    }

    [Fact]
    public void TextManipulations_MoveLines_ShiftsCorrectly()
    {
        var lines = new List<string> { "first", "second", "third" };

        // Move "second" up
        var (movedUp, newIdx) = TextManipulations.MoveLinesUp(lines, 1, 1);
        Assert.Equal(0, newIdx);
        Assert.Equal(new[] { "second", "first", "third" }, movedUp);

        // Move "second" down
        var (movedDown, newIdx2) = TextManipulations.MoveLinesDown(movedUp, 0, 1);
        Assert.Equal(1, newIdx2);
        Assert.Equal(new[] { "first", "second", "third" }, movedDown);

        // Boundary tests
        var (atTop, _) = TextManipulations.MoveLinesUp(lines, 0, 1);
        Assert.Equal(lines, atTop);

        var (atBottom, _) = TextManipulations.MoveLinesDown(lines, 2, 1);
        Assert.Equal(lines, atBottom);
    }

    [Fact]
    public void TextManipulations_DuplicateLines_ClonesSlice()
    {
        var lines = new List<string> { "line 1", "line 2", "line 3" };

        var (duplicatedDown, insertedIdx) = TextManipulations.DuplicateLines(lines, 1, 1, duplicateBelow: true);
        Assert.Equal(2, insertedIdx);
        Assert.Equal(new[] { "line 1", "line 2", "line 2", "line 3" }, duplicatedDown);

        var (duplicatedUp, insertedIdx2) = TextManipulations.DuplicateLines(lines, 1, 1, duplicateBelow: false);
        Assert.Equal(1, insertedIdx2);
        Assert.Equal(new[] { "line 1", "line 2", "line 2", "line 3" }, duplicatedUp);
    }

    [Fact]
    public void TextManipulations_DeleteLines_RemovesCorrectLines()
    {
        var lines = new List<string> { "line 1", "line 2", "line 3" };

        var deleted = TextManipulations.DeleteLines(lines, 1, 1);
        Assert.Equal(new[] { "line 1", "line 3" }, deleted);

        var allDeleted = TextManipulations.DeleteLines(lines, 0, 3);
        Assert.Single(allDeleted);
        Assert.Equal(string.Empty, allDeleted[0]);
    }

    [Fact]
    public void TextManipulations_JoinLines_CollapsesIndentation()
    {
        var lines = new[] { "function hello() {", "    return 42;", "}" };
        string joined = TextManipulations.JoinLines(lines);
        Assert.Equal("function hello() { return 42; }", joined);
    }

    [Fact]
    public void TextManipulations_FindWordBoundaries_FindsIdentifiers()
    {
        string text = "int my_variable_name = 100;";
        var (start, length) = TextManipulations.FindWordBoundaries(text, 10);
        Assert.Equal(4, start);
        Assert.Equal("my_variable_name", text.Substring(start, length));
    }

    [Fact]
    public void EditorDocumentViewModel_ToggleLineComment_CS_And_Python()
    {
        var doc = new EditorDocumentViewModel
        {
            FilePath = "test.cs",
            TextDocument = new TextDocument("line one\nline two\nline three")
        };

        doc.CaretOffset = 2; // Line 1
        doc.ToggleLineComment();
        Assert.Equal("// line one\nline two\nline three", doc.GetText());
        Assert.True(doc.IsModified);

        doc.ToggleLineComment();
        Assert.Equal("line one\nline two\nline three", doc.GetText());

        // Test python
        var pyDoc = new EditorDocumentViewModel
        {
            FilePath = "script.py",
            TextDocument = new TextDocument("print('hello')")
        };
        pyDoc.ToggleLineComment();
        Assert.Equal("# print('hello')", pyDoc.GetText());
    }

    [Fact]
    public void EditorDocumentViewModel_ToggleBlockComment_Selection()
    {
        var doc = new EditorDocumentViewModel
        {
            FilePath = "test.cs",
            TextDocument = new TextDocument("int value = 42;")
        };

        doc.SelectionStart = 0;
        doc.SelectedLength = 15;
        doc.ToggleBlockComment();

        Assert.Equal("/* int value = 42; */", doc.GetText());

        doc.ToggleBlockComment();
        Assert.Equal("int value = 42;", doc.GetText());
    }

    [Fact]
    public void EditorDocumentViewModel_MoveLineUp_And_Down()
    {
        var doc = new EditorDocumentViewModel
        {
            TextDocument = new TextDocument("alpha\nbeta\ngamma")
        };

        // Move "beta" (line 2) up
        doc.CaretOffset = 7;
        doc.MoveLineUp();
        Assert.Equal("beta\nalpha\ngamma", doc.GetText());

        // Move "beta" (line 1) down
        doc.MoveLineDown();
        Assert.Equal("alpha\nbeta\ngamma", doc.GetText());
    }

    [Fact]
    public void EditorDocumentViewModel_DuplicateLine_And_DeleteLine()
    {
        var doc = new EditorDocumentViewModel
        {
            TextDocument = new TextDocument("alpha\nbeta\ngamma")
        };

        // Duplicate line 2 ("beta") down
        doc.CaretOffset = 7;
        doc.DuplicateLineDown();
        Assert.Equal("alpha\nbeta\nbeta\ngamma", doc.GetText());

        // Delete duplicated line
        doc.DeleteLine();
        Assert.Equal("alpha\nbeta\ngamma", doc.GetText());
    }

    [Fact]
    public void EditorDocumentViewModel_JoinLines_MergesNextLine()
    {
        var doc = new EditorDocumentViewModel
        {
            TextDocument = new TextDocument("hello\n    world")
        };

        doc.CaretOffset = 2; // on "hello"
        doc.JoinLines();
        Assert.Equal("hello world", doc.GetText());
    }

    [Fact]
    public void EditorDocumentViewModel_TransformCase_UpperAndLower()
    {
        var doc = new EditorDocumentViewModel
        {
            TextDocument = new TextDocument("hello world")
        };

        doc.SelectionStart = 0;
        doc.SelectedLength = 5; // "hello"
        doc.TransformUppercase();
        Assert.Equal("HELLO world", doc.GetText());

        doc.TransformLowercase();
        Assert.Equal("hello world", doc.GetText());
    }

    [Fact]
    public void EditorDocumentViewModel_ToggleWordWrap()
    {
        var doc = new EditorDocumentViewModel();
        Assert.False(doc.WordWrap);

        doc.ToggleWordWrap();
        Assert.True(doc.WordWrap);

        doc.ToggleWordWrap();
        Assert.False(doc.WordWrap);
    }
}
