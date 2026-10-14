using System.Linq;
using AgduBugdu.Core.Folding;
using Xunit;

namespace AgduBugdu.Tests;

public class CodeFoldingTests
{
    [Fact]
    public void TestBraceFoldingDetection()
    {
        string code = @"namespace MyDemo
{
    public class Sample
    {
        public void Run()
        {
            int a = 1;
        }
    }
}";
        var foldings = CodeFoldingEngine.FindFoldings(code, ".cs");

        Assert.NotEmpty(foldings);
        Assert.Contains(foldings, f => f.Type == FoldingType.Braces);
        Assert.True(foldings.Count >= 3); // namespace, class, method
    }

    [Fact]
    public void TestRegionFoldingDetection()
    {
        string code = @"public class Calculator
{
    #region Public Math APIs
    public int Add(int a, int b) => a + b;
    public int Sub(int a, int b) => a - b;
    #endregion
}";
        var foldings = CodeFoldingEngine.FindFoldings(code, ".cs");

        var regionFolding = foldings.FirstOrDefault(f => f.Type == FoldingType.Region);
        Assert.NotNull(regionFolding);
        Assert.Equal("[Region: Public Math APIs]", regionFolding.HeaderText);
        Assert.True(regionFolding.EndLine > regionFolding.StartLine);
    }

    [Fact]
    public void TestCommentFoldingDetection()
    {
        string code = @"/*
 * Multi-line header comment
 * Line 3
 */
public class Foo {}";
        var foldings = CodeFoldingEngine.FindFoldings(code, ".cs");

        var commentFolding = foldings.FirstOrDefault(f => f.Type == FoldingType.Comment);
        Assert.NotNull(commentFolding);
        Assert.Equal(1, commentFolding.StartLine);
        Assert.Equal(4, commentFolding.EndLine);
    }

    [Fact]
    public void TestXmlCommentFolding()
    {
        string xml = @"<root>
    <!--
      XML block comment
    -->
    <item>Value</item>
</root>";
        var foldings = CodeFoldingEngine.FindFoldings(xml, ".xml");

        var xmlComment = foldings.FirstOrDefault(f => f.Type == FoldingType.Comment);
        Assert.NotNull(xmlComment);
        Assert.Equal("<!-- ... -->", xmlComment.HeaderText);
    }

    [Fact]
    public void TestPythonIndentFolding()
    {
        string pyCode = @"def calculate_total(items):
    total = 0
    for item in items:
        total += item.price
    return total

print('Done')
";
        var foldings = CodeFoldingEngine.FindFoldings(pyCode, ".py");

        Assert.NotEmpty(foldings);
        Assert.Contains(foldings, f => f.Type == FoldingType.Indent);
    }

    [Fact]
    public void TestIgnoreBracesInsideStringsAndComments()
    {
        string code = @"public void Test()
{
    string s = ""{ not a real block }"";
    // { also not a block }
    int y = 42;
}";
        var foldings = CodeFoldingEngine.FindFoldings(code, ".cs");

        var braceFoldings = foldings.Where(f => f.Type == FoldingType.Braces).ToList();
        Assert.Single(braceFoldings); // Only the outer method braces
    }
}
