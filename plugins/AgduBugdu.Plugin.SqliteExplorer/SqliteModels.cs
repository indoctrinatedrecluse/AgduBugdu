using System;
using System.Collections.Generic;

namespace AgduBugdu.Plugin.SqliteExplorer;

public class SqliteColumnInfo
{
    public int ColumnId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "TEXT";
    public bool NotNull { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsPrimaryKey { get; set; }
    public string PrimaryKeyIndicator => IsPrimaryKey ? "🔑 " : "";
    public string DisplayText => $"{PrimaryKeyIndicator}{Name} ({DataType})";
}

public class SqliteTableSchema
{
    public string TableName { get; set; } = string.Empty;
    public string Type { get; set; } = "table"; // "table" or "view"
    public string Sql { get; set; } = string.Empty;
    public long RowCount { get; set; }
    public List<SqliteColumnInfo> Columns { get; } = new();

    public string SummaryText => $"{TableName} ({RowCount:N0} rows, {Columns.Count} cols)";
}

public class SqliteQueryResult
{
    public string QueryText { get; set; } = string.Empty;
    public List<string> ColumnNames { get; } = new();
    public List<List<object?>> Rows { get; } = new();
    public long ElapsedMilliseconds { get; set; }
    public int RowsAffected { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);
    public string StatusBadge => IsSuccess
        ? $"Success ({Rows.Count} rows in {ElapsedMilliseconds} ms)"
        : $"Error: {ErrorMessage}";
}
