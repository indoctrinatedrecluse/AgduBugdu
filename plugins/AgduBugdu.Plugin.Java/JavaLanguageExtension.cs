using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Plugin.Java;

public class JavaLanguageExtension : IExtension
{
    public string Id => "agdubugdu.language.java";
    public string Name => "Java Language Support";
    public string Version => "1.0.0";

    public static readonly LanguageDefinition JavaDefinition = new(
        Id: "java",
        Name: "Java",
        FileExtensions: new[] { ".java" },
        GrammarScope: "source.java",
        LineCommentPrefix: "//",
        BlockCommentStart: "/*",
        BlockCommentEnd: "*/"
    );

    public static readonly IReadOnlyList<Snippet> Snippets = new List<Snippet>
    {
        new(
            Id: "java.main",
            Name: "Java Main Entry Class",
            Prefix: "main",
            Description: "Standard public static void main method inside a class",
            Body: "public class Main {\n    public static void main(String[] args) {\n        System.out.println(\"Hello, World!\");\n    }\n}\n",
            LanguageId: "java",
            Tags: new[] { "entrypoint", "boilerplate" }
        ),
        new(
            Id: "java.class",
            Name: "Java Class",
            Prefix: "class",
            Description: "Public class declaration with constructor",
            Body: "public class MyClass {\n    public MyClass() {\n        \n    }\n}\n",
            LanguageId: "java",
            Tags: new[] { "oop", "class" }
        ),
        new(
            Id: "java.sout",
            Name: "Print to Standard Output (sout)",
            Prefix: "sout",
            Description: "System.out.println statement",
            Body: "System.out.println(\"Hello\");\n",
            LanguageId: "java",
            Tags: new[] { "io" }
        ),
        new(
            Id: "java.serr",
            Name: "Print to Standard Error (serr)",
            Prefix: "serr",
            Description: "System.err.println statement",
            Body: "System.err.println(\"Error\");\n",
            LanguageId: "java",
            Tags: new[] { "io", "error" }
        ),
        new(
            Id: "java.fori",
            Name: "For Index Loop",
            Prefix: "fori",
            Description: "Indexed for loop",
            Body: "for (int i = 0; i < count; i++) {\n    \n}\n",
            LanguageId: "java",
            Tags: new[] { "loop" }
        ),
        new(
            Id: "java.foreach",
            Name: "Enhanced For Loop",
            Prefix: "foreach",
            Description: "Iterate over an Iterable collection or array",
            Body: "for (var item : items) {\n    \n}\n",
            LanguageId: "java",
            Tags: new[] { "loop", "collection" }
        ),
        new(
            Id: "java.interface",
            Name: "Java Interface",
            Prefix: "interface",
            Description: "Public interface declaration",
            Body: "public interface IService {\n    void execute();\n}\n",
            LanguageId: "java",
            Tags: new[] { "oop", "interface" }
        ),
        new(
            Id: "java.record",
            Name: "Java Record",
            Prefix: "record",
            Description: "Immutable data record declaration",
            Body: "public record User(String id, String name) {\n}\n",
            LanguageId: "java",
            Tags: new[] { "data", "modern" }
        ),
        new(
            Id: "java.trycatch",
            Name: "Try-Catch Exception Block",
            Prefix: "try",
            Description: "Exception handling block with stack trace printing",
            Body: "try {\n    \n} catch (Exception e) {\n    e.printStackTrace();\n}\n",
            LanguageId: "java",
            Tags: new[] { "exceptions" }
        ),
        new(
            Id: "java.singleton",
            Name: "Bill Pugh Thread-Safe Singleton",
            Prefix: "singleton",
            Description: "Thread-safe lazy initialization singleton pattern",
            Body: "public class Singleton {\n    private Singleton() {}\n\n    private static class Helper {\n        private static final Singleton INSTANCE = new Singleton();\n    }\n\n    public static Singleton getInstance() {\n        return Helper.INSTANCE;\n    }\n}\n",
            LanguageId: "java",
            Tags: new[] { "pattern", "design" }
        ),
        new(
            Id: "java.junit",
            Name: "JUnit 5 Test Method",
            Prefix: "test",
            Description: "JUnit test method with assertion",
            Body: "@Test\nvoid shouldPassSuccessfully() {\n    org.junit.jupiter.api.Assertions.assertTrue(true);\n}\n",
            LanguageId: "java",
            Tags: new[] { "testing" }
        )
    };

    private IExtensionContext? _context;

    public void Initialize(IExtensionContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        // 1. Register language definition
        context.Languages.RegisterLanguage(JavaDefinition);

        // 2. Register all snippets
        foreach (var snippet in Snippets)
        {
            context.Languages.RegisterSnippet(snippet);
        }

        // 3. Register commands
        context.Commands.RegisterCommand("java.newFile", "Java: New Java Class (Main.java)", () =>
        {
            var snippet = Snippets[0]; // Java main
            context.EditorService.NewDocument("Main.java", snippet.Body);
            return Task.CompletedTask;
        });
    }

    public Task ActivateAsync()
    {
        _context?.Log("Java Language Support extension activated.", "Info");
        return Task.CompletedTask;
    }

    public Task DeactivateAsync()
    {
        _context?.Log("Java Language Support extension deactivated.", "Info");
        return Task.CompletedTask;
    }
}
