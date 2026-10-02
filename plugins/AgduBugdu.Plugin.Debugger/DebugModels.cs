using System;

namespace AgduBugdu.Plugin.Debugger;

public class DebugWatchItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = "string";

    public DebugWatchItem(string name, string value, string type = "string")
    {
        Name = name;
        Value = value;
        Type = type;
    }
}

public class CallStackFrame
{
    public string FunctionName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; } = 1;

    public string DisplayText => $"{FunctionName} at {FileName}:{LineNumber}";

    public CallStackFrame(string functionName, string filePath, int lineNumber)
    {
        FunctionName = functionName;
        FilePath = filePath;
        FileName = System.IO.Path.GetFileName(filePath);
        LineNumber = lineNumber;
    }
}
