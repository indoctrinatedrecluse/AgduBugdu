using System.Collections.Generic;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Core.Snippets;

public static class BuiltInSnippets
{
    public static IReadOnlyList<Snippet> GetDefaultSnippets()
    {
        return new List<Snippet>
        {
            // C#
            new("cs.ctor", "Constructor", "ctor", "Default parameterless constructor", "public ${ClassName}()\n{\n    $0\n}", "csharp"),
            new("cs.prop", "Property", "prop", "Auto-implemented property", "public ${string} ${Name} { get; set; }", "csharp"),
            new("cs.propg", "Get-only Property", "propg", "Property with private setter", "public ${string} ${Name} { get; private set; }", "csharp"),
            new("cs.for", "For Loop", "for", "Counted for loop", "for (int i = 0; i < ${length}; i++)\n{\n    $0\n}", "csharp"),
            new("cs.foreach", "Foreach Loop", "foreach", "Foreach collection iteration", "foreach (var item in ${collection})\n{\n    $0\n}", "csharp"),
            new("cs.try", "Try Catch", "try", "Try catch exception block", "try\n{\n    $0\n}\ncatch (Exception ex)\n{\n    \n}", "csharp"),
            new("cs.cw", "Console WriteLine", "cw", "System.Console.WriteLine", "Console.WriteLine($0);", "csharp"),
            new("cs.class", "Class", "class", "Public class declaration", "public class ${ClassName}\n{\n    $0\n}", "csharp"),

            // Python
            new("py.def", "Function Definition", "def", "Function definition with docstring", "def ${func_name}(${args}):\n    \"\"\"${doc}\"\"\"\n    $0", "python"),
            new("py.class", "Class Definition", "class", "Python class declaration", "class ${ClassName}:\n    def __init__(self${args}):\n        $0", "python"),
            new("py.main", "Main Guard", "main", "if __name__ == '__main__':", "if __name__ == \"__main__\":\n    $0", "python"),
            new("py.try", "Try Except", "try", "Try except exception block", "try:\n    $0\nexcept Exception as e:\n    pass", "python"),
            new("py.for", "For Loop", "for", "For item in iterable loop", "for ${item} in ${iterable}:\n    $0", "python"),

            // Rust
            new("rs.fn", "Function", "fn", "Rust function declaration", "fn ${name}(${args}) -> ${ret} {\n    $0\n}", "rust"),
            new("rs.struct", "Struct", "struct", "Rust struct definition", "pub struct ${Name} {\n    $0\n}", "rust"),
            new("rs.enum", "Enum", "enum", "Rust enum definition", "pub enum ${Name} {\n    $0\n}", "rust"),
            new("rs.impl", "Impl Block", "impl", "Rust impl block", "impl ${Name} {\n    $0\n}", "rust"),
            new("rs.println", "Println Macro", "println", "println! macro invocation", "println!(\"${text}\", $0);", "rust"),

            // C / C++
            new("cpp.main", "Main Function", "main", "Standard C/C++ main entrypoint", "int main(int argc, char* argv[])\n{\n    $0\n    return 0;\n}", "cpp"),
            new("cpp.cl", "Class Definition", "cl", "C++ class declaration", "class ${ClassName}\n{\npublic:\n    ${ClassName}();\n    ~${ClassName}();\nprivate:\n    $0\n};", "cpp"),
            new("cpp.cout", "Std Cout", "cout", "std::cout stream output", "std::cout << $0 << std::endl;", "cpp"),
            new("cpp.inc", "Include Header", "inc", "#include header preprocessor", "#include <${header}>$0", "cpp"),

            // Go
            new("go.func", "Function", "func", "Go function declaration", "func ${name}(${args}) ${ret} {\n    $0\n}", "go"),
            new("go.struct", "Struct Type", "struct", "Go struct type declaration", "type ${Name} struct {\n    $0\n}", "go"),
            new("go.inter", "Interface Type", "inter", "Go interface type declaration", "type ${Name} interface {\n    $0\n}", "go"),
            new("go.iferr", "If Error Check", "iferr", "if err != nil check", "if err != nil {\n    return $0\n}", "go"),

            // Java
            new("java.psvm", "Main Method", "psvm", "public static void main", "public static void main(String[] args) {\n    $0\n}", "java"),
            new("java.sout", "System Out Println", "sout", "System.out.println()", "System.out.println($0);", "java"),
            new("java.cl", "Class", "cl", "Public class declaration", "public class ${ClassName} {\n    $0\n}", "java"),

            // Markdown
            new("md.table", "Table", "table", "Markdown table template", "| ${Column 1} | ${Column 2} |\n| --- | --- |\n| $0 | |", "markdown"),
            new("md.link", "Link", "link", "Markdown hyperlink", "[${text}](${url})$0", "markdown"),
            new("md.img", "Image", "img", "Markdown image link", "![${alt}](${url})$0", "markdown"),
            new("md.code", "Code Fence", "code", "Fenced code block", "```${lang}\n$0\n```", "markdown"),

            // JSON
            new("json.kv", "Key Value Pair", "kv", "JSON string key value", "\"${key}\": \"${value}\"$0", "json"),
            new("json.obj", "Object", "obj", "JSON nested object", "\"${key}\": {\n    $0\n}", "json"),
            new("json.arr", "Array", "arr", "JSON array", "\"${key}\": [\n    $0\n]", "json")
        };
    }
}
