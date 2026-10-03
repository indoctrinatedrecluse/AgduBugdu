using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Plugin.Rust;

public class RustLanguageExtension : IExtension
{
    public string Id => "agdubugdu.language.rust";
    public string Name => "Rust Language Support";
    public string Version => "1.0.0";

    public static readonly LanguageDefinition RustDefinition = new(
        Id: "rust",
        Name: "Rust",
        FileExtensions: new[] { ".rs" },
        GrammarScope: "source.rust",
        LineCommentPrefix: "//",
        BlockCommentStart: "/*",
        BlockCommentEnd: "*/"
    );

    public static readonly IReadOnlyList<Snippet> Snippets = new List<Snippet>
    {
        new(
            Id: "rust.main",
            Name: "Rust Main Function",
            Prefix: "main",
            Description: "Standard Rust main entry point with println!",
            Body: "fn main() {\n    println!(\"Hello, World!\");\n}\n",
            LanguageId: "rust",
            Tags: new[] { "entrypoint", "boilerplate" }
        ),
        new(
            Id: "rust.fn",
            Name: "Function Declaration",
            Prefix: "fn",
            Description: "Standard function returning Result",
            Body: "fn process_data(input: &str) -> Result<String, Box<dyn std::error::Error>> {\n    Ok(input.to_string())\n}\n",
            LanguageId: "rust",
            Tags: new[] { "fn" }
        ),
        new(
            Id: "rust.struct",
            Name: "Struct Declaration",
            Prefix: "struct",
            Description: "Struct with Debug and Clone derives",
            Body: "#[derive(Debug, Clone)]\npub struct Item {\n    pub id: u64,\n    pub name: String,\n}\n",
            LanguageId: "rust",
            Tags: new[] { "struct" }
        ),
        new(
            Id: "rust.enum",
            Name: "Enum Declaration",
            Prefix: "enum",
            Description: "Enum with common derives",
            Body: "#[derive(Debug, PartialEq, Eq, Clone)]\npub enum Status {\n    Idle,\n    Running,\n    Failed(String),\n    Success,\n}\n",
            LanguageId: "rust",
            Tags: new[] { "enum" }
        ),
        new(
            Id: "rust.impl",
            Name: "Implementation Block",
            Prefix: "impl",
            Description: "Method implementation block with constructor",
            Body: "impl Item {\n    pub fn new(id: u64, name: impl Into<String>) -> Self {\n        Self {\n            id,\n            name: name.into(),\n        }\n    }\n}\n",
            LanguageId: "rust",
            Tags: new[] { "impl" }
        ),
        new(
            Id: "rust.test",
            Name: "Rust Unit Test Module",
            Prefix: "test",
            Description: "Embedded tests module with assertions",
            Body: "#[cfg(test)]\nmod tests {\n    use super::*;\n\n    #[test]\n    fn test_basic_operation() {\n        assert_eq!(2 + 2, 4);\n    }\n}\n",
            LanguageId: "rust",
            Tags: new[] { "testing" }
        ),
        new(
            Id: "rust.match",
            Name: "Pattern Matching",
            Prefix: "match",
            Description: "Exhaustive match expression",
            Body: "match result {\n    Ok(val) => println!(\"Value: {}\", val),\n    Err(err) => eprintln!(\"Error: {}\", err),\n}\n",
            LanguageId: "rust",
            Tags: new[] { "pattern", "control" }
        ),
        new(
            Id: "rust.println",
            Name: "Print Line Macro",
            Prefix: "pln",
            Description: "Formatted output to stdout",
            Body: "println!(\"{}\", message);\n",
            LanguageId: "rust",
            Tags: new[] { "io" }
        ),
        new(
            Id: "rust.forin",
            Name: "Iterator For Loop",
            Prefix: "forin",
            Description: "Loop over iterator elements",
            Body: "for item in items.iter() {\n    println!(\"{item:?}\");\n}\n",
            LanguageId: "rust",
            Tags: new[] { "loop" }
        ),
        new(
            Id: "rust.tokio",
            Name: "Tokio Async Main",
            Prefix: "tokiomain",
            Description: "Async entrypoint powered by Tokio runtime",
            Body: "#[tokio::main]\nasync fn main() -> Result<(), Box<dyn std::error::Error>> {\n    println!(\"Hello, Async Rust!\");\n    Ok(())\n}\n",
            LanguageId: "rust",
            Tags: new[] { "async", "tokio" }
        )
    };

    private IExtensionContext? _context;

    public void Initialize(IExtensionContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        // 1. Register language definition
        context.Languages.RegisterLanguage(RustDefinition);

        // 2. Register all snippets
        foreach (var snippet in Snippets)
        {
            context.Languages.RegisterSnippet(snippet);
        }

        // 3. Register commands
        context.Commands.RegisterCommand("rust.newFile", "Rust: New Rust File (main.rs)", () =>
        {
            var snippet = Snippets[0]; // Rust main
            context.EditorService.NewDocument("main.rs", snippet.Body);
            return Task.CompletedTask;
        });
    }

    public Task ActivateAsync()
    {
        _context?.Log("Rust Language Support extension activated.", "Info");
        return Task.CompletedTask;
    }

    public Task DeactivateAsync()
    {
        _context?.Log("Rust Language Support extension deactivated.", "Info");
        return Task.CompletedTask;
    }
}
