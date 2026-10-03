using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Plugin.Python;

public class PythonLanguageExtension : IExtension
{
    public string Id => "agdubugdu.language.python";
    public string Name => "Python Language Support";
    public string Version => "1.0.0";

    public static readonly LanguageDefinition PythonDefinition = new(
        Id: "python",
        Name: "Python",
        FileExtensions: new[] { ".py", ".pyw" },
        GrammarScope: "source.python",
        LineCommentPrefix: "#",
        BlockCommentStart: "\"\"\"",
        BlockCommentEnd: "\"\"\""
    );

    public static readonly IReadOnlyList<Snippet> Snippets = new List<Snippet>
    {
        new(
            Id: "python.main",
            Name: "Python Main Guard",
            Prefix: "main",
            Description: "Standard script entrypoint with main() function and guard",
            Body: "def main():\n    print(\"Hello, World!\")\n\n\nif __name__ == \"__main__\":\n    main()\n",
            LanguageId: "python",
            Tags: new[] { "entrypoint", "boilerplate" }
        ),
        new(
            Id: "python.def",
            Name: "Function Definition",
            Prefix: "def",
            Description: "Function definition with type hints and docstring",
            Body: "def compute_result(name: str, count: int = 1) -> bool:\n    \"\"\"Perform computation.\"\"\"\n    return True\n",
            LanguageId: "python",
            Tags: new[] { "func", "types" }
        ),
        new(
            Id: "python.class",
            Name: "Class Declaration",
            Prefix: "class",
            Description: "Class definition with __init__ constructor",
            Body: "class MyService:\n    def __init__(self, name: str):\n        self.name = name\n\n    def execute(self) -> None:\n        pass\n",
            LanguageId: "python",
            Tags: new[] { "oop", "class" }
        ),
        new(
            Id: "python.dataclass",
            Name: "Data Class",
            Prefix: "dataclass",
            Description: "Python dataclass definition",
            Body: "from dataclasses import dataclass\n\n@dataclass\nclass UserProfile:\n    id: int\n    username: str\n    is_active: bool = True\n",
            LanguageId: "python",
            Tags: new[] { "data", "modern" }
        ),
        new(
            Id: "python.tryexcept",
            Name: "Try-Except Block",
            Prefix: "try",
            Description: "Error handling block with exception capture",
            Body: "try:\n    pass\nexcept Exception as e:\n    print(f\"An error occurred: {e}\")\n",
            LanguageId: "python",
            Tags: new[] { "exceptions" }
        ),
        new(
            Id: "python.withopen",
            Name: "File Open Context Manager",
            Prefix: "withopen",
            Description: "Read file contents using context manager",
            Body: "with open(\"data.txt\", \"r\", encoding=\"utf-8\") as f:\n    content = f.read()\n",
            LanguageId: "python",
            Tags: new[] { "file", "io" }
        ),
        new(
            Id: "python.listcomp",
            Name: "List Comprehension",
            Prefix: "lcomp",
            Description: "Filter and map elements in a list comprehension",
            Body: "[item for item in collection if item is not None]",
            LanguageId: "python",
            Tags: new[] { "syntax", "comprehension" }
        ),
        new(
            Id: "python.print",
            Name: "F-String Print",
            Prefix: "pf",
            Description: "Print interpolated string to stdout",
            Body: "print(f\"{variable=}\")\n",
            LanguageId: "python",
            Tags: new[] { "io", "debug" }
        ),
        new(
            Id: "python.unittest",
            Name: "Unittest Test Case",
            Prefix: "test",
            Description: "Standard library unittest test suite",
            Body: "import unittest\n\nclass TestOperations(unittest.TestCase):\n    def test_sample(self):\n        self.assertEqual(2 * 2, 4)\n\nif __name__ == \"__main__\":\n    unittest.main()\n",
            LanguageId: "python",
            Tags: new[] { "testing" }
        ),
        new(
            Id: "python.fastapi",
            Name: "FastAPI Endpoint",
            Prefix: "fastapi",
            Description: "Minimal FastAPI application and root route",
            Body: "from fastapi import FastAPI\n\napp = FastAPI()\n\n@app.get(\"/\")\ndef root():\n    return {\"status\": \"ok\", \"message\": \"Hello from FastAPI\"}\n",
            LanguageId: "python",
            Tags: new[] { "web", "fastapi" }
        )
    };

    private IExtensionContext? _context;

    public void Initialize(IExtensionContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        // 1. Register language definition
        context.Languages.RegisterLanguage(PythonDefinition);

        // 2. Register all snippets
        foreach (var snippet in Snippets)
        {
            context.Languages.RegisterSnippet(snippet);
        }

        // 3. Register commands
        context.Commands.RegisterCommand("python.newFile", "Python: New Python File (main.py)", () =>
        {
            var snippet = Snippets[0]; // Python main
            context.EditorService.NewDocument("main.py", snippet.Body);
            return Task.CompletedTask;
        });
    }

    public Task ActivateAsync()
    {
        _context?.Log("Python Language Support extension activated.", "Info");
        return Task.CompletedTask;
    }

    public Task DeactivateAsync()
    {
        _context?.Log("Python Language Support extension deactivated.", "Info");
        return Task.CompletedTask;
    }
}
