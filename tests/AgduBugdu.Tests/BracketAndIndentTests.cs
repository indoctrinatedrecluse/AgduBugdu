using System.Linq;
using AgduBugdu.Core.Text;
using Xunit;

namespace AgduBugdu.Tests;

public class BracketAndIndentTests
{
    [Fact]
    public void TestRainbowBracketsNestingDepths()
    {
        string code = "void Foo() { if (true) { var list = new int[] { (1) }; } }";
        var pairs = BracketColorizerEngine.FindRainbowBrackets(code);

        Assert.NotEmpty(pairs);
        // Level 0: Outer braces { } and method parens ( )
        var level0 = pairs.Where(p => p.Depth == 0).ToList();
        Assert.NotEmpty(level0);
        Assert.Equal(BracketColorizerEngine.RainbowPalette[0], level0[0].ColorHex);

        // Level 1: Inner if braces { }
        var level1 = pairs.Where(p => p.Depth == 1).ToList();
        Assert.NotEmpty(level1);
        Assert.Equal(BracketColorizerEngine.RainbowPalette[1], level1[0].ColorHex);

        // Max depth should be at least 3
        Assert.True(pairs.Max(p => p.Depth) >= 3);
    }

    [Fact]
    public void TestRainbowBracketsIgnoresStringsAndComments()
    {
        string code = @"void Bar()
{
    string s = ""( not a bracket )"";
    // { not a brace }
    /* [ not a bracket ] */
    int x = 42;
}";
        var pairs = BracketColorizerEngine.FindRainbowBrackets(code);

        // Should only contain the method definition ( ) and method body { }
        Assert.Equal(2, pairs.Count);
        Assert.Contains(pairs, p => p.Type == BracketType.Round);
        Assert.Contains(pairs, p => p.Type == BracketType.Curly);
        Assert.DoesNotContain(pairs, p => p.Type == BracketType.Square);
    }

    [Fact]
    public void TestIndentationGuidesComputation()
    {
        string code = @"class Program
{
    void Main()
    {
        if (true)
        {
            Console.WriteLine(""Hello"");
        }
    }
}";
        var guides = BracketColorizerEngine.ComputeIndentationGuides(code, tabSize: 4, caretLine: 7);

        Assert.NotEmpty(guides);
        Assert.Contains(guides, g => g.Column == 4);
        Assert.Contains(guides, g => g.Column == 8);

        // Line 7 is Console.WriteLine inside innermost block -> should mark active scope
        var active = guides.Where(g => g.IsActiveScope).ToList();
        Assert.NotEmpty(active);
    }

    [Fact]
    public void TestEmptyTextReturnsEmptyResults()
    {
        var brackets = BracketColorizerEngine.FindRainbowBrackets("");
        Assert.Empty(brackets);

        var guides = BracketColorizerEngine.ComputeIndentationGuides("");
        Assert.Empty(guides);
    }
}
