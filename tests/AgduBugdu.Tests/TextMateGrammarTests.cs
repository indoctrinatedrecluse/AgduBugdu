using System;
using System.Linq;
using TextMateSharp.Grammars;
using Xunit;

namespace AgduBugdu.Tests;

public class TextMateGrammarTests
{
    [Fact]
    public void VerifyLanguagesInTextMate()
    {
        var reg = new RegistryOptions(ThemeName.DarkPlus);
        var langs = reg.GetAvailableLanguages().ToList();
        
        Assert.NotEmpty(langs);

        // Check our 6 languages
        var cpp = langs.FirstOrDefault(l => l.Id == "cpp" || l.Id == "c");
        var java = langs.FirstOrDefault(l => l.Id == "java");
        var go = langs.FirstOrDefault(l => l.Id == "go");
        var rust = langs.FirstOrDefault(l => l.Id == "rust");
        var python = langs.FirstOrDefault(l => l.Id == "python");
        var csharp = langs.FirstOrDefault(l => l.Id == "csharp");

        Assert.NotNull(cpp);
        Assert.NotNull(java);
        Assert.NotNull(go);
        Assert.NotNull(rust);
        Assert.NotNull(python);
        Assert.NotNull(csharp);

        // Check scopes
        Assert.Equal("source.cpp", reg.GetScopeByLanguageId("cpp"));
        Assert.Equal("source.java", reg.GetScopeByLanguageId("java"));
        Assert.Equal("source.go", reg.GetScopeByLanguageId("go"));
        Assert.Equal("source.rust", reg.GetScopeByLanguageId("rust"));
        Assert.Equal("source.python", reg.GetScopeByLanguageId("python"));
        Assert.Equal("source.cs", reg.GetScopeByLanguageId("csharp"));
    }
}
