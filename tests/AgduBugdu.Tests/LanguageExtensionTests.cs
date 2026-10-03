using System.Linq;
using System.Threading.Tasks;
using AgduBugdu.Extensibility;
using AgduBugdu.Extensibility.Registries;
using AgduBugdu.Extensibility.Services;
using AgduBugdu.Plugin.Cpp;
using AgduBugdu.Plugin.CSharp;
using AgduBugdu.Plugin.Go;
using AgduBugdu.Plugin.Java;
using AgduBugdu.Plugin.Python;
using AgduBugdu.Plugin.Rust;
using Xunit;

namespace AgduBugdu.Tests;

public class LanguageExtensionTests
{
    private ExtensionContext CreateTestContext()
    {
        var commands = new CommandRegistry();
        var tools = new ToolWindowRegistry();
        var editor = new MockEditorService();
        var workspace = new MockWorkspaceService();
        var languages = new DefaultLanguageService();
        return new ExtensionContext(commands, tools, editor, workspace, null, languages);
    }

    [Fact]
    public async Task Cpp_Language_Extension_Registers_Definition_And_Snippets()
    {
        var context = CreateTestContext();
        var ext = new CppLanguageExtension();

        ext.Initialize(context);
        await ext.ActivateAsync();

        var lang = context.Languages.GetLanguageById("cpp");
        Assert.NotNull(lang);
        Assert.Equal("C/C++", lang.Name);
        Assert.Equal("source.cpp", lang.GrammarScope);
        Assert.Contains(".cpp", lang.FileExtensions);
        Assert.Contains(".c", lang.FileExtensions);
        Assert.Contains(".h", lang.FileExtensions);

        var fileLang = context.Languages.GetLanguageForFile("src/main.cpp");
        Assert.NotNull(fileLang);
        Assert.Equal("cpp", fileLang.Id);

        var snippets = context.Languages.GetSnippets("cpp");
        Assert.NotEmpty(snippets);
        Assert.Contains(snippets, s => s.Prefix == "main");
        Assert.Contains(snippets, s => s.Prefix == "class");
        Assert.Contains(snippets, s => s.Prefix == "cout");

        var registeredCommands = context.Commands.GetRegisteredCommands();
        Assert.True(registeredCommands.ContainsKey("cpp.newFile"));
        Assert.True(registeredCommands.ContainsKey("c.newFile"));

        await ext.DeactivateAsync();
    }

    [Fact]
    public async Task Java_Language_Extension_Registers_Definition_And_Snippets()
    {
        var context = CreateTestContext();
        var ext = new JavaLanguageExtension();

        ext.Initialize(context);
        await ext.ActivateAsync();

        var lang = context.Languages.GetLanguageById("java");
        Assert.NotNull(lang);
        Assert.Equal("Java", lang.Name);
        Assert.Equal("source.java", lang.GrammarScope);
        Assert.Contains(".java", lang.FileExtensions);

        var fileLang = context.Languages.GetLanguageForFile("Application.java");
        Assert.NotNull(fileLang);
        Assert.Equal("java", fileLang.Id);

        var snippets = context.Languages.GetSnippets("java");
        Assert.NotEmpty(snippets);
        Assert.Contains(snippets, s => s.Prefix == "main");
        Assert.Contains(snippets, s => s.Prefix == "sout");
        Assert.Contains(snippets, s => s.Prefix == "record");

        var registeredCommands = context.Commands.GetRegisteredCommands();
        Assert.True(registeredCommands.ContainsKey("java.newFile"));

        await ext.DeactivateAsync();
    }

    [Fact]
    public async Task Go_Language_Extension_Registers_Definition_And_Snippets()
    {
        var context = CreateTestContext();
        var ext = new GoLanguageExtension();

        ext.Initialize(context);
        await ext.ActivateAsync();

        var lang = context.Languages.GetLanguageById("go");
        Assert.NotNull(lang);
        Assert.Equal("Go", lang.Name);
        Assert.Equal("source.go", lang.GrammarScope);
        Assert.Contains(".go", lang.FileExtensions);

        var fileLang = context.Languages.GetLanguageForFile("server/main.go");
        Assert.NotNull(fileLang);
        Assert.Equal("go", fileLang.Id);

        var snippets = context.Languages.GetSnippets("go");
        Assert.NotEmpty(snippets);
        Assert.Contains(snippets, s => s.Prefix == "main");
        Assert.Contains(snippets, s => s.Prefix == "iferr");
        Assert.Contains(snippets, s => s.Prefix == "struct");

        var registeredCommands = context.Commands.GetRegisteredCommands();
        Assert.True(registeredCommands.ContainsKey("go.newFile"));

        await ext.DeactivateAsync();
    }

    [Fact]
    public async Task Rust_Language_Extension_Registers_Definition_And_Snippets()
    {
        var context = CreateTestContext();
        var ext = new RustLanguageExtension();

        ext.Initialize(context);
        await ext.ActivateAsync();

        var lang = context.Languages.GetLanguageById("rust");
        Assert.NotNull(lang);
        Assert.Equal("Rust", lang.Name);
        Assert.Equal("source.rust", lang.GrammarScope);
        Assert.Contains(".rs", lang.FileExtensions);

        var fileLang = context.Languages.GetLanguageForFile("lib.rs");
        Assert.NotNull(fileLang);
        Assert.Equal("rust", fileLang.Id);

        var snippets = context.Languages.GetSnippets("rust");
        Assert.NotEmpty(snippets);
        Assert.Contains(snippets, s => s.Prefix == "main");
        Assert.Contains(snippets, s => s.Prefix == "struct");
        Assert.Contains(snippets, s => s.Prefix == "match");

        var registeredCommands = context.Commands.GetRegisteredCommands();
        Assert.True(registeredCommands.ContainsKey("rust.newFile"));

        await ext.DeactivateAsync();
    }

    [Fact]
    public async Task Python_Language_Extension_Registers_Definition_And_Snippets()
    {
        var context = CreateTestContext();
        var ext = new PythonLanguageExtension();

        ext.Initialize(context);
        await ext.ActivateAsync();

        var lang = context.Languages.GetLanguageById("python");
        Assert.NotNull(lang);
        Assert.Equal("Python", lang.Name);
        Assert.Equal("source.python", lang.GrammarScope);
        Assert.Contains(".py", lang.FileExtensions);

        var fileLang = context.Languages.GetLanguageForFile("script.py");
        Assert.NotNull(fileLang);
        Assert.Equal("python", fileLang.Id);

        var snippets = context.Languages.GetSnippets("python");
        Assert.NotEmpty(snippets);
        Assert.Contains(snippets, s => s.Prefix == "main");
        Assert.Contains(snippets, s => s.Prefix == "dataclass");
        Assert.Contains(snippets, s => s.Prefix == "def");

        var registeredCommands = context.Commands.GetRegisteredCommands();
        Assert.True(registeredCommands.ContainsKey("python.newFile"));

        await ext.DeactivateAsync();
    }

    [Fact]
    public async Task CSharp_Language_Extension_Registers_Definition_And_Snippets()
    {
        var context = CreateTestContext();
        var ext = new CSharpLanguageExtension();

        ext.Initialize(context);
        await ext.ActivateAsync();

        var lang = context.Languages.GetLanguageById("csharp");
        Assert.NotNull(lang);
        Assert.Equal("C#", lang.Name);
        Assert.Equal("source.cs", lang.GrammarScope);
        Assert.Contains(".cs", lang.FileExtensions);

        var fileLang = context.Languages.GetLanguageForFile("Services/Worker.cs");
        Assert.NotNull(fileLang);
        Assert.Equal("csharp", fileLang.Id);

        var snippets = context.Languages.GetSnippets("csharp");
        Assert.NotEmpty(snippets);
        Assert.Contains(snippets, s => s.Prefix == "prop");
        Assert.Contains(snippets, s => s.Prefix == "record");
        Assert.Contains(snippets, s => s.Prefix == "cw");

        var registeredCommands = context.Commands.GetRegisteredCommands();
        Assert.True(registeredCommands.ContainsKey("csharp.newFile"));

        await ext.DeactivateAsync();
    }

    [Fact]
    public async Task All_Six_Languages_Can_Coexist_And_Be_Queried()
    {
        var context = CreateTestContext();

        var extensions = new PluginContracts.IExtension[]
        {
            new CppLanguageExtension(),
            new JavaLanguageExtension(),
            new GoLanguageExtension(),
            new RustLanguageExtension(),
            new PythonLanguageExtension(),
            new CSharpLanguageExtension()
        };

        foreach (var ext in extensions)
        {
            ext.Initialize(context);
            await ext.ActivateAsync();
        }

        var allLangs = context.Languages.GetLanguages();
        Assert.Equal(6, allLangs.Count);

        var allSnippets = context.Languages.GetSnippets();
        Assert.True(allSnippets.Count >= 50);

        Assert.Equal("cpp", context.Languages.GetLanguageForFile("test.hpp")?.Id);
        Assert.Equal("java", context.Languages.GetLanguageForFile("App.java")?.Id);
        Assert.Equal("go", context.Languages.GetLanguageForFile("main.go")?.Id);
        Assert.Equal("rust", context.Languages.GetLanguageForFile("main.rs")?.Id);
        Assert.Equal("python", context.Languages.GetLanguageForFile("app.py")?.Id);
        Assert.Equal("csharp", context.Languages.GetLanguageForFile("Program.cs")?.Id);

        foreach (var ext in extensions)
        {
            await ext.DeactivateAsync();
        }
    }
}
