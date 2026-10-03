using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Plugin.CSharp;

public class CSharpLanguageExtension : IExtension
{
    public string Id => "agdubugdu.language.csharp";
    public string Name => "C# / .NET Language Support";
    public string Version => "1.0.0";

    public static readonly LanguageDefinition CSharpDefinition = new(
        Id: "csharp",
        Name: "C#",
        FileExtensions: new[] { ".cs", ".csx" },
        GrammarScope: "source.cs",
        LineCommentPrefix: "//",
        BlockCommentStart: "/*",
        BlockCommentEnd: "*/"
    );

    public static readonly IReadOnlyList<Snippet> Snippets = new List<Snippet>
    {
        new(
            Id: "cs.class",
            Name: "C# Class Declaration",
            Prefix: "class",
            Description: "Standard public class declaration with constructor",
            Body: "namespace MyNamespace;\n\npublic class MyClass\n{\n    public MyClass()\n    {\n    }\n}\n",
            LanguageId: "csharp",
            Tags: new[] { "oop", "class" }
        ),
        new(
            Id: "cs.prop",
            Name: "Auto Property",
            Prefix: "prop",
            Description: "Public auto-implemented property with getter and setter",
            Body: "public string Name { get; set; } = string.Empty;\n",
            LanguageId: "csharp",
            Tags: new[] { "property" }
        ),
        new(
            Id: "cs.propg",
            Name: "Property With Private Setter",
            Prefix: "propg",
            Description: "Property with public getter and private setter",
            Body: "public int Count { get; private set; }\n",
            LanguageId: "csharp",
            Tags: new[] { "property" }
        ),
        new(
            Id: "cs.ctor",
            Name: "Constructor",
            Prefix: "ctor",
            Description: "Public constructor definition",
            Body: "public MyClass()\n{\n    \n}\n",
            LanguageId: "csharp",
            Tags: new[] { "constructor" }
        ),
        new(
            Id: "cs.record",
            Name: "C# Record",
            Prefix: "record",
            Description: "Positional record declaration for immutable data",
            Body: "public record UserDto(string Id, string Name, string Email);\n",
            LanguageId: "csharp",
            Tags: new[] { "record", "data" }
        ),
        new(
            Id: "cs.interface",
            Name: "Interface Declaration",
            Prefix: "interface",
            Description: "Public interface declaration with method contract",
            Body: "public interface IService\n{\n    Task ExecuteAsync(CancellationToken cancellationToken = default);\n}\n",
            LanguageId: "csharp",
            Tags: new[] { "interface" }
        ),
        new(
            Id: "cs.async",
            Name: "Async Method",
            Prefix: "asyncm",
            Description: "Asynchronous method declaration returning Task",
            Body: "public async Task<int> ProcessAsync()\n{\n    await Task.Yield();\n    return 0;\n}\n",
            LanguageId: "csharp",
            Tags: new[] { "async" }
        ),
        new(
            Id: "cs.cw",
            Name: "Console.WriteLine",
            Prefix: "cw",
            Description: "Write line to standard output",
            Body: "Console.WriteLine(\"Hello, World!\");\n",
            LanguageId: "csharp",
            Tags: new[] { "io" }
        ),
        new(
            Id: "cs.trycatch",
            Name: "Try-Catch Block",
            Prefix: "try",
            Description: "Exception handling block with error output",
            Body: "try\n{\n    \n}\ncatch (Exception ex)\n{\n    Console.Error.WriteLine($\"Error: {ex.Message}\");\n}\n",
            LanguageId: "csharp",
            Tags: new[] { "exceptions" }
        ),
        new(
            Id: "cs.topmain",
            Name: "Top-Level Program",
            Prefix: "topmain",
            Description: "C# 9+ modern top-level statements entry point",
            Body: "using System;\n\nConsole.WriteLine(\"Hello from C# and .NET!\");\n",
            LanguageId: "csharp",
            Tags: new[] { "entrypoint", "toplevel" }
        ),
        new(
            Id: "cs.xunit",
            Name: "xUnit Test Method",
            Prefix: "fact",
            Description: "Unit test method attribute with assertion",
            Body: "[Fact]\npublic void Should_Perform_Action_Expectedly()\n{\n    // Arrange\n\n    // Act\n\n    // Assert\n    Assert.True(true);\n}\n",
            LanguageId: "csharp",
            Tags: new[] { "testing", "xunit" }
        ),
        new(
            Id: "cs.di",
            Name: "Dependency Injection Constructor Pattern",
            Prefix: "di",
            Description: "Primary dependency injection fields and constructor",
            Body: "private readonly ILogger<MyService> _logger;\n\npublic MyService(ILogger<MyService> logger)\n{\n    _logger = logger;\n}\n",
            LanguageId: "csharp",
            Tags: new[] { "di", "patterns" }
        )
    };

    private IExtensionContext? _context;

    public void Initialize(IExtensionContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        // 1. Register language definition
        context.Languages.RegisterLanguage(CSharpDefinition);

        // 2. Register all snippets
        foreach (var snippet in Snippets)
        {
            context.Languages.RegisterSnippet(snippet);
        }

        // 3. Register commands
        context.Commands.RegisterCommand("csharp.newFile", "C#: New C# File (Program.cs)", () =>
        {
            var snippet = Snippets[9]; // Top-level statements
            context.EditorService.NewDocument("Program.cs", snippet.Body);
            return Task.CompletedTask;
        });
    }

    public Task ActivateAsync()
    {
        _context?.Log("C# / .NET Language Support extension activated.", "Info");
        return Task.CompletedTask;
    }

    public Task DeactivateAsync()
    {
        _context?.Log("C# / .NET Language Support extension deactivated.", "Info");
        return Task.CompletedTask;
    }
}
