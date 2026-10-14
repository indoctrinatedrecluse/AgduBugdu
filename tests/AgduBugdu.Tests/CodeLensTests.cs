using System.Linq;
using AgduBugdu.Core.Symbols;
using Xunit;

namespace AgduBugdu.Tests;

public class CodeLensTests
{
    [Fact]
    public void TestCodeLensResolvesClassesAndMethods()
    {
        string code = @"public class ServiceWorker
{
    public void Initialize()
    {
        DoWork();
    }

    public void DoWork()
    {
    }
}";
        var lenses = CodeLensEngine.ResolveCodeLenses(code, ".cs");

        Assert.NotEmpty(lenses);
        Assert.Contains(lenses, l => l.SymbolName == "ServiceWorker" && l.Kind == SymbolKind.Class);
        Assert.Contains(lenses, l => l.SymbolName == "Initialize" && l.Kind == SymbolKind.Method);
        Assert.Contains(lenses, l => l.SymbolName == "DoWork" && l.Kind == SymbolKind.Method);
    }

    [Fact]
    public void TestCodeLensReferenceCounting()
    {
        string code = @"public class Calculator
{
    public int Add(int a, int b) => a + b;

    public void Compute()
    {
        var x = Add(1, 2);
        var y = Add(3, 4);
        var z = Add(5, 6);
    }
}";
        var lenses = CodeLensEngine.ResolveCodeLenses(code, ".cs");
        var addLens = lenses.FirstOrDefault(l => l.SymbolName == "Add");

        Assert.NotNull(addLens);
        Assert.Equal(3, addLens.ReferenceCount);
        Assert.Contains("3 references", addLens.DisplayText);
    }

    [Fact]
    public void TestCodeLensDetectsTests()
    {
        string code = @"public class MathTests
{
    [Fact]
    public void VerifyAddition()
    {
        Assert.True(true);
    }

    public void HelperMethod()
    {
    }
}";
        var lenses = CodeLensEngine.ResolveCodeLenses(code, ".cs");
        var testLens = lenses.FirstOrDefault(l => l.SymbolName == "VerifyAddition");
        var helperLens = lenses.FirstOrDefault(l => l.SymbolName == "HelperMethod");

        Assert.NotNull(testLens);
        Assert.True(testLens.IsTest);
        Assert.Contains("▶ Run Test", testLens.DisplayText);

        Assert.NotNull(helperLens);
        Assert.False(helperLens.IsTest);
        Assert.DoesNotContain("▶ Run Test", helperLens.DisplayText);
    }

    [Fact]
    public void TestCodeLensEmptyOrWhitespace()
    {
        var lenses = CodeLensEngine.ResolveCodeLenses("", ".cs");
        Assert.Empty(lenses);

        var nullLenses = CodeLensEngine.ResolveCodeLenses("    \n\t  ", ".cs");
        Assert.Empty(nullLenses);
    }
}
