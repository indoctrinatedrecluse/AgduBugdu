using System.Linq;
using AgduBugdu.Core.Symbols;
using Xunit;

namespace AgduBugdu.Tests;

public class BreadcrumbTests
{
    [Fact]
    public void ResolveBreadcrumbs_UntitledDocument_ReturnsSingleFileItem()
    {
        var items = BreadcrumbResolver.ResolveBreadcrumbs(string.Empty, string.Empty, 1);

        Assert.Single(items);
        Assert.Equal("Untitled", items[0].Name);
        Assert.Equal(BreadcrumbKind.File, items[0].Kind);
        Assert.True(items[0].IsLast);
    }

    [Fact]
    public void ResolveBreadcrumbs_DeepPath_TruncatesToThreeParentDirectories()
    {
        string path = @"D:\Projects\Repo\src\AgduBugdu.Core\Text\MultiCaretManager.cs";
        var items = BreadcrumbResolver.ResolveBreadcrumbs(path, string.Empty, 1);

        // Parent folders: src, AgduBugdu.Core, Text; and file: MultiCaretManager.cs
        Assert.Equal(4, items.Count);
        Assert.Equal("src", items[0].Name);
        Assert.Equal("AgduBugdu.Core", items[1].Name);
        Assert.Equal("Text", items[2].Name);
        Assert.Equal("MultiCaretManager.cs", items[3].Name);
        Assert.True(items[3].IsLast);
    }

    [Fact]
    public void ResolveBreadcrumbs_CSharpCode_ResolvesEnclosingClassAndMethod()
    {
        string code = @"namespace TestApp;

public class SampleService
{
    public void Execute()
    {
        // Cursor is here at line 8
        int a = 1;
    }
}";

        var items = BreadcrumbResolver.ResolveBreadcrumbs("SampleService.cs", code, 8);

        Assert.True(items.Count >= 3);
        var fileItem = items[0];
        Assert.Equal("SampleService.cs", fileItem.Name);

        var classItem = items[1];
        Assert.Equal("SampleService", classItem.Name);
        Assert.Equal(SymbolKind.Class, classItem.Symbol?.Kind);

        var methodItem = items[2];
        Assert.Equal("Execute ()", methodItem.Name);
        Assert.Equal(SymbolKind.Method, methodItem.Symbol?.Kind);
        Assert.True(items.Last().IsLast);
    }

    [Fact]
    public void ResolveBreadcrumbs_PythonCode_ResolvesClassAndDef()
    {
        string pyCode = @"class DataProcessor:
    def process_items(self):
        # Line 3
        pass";

        var items = BreadcrumbResolver.ResolveBreadcrumbs("script.py", pyCode, 3);

        Assert.Equal(3, items.Count);
        Assert.Equal("script.py", items[0].Name);
        Assert.Equal("DataProcessor", items[1].Name);
        Assert.Equal("process_items (self)", items[2].Name);
    }

    [Fact]
    public void ResolveStickyScope_AtLine1_ReturnsNull()
    {
        string code = "public class A {\n    public void B() {}\n}";
        var scope = BreadcrumbResolver.ResolveStickyScope("Test.cs", code, 1);
        Assert.Null(scope);
    }

    [Fact]
    public void ResolveStickyScope_InsideMethod_ReturnsMethodScope()
    {
        string code = @"public class A
{
    public void Calculate()
    {
        // Line 5
        // Line 6
        // Line 7
    }
}";

        var scope = BreadcrumbResolver.ResolveStickyScope("Test.cs", code, 6);

        Assert.NotNull(scope);
        Assert.Equal("Calculate ()", scope.Title);
        Assert.Equal(3, scope.LineNumber);
    }

    [Fact]
    public void ResolveStickyScope_InsideClassBeforeMethod_ReturnsClassScope()
    {
        string code = @"public class Container
{
    // Line 3: inside class
    // Line 4: inside class
    public void Run() {}
}";

        var scope = BreadcrumbResolver.ResolveStickyScope("Test.cs", code, 4);

        Assert.NotNull(scope);
        Assert.Equal("Container", scope.Title);
        Assert.Equal(1, scope.LineNumber);
    }
}
