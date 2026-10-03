using AgduBugdu.App.ViewModels.Documents;
using AgduBugdu.Core.Text;
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
        string text = "cat catch catfish cat";

        var wholeWord = SearchEngine.FindAll(text, new SearchOptions("cat", WholeWord: true));
        Assert.Equal(2, wholeWord.Count);
        Assert.Equal(0, wholeWord[0].Offset);
        Assert.Equal(18, wholeWord[1].Offset);
    }

    [Fact]
    public void SearchEngine_FindAll_Regex()
    {
        string text = "int v1 = 10;\nint v2 = 20;\nstring s = \"hello\";";

        var regexMatches = SearchEngine.FindAll(text, new SearchOptions(@"v\d+", UseRegex: true));
        Assert.Equal(2, regexMatches.Count);
        Assert.Equal("v1", regexMatches[0].Value);
        Assert.Equal("v2", regexMatches[1].Value);
    }

    [Fact]
    public void SearchEngine_FindNext_And_FindPrevious_Wrap_Around()
    {
        string text = "apple banana apple cherry apple";
        var options = new SearchOptions("apple");

        // Next from offset 0
        var next1 = SearchEngine.FindNext(text, options, 0);
        Assert.NotNull(next1);
        Assert.Equal(0, next1.Value.Result.Offset);
        Assert.Equal(0, next1.Value.Index);
        Assert.Equal(3, next1.Value.TotalCount);

        // Next from offset 5 -> should find index 13
        var next2 = SearchEngine.FindNext(text, options, 5);
        Assert.NotNull(next2);
        Assert.Equal(13, next2.Value.Result.Offset);

        // Next from offset 30 -> wraps around to 0
        var nextWrap = SearchEngine.FindNext(text, options, 30);
        Assert.NotNull(nextWrap);
        Assert.Equal(0, nextWrap.Value.Result.Offset);

        // Previous from offset 20 -> should find index 13
        var prev1 = SearchEngine.FindPrevious(text, options, 20);
        Assert.NotNull(prev1);
        Assert.Equal(13, prev1.Value.Result.Offset);

        // Previous from offset 0 -> wraps around to index 26
        var prevWrap = SearchEngine.FindPrevious(text, options, 0);
        Assert.NotNull(prevWrap);
        Assert.Equal(26, prevWrap.Value.Result.Offset);
    }

    [Fact]
    public void SearchEngine_ReplaceAll()
    {
        string text = "foo bar foo baz foo";
        var (newText, count) = SearchEngine.ReplaceAll(text, new SearchOptions("foo"), "qux");

        Assert.Equal(3, count);
        Assert.Equal("qux bar qux baz qux", newText);
    }

    [Fact]
    public void EditorDocumentViewModel_FindNext_And_Previous_Fires_Events()
    {
        var doc = new EditorDocumentViewModel
        {
            TextDocument = new TextDocument("first item, second item, third item")
        };

        (int Offset, int Length)? selected = null;
        doc.SelectTextRequested += (s, req) => selected = (req.Offset, req.Length);

        doc.OpenFind("item");
        Assert.True(doc.IsFindVisible);

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
}
