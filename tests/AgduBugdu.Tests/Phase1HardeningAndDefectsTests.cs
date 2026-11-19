using System;
using System.IO;
using System.Threading.Tasks;
using AgduBugdu.Core.Diagnostics;
using AgduBugdu.Core.Docker;
using AgduBugdu.Core.Git;
using AgduBugdu.Core.Symbols;
using AgduBugdu.Core.Text;
using AgduBugdu.Infrastructure.Terminal;
using AgduBugdu.Plugin.RestClient;
using Xunit;

namespace AgduBugdu.Tests;

public class Phase1HardeningAndDefectsTests
{
    [Fact]
    public void RestClientViewModel_AllowUntrustedCertificates_DefaultsToFalse()
    {
        var vm = new RestClientViewModel();
        Assert.False(vm.AllowUntrustedCertificates);

        vm.AllowUntrustedCertificates = true;
        Assert.True(vm.AllowUntrustedCertificates);
    }

    [Fact]
    public void TuiKeyTranslator_Backspace_EmitsDel127Consistently()
    {
        string? specialKey = TuiKeyTranslator.TranslateSpecialKey(TuiSpecialKey.Backspace);
        Assert.Equal("\x7f", specialKey);

        string actionKey = TuiKeyTranslator.ResolveTuiAction("backspace");
        Assert.Equal("\x7f", actionKey);
    }

    [Fact]
    public void SearchEngine_ReDoS_TimesOutGracefullyWithoutCrashing()
    {
        // Pathological backtracking pattern: (a+)+$ against 'aaaaaaaaaaaaaaaaaaaaaaaaaaaa!'
        string text = new string('a', 30) + "!";
        var options = new SearchOptions(
            Query: "(a+)+$",
            UseRegex: true
        );

        var results = SearchEngine.FindAll(text, options);
        // Handled gracefully via timeout without unhandled exception or hanging
        Assert.Empty(results);
    }

    [Fact]
    public void CodeSymbolParser_CSharpBraceScopes_CalculatesEndLineAccurately()
    {
        string code = @"public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }

    public int Subtract(int a, int b)
    {
        return a - b;
    }
}";
        var symbols = CodeSymbolParser.ParseSymbols(code, "Calculator.cs");
        Assert.Equal(3, symbols.Count);

        var classSym = symbols[0];
        Assert.Equal("Calculator", classSym.Name);
        Assert.Equal(1, classSym.LineNumber);
        Assert.Equal(12, classSym.EndLineNumber);

        var addMethod = symbols[1];
        Assert.Equal("Add", addMethod.Name);
        Assert.Equal(3, addMethod.LineNumber);
        Assert.Equal(6, addMethod.EndLineNumber);

        var subMethod = symbols[2];
        Assert.Equal("Subtract", subMethod.Name);
        Assert.Equal(8, subMethod.LineNumber);
        Assert.Equal(11, subMethod.EndLineNumber);
    }

    [Fact]
    public void BreadcrumbResolver_LineOutsideMethod_OnlyReturnsClassScope()
    {
        string code = @"public class Service
{
    public void Work()
    {
        int x = 1;
    }

    // Line 8 is blank / comment between methods

    public void Done()
    {
    }
}";
        // Line 8 is after Work() closes at line 6
        var items = BreadcrumbResolver.ResolveBreadcrumbs("Service.cs", code, 8);
        Assert.Equal(2, items.Count);
        Assert.Equal("Service.cs", items[0].Name);
        Assert.Equal("Service", items[1].Name);
    }

    [Fact]
    public void DockerCliService_ParseContainerLines_PreservesFormat()
    {
        string output = "c123456\tmy-app\tmy-image:latest\trunning\tUp 2 hours\t0.0.0.0:8080->80/tcp\t2026-10-14";
        var items = DockerCliService.ParseContainerLines(output);
        Assert.Single(items);
        Assert.Equal("c123456", items[0].Id);
        Assert.Equal("my-app", items[0].Name);
        Assert.Equal("running", items[0].State);
    }
}
