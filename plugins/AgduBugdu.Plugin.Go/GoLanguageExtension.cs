using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Plugin.Go;

public class GoLanguageExtension : IExtension
{
    public string Id => "agdubugdu.language.go";
    public string Name => "Go Language Support";
    public string Version => "1.0.0";

    public static readonly LanguageDefinition GoDefinition = new(
        Id: "go",
        Name: "Go",
        FileExtensions: new[] { ".go" },
        GrammarScope: "source.go",
        LineCommentPrefix: "//",
        BlockCommentStart: "/*",
        BlockCommentEnd: "*/"
    );

    public static readonly IReadOnlyList<Snippet> Snippets = new List<Snippet>
    {
        new(
            Id: "go.main",
            Name: "Go Main Package",
            Prefix: "main",
            Description: "Standard main package entry point with fmt import",
            Body: "package main\n\nimport (\n\t\"fmt\"\n)\n\nfunc main() {\n\tfmt.Println(\"Hello, World!\")\n}\n",
            LanguageId: "go",
            Tags: new[] { "entrypoint", "boilerplate" }
        ),
        new(
            Id: "go.func",
            Name: "Function Declaration",
            Prefix: "func",
            Description: "Standard function declaration",
            Body: "func functionName(param string) error {\n\treturn nil\n}\n",
            LanguageId: "go",
            Tags: new[] { "func" }
        ),
        new(
            Id: "go.method",
            Name: "Method Receiver Declaration",
            Prefix: "meth",
            Description: "Method declaration with pointer receiver",
            Body: "func (s *StructName) MethodName() error {\n\treturn nil\n}\n",
            LanguageId: "go",
            Tags: new[] { "method", "oop" }
        ),
        new(
            Id: "go.struct",
            Name: "Struct Declaration",
            Prefix: "struct",
            Description: "Type definition for struct with JSON tags",
            Body: "type Config struct {\n\tID   string `json:\"id\"`\n\tName string `json:\"name\"`\n}\n",
            LanguageId: "go",
            Tags: new[] { "struct", "json" }
        ),
        new(
            Id: "go.interface",
            Name: "Interface Declaration",
            Prefix: "interface",
            Description: "Interface definition with method signatures",
            Body: "type Service interface {\n\tExecute() error\n}\n",
            LanguageId: "go",
            Tags: new[] { "interface" }
        ),
        new(
            Id: "go.iferr",
            Name: "If Error Not Nil",
            Prefix: "iferr",
            Description: "Idiomatic Go error check and return",
            Body: "if err != nil {\n\treturn err\n}\n",
            LanguageId: "go",
            Tags: new[] { "error", "idiomatic" }
        ),
        new(
            Id: "go.goroutine",
            Name: "Anonymous Goroutine",
            Prefix: "go",
            Description: "Execute a concurrently spawned anonymous goroutine",
            Body: "go func() {\n\t// concurrent work\n}()\n",
            LanguageId: "go",
            Tags: new[] { "concurrency", "goroutine" }
        ),
        new(
            Id: "go.forr",
            Name: "For Range Loop",
            Prefix: "forr",
            Description: "Iterate with index and value using range",
            Body: "for i, v := range items {\n\t_ = i\n\t_ = v\n}\n",
            LanguageId: "go",
            Tags: new[] { "loop", "range" }
        ),
        new(
            Id: "go.test",
            Name: "Unit Test Function",
            Prefix: "test",
            Description: "Testing framework unit test signature",
            Body: "func TestFeature(t *testing.T) {\n\tif false {\n\t\tt.Errorf(\"expected true, got false\")\n\t}\n}\n",
            LanguageId: "go",
            Tags: new[] { "testing" }
        ),
        new(
            Id: "go.http",
            Name: "HTTP Server Boilerplate",
            Prefix: "http",
            Description: "Basic HTTP handler and listen on port 8080",
            Body: "package main\n\nimport (\n\t\"fmt\"\n\t\"net/http\"\n)\n\nfunc main() {\n\thttp.HandleFunc(\"/\", func(w http.ResponseWriter, r *http.Request) {\n\t\tfmt.Fprintf(w, \"Hello, Go Web Server!\")\n\t})\n\n\tfmt.Println(\"Server listening on :8080\")\n\thttp.ListenAndServe(\":8080\", nil)\n}\n",
            LanguageId: "go",
            Tags: new[] { "http", "web" }
        )
    };

    private IExtensionContext? _context;

    public void Initialize(IExtensionContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        // 1. Register language definition
        context.Languages.RegisterLanguage(GoDefinition);

        // 2. Register all snippets
        foreach (var snippet in Snippets)
        {
            context.Languages.RegisterSnippet(snippet);
        }

        // 3. Register commands
        context.Commands.RegisterCommand("go.newFile", "Go: New Go File (main.go)", () =>
        {
            var snippet = Snippets[0]; // Go main
            context.EditorService.NewDocument("main.go", snippet.Body);
            return Task.CompletedTask;
        });
    }

    public Task ActivateAsync()
    {
        _context?.Log("Go Language Support extension activated.", "Info");
        return Task.CompletedTask;
    }

    public Task DeactivateAsync()
    {
        _context?.Log("Go Language Support extension deactivated.", "Info");
        return Task.CompletedTask;
    }
}
