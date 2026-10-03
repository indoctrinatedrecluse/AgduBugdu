using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Plugin.Cpp;

public class CppLanguageExtension : IExtension
{
    public string Id => "agdubugdu.language.cpp";
    public string Name => "C/C++ Language Support";
    public string Version => "1.0.0";

    public static readonly LanguageDefinition CppDefinition = new(
        Id: "cpp",
        Name: "C/C++",
        FileExtensions: new[] { ".cpp", ".c", ".h", ".hpp", ".cc", ".cxx", ".hh" },
        GrammarScope: "source.cpp",
        LineCommentPrefix: "//",
        BlockCommentStart: "/*",
        BlockCommentEnd: "*/"
    );

    public static readonly IReadOnlyList<Snippet> Snippets = new List<Snippet>
    {
        new(
            Id: "cpp.main",
            Name: "C++ Main Function",
            Prefix: "main",
            Description: "Standard C++ main function with iostream",
            Body: "#include <iostream>\n\nint main(int argc, char* argv[]) {\n    std::cout << \"Hello, World!\" << std::endl;\n    return 0;\n}\n",
            LanguageId: "cpp",
            Tags: new[] { "boilerplate", "entrypoint" }
        ),
        new(
            Id: "c.main",
            Name: "C Main Function",
            Prefix: "cmain",
            Description: "Standard C main function with stdio.h",
            Body: "#include <stdio.h>\n\nint main(int argc, char* argv[]) {\n    printf(\"Hello, World!\\n\");\n    return 0;\n}\n",
            LanguageId: "cpp",
            Tags: new[] { "c", "boilerplate" }
        ),
        new(
            Id: "cpp.class",
            Name: "C++ Class Declaration",
            Prefix: "class",
            Description: "Declaration of a C++ class with constructor and destructor",
            Body: "class MyClass {\npublic:\n    MyClass();\n    virtual ~MyClass();\n\nprivate:\n    // member variables\n};\n",
            LanguageId: "cpp",
            Tags: new[] { "oop", "class" }
        ),
        new(
            Id: "cpp.struct",
            Name: "C/C++ Struct Declaration",
            Prefix: "struct",
            Description: "Declaration of a data structure",
            Body: "struct MyStruct {\n    int id;\n    char name[64];\n};\n",
            LanguageId: "cpp",
            Tags: new[] { "struct" }
        ),
        new(
            Id: "cpp.fori",
            Name: "For Index Loop",
            Prefix: "fori",
            Description: "Iterate from 0 to N with an index variable",
            Body: "for (int i = 0; i < count; ++i) {\n    \n}\n",
            LanguageId: "cpp",
            Tags: new[] { "loop" }
        ),
        new(
            Id: "cpp.forrange",
            Name: "Range-based For Loop",
            Prefix: "forr",
            Description: "C++11 range-based for loop over collection",
            Body: "for (const auto& item : items) {\n    \n}\n",
            LanguageId: "cpp",
            Tags: new[] { "loop", "c++11" }
        ),
        new(
            Id: "cpp.cout",
            Name: "Console Output (std::cout)",
            Prefix: "cout",
            Description: "Write standard output stream with newline",
            Body: "std::cout << \"Hello\" << std::endl;\n",
            LanguageId: "cpp",
            Tags: new[] { "io" }
        ),
        new(
            Id: "cpp.cin",
            Name: "Console Input (std::cin)",
            Prefix: "cin",
            Description: "Read standard input stream",
            Body: "std::cin >> value;\n",
            LanguageId: "cpp",
            Tags: new[] { "io" }
        ),
        new(
            Id: "cpp.headerguard",
            Name: "Header Include Guard",
            Prefix: "guard",
            Description: "Standard preprocessor include guard for headers",
            Body: "#ifndef HEADER_NAME_H\n#define HEADER_NAME_H\n\n// declarations\n\n#endif // HEADER_NAME_H\n",
            LanguageId: "cpp",
            Tags: new[] { "header", "preprocessor" }
        ),
        new(
            Id: "cpp.pragma",
            Name: "#pragma once Guard",
            Prefix: "pragma",
            Description: "Modern header guard pragma directive",
            Body: "#pragma once\n\n",
            LanguageId: "cpp",
            Tags: new[] { "header" }
        ),
        new(
            Id: "cpp.vector",
            Name: "std::vector declaration",
            Prefix: "vec",
            Description: "Standard vector container initialization",
            Body: "std::vector<int> items;\n",
            LanguageId: "cpp",
            Tags: new[] { "stl", "containers" }
        ),
        new(
            Id: "cpp.trycatch",
            Name: "Try-Catch Exception Block",
            Prefix: "try",
            Description: "Exception handling block with std::exception catch",
            Body: "try {\n    \n} catch (const std::exception& ex) {\n    std::cerr << \"Error: \" << ex.what() << std::endl;\n}\n",
            LanguageId: "cpp",
            Tags: new[] { "exceptions" }
        ),
        new(
            Id: "cpp.lambda",
            Name: "C++ Lambda Expression",
            Prefix: "lambda",
            Description: "Inline anonymous function closure",
            Body: "[&](auto& item) {\n    return true;\n}",
            LanguageId: "cpp",
            Tags: new[] { "lambda", "modern" }
        )
    };

    private IExtensionContext? _context;

    public void Initialize(IExtensionContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        // 1. Register language definition
        context.Languages.RegisterLanguage(CppDefinition);

        // 2. Register all snippets
        foreach (var snippet in Snippets)
        {
            context.Languages.RegisterSnippet(snippet);
        }

        // 3. Register commands
        context.Commands.RegisterCommand("cpp.newFile", "C++: New C++ File (main.cpp)", () =>
        {
            var snippet = Snippets[0]; // C++ main
            context.EditorService.NewDocument("main.cpp", snippet.Body);
            return Task.CompletedTask;
        });

        context.Commands.RegisterCommand("c.newFile", "C: New C File (main.c)", () =>
        {
            var snippet = Snippets[1]; // C main
            context.EditorService.NewDocument("main.c", snippet.Body);
            return Task.CompletedTask;
        });
    }

    public Task ActivateAsync()
    {
        _context?.Log("C/C++ Language Support extension activated.", "Info");
        return Task.CompletedTask;
    }

    public Task DeactivateAsync()
    {
        _context?.Log("C/C++ Language Support extension deactivated.", "Info");
        return Task.CompletedTask;
    }
}
