using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AgduBugdu.Core.Database;

public class SqlColumnSchema
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "TEXT";
    public bool IsPrimaryKey { get; set; }
    public bool IsNullable { get; set; } = true;
}

public class SqlTableSchema
{
    public string TableName { get; set; } = string.Empty;
    public List<SqlColumnSchema> Columns { get; set; } = new();
}

public class SqlQueryResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> ColumnNames { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int RowsAffected { get; set; }
    public double ExecutionDurationMs { get; set; }
    public string SqlExecuted { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; } = DateTime.Now;
}

public class SqlQueryHistoryItem
{
    public string Query { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool Success { get; set; }
    public int RowCount { get; set; }
    public double DurationMs { get; set; }
}

public class SqlStudioEngine : IDisposable
{
    private SqliteConnection? _sharedConnection;
    private readonly string _defaultConnectionString = "Data Source=InMemoryDb;Mode=Memory;Cache=Shared";

    public SqlStudioEngine(string? connectionString = null)
    {
        string connStr = connectionString ?? _defaultConnectionString;
        _sharedConnection = new SqliteConnection(connStr);
        _sharedConnection.Open();

        // Seed with sample initial tables if memory
        if (connStr.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase))
        {
            SeedSampleDatabase();
        }
    }

    private void SeedSampleDatabase()
    {
        try
        {
            string seedScript = @"
CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    username TEXT NOT NULL UNIQUE,
    email TEXT NOT NULL,
    role TEXT DEFAULT 'User',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS projects (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER,
    name TEXT NOT NULL,
    status TEXT DEFAULT 'Active',
    budget REAL DEFAULT 0.0,
    FOREIGN KEY(user_id) REFERENCES users(id)
);

INSERT OR IGNORE INTO users (id, username, email, role) VALUES
(1, 'superadmin', 'admin@agdubugdu.com', 'Admin'),
(2, 'johndoe', 'john@acme.org', 'Developer'),
(3, 'janedoe', 'jane@design.io', 'Designer');

INSERT OR IGNORE INTO projects (id, user_id, name, status, budget) VALUES
(1, 1, 'AgduBugdu IDE Next-Gen', 'Active', 25000.0),
(2, 2, 'Cloud Sync Engine', 'In Progress', 12000.0),
(3, 3, 'Design System Refresh', 'Completed', 8500.0);
";
            using var cmd = _sharedConnection!.CreateCommand();
            cmd.CommandText = seedScript;
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Seed gracefully
        }
    }

    public SqlQueryResult Execute(string sql)
    {
        var result = new SqlQueryResult
        {
            SqlExecuted = sql,
            ExecutedAt = DateTime.Now
        };

        if (string.IsNullOrWhiteSpace(sql))
        {
            result.Success = false;
            result.ErrorMessage = "Query string cannot be empty.";
            return result;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            if (_sharedConnection == null || _sharedConnection.State != System.Data.ConnectionState.Open)
            {
                _sharedConnection = new SqliteConnection(_defaultConnectionString);
                _sharedConnection.Open();
            }

            using var cmd = _sharedConnection.CreateCommand();
            cmd.CommandText = sql;

            bool isSelect = sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                         || sql.TrimStart().StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase)
                         || sql.TrimStart().StartsWith("EXPLAIN", StringComparison.OrdinalIgnoreCase);

            if (isSelect)
            {
                using var reader = cmd.ExecuteReader();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    result.ColumnNames.Add(reader.GetName(i));
                }

                while (reader.Read())
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        row[result.ColumnNames[i]] = val;
                    }
                    result.Rows.Add(row);
                }
                result.RowsAffected = result.Rows.Count;
            }
            else
            {
                result.RowsAffected = cmd.ExecuteNonQuery();
            }

            sw.Stop();
            result.ExecutionDurationMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
            result.Success = true;
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.ExecutionDurationMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public List<SqlTableSchema> GetSchema()
    {
        var tables = new List<SqlTableSchema>();
        try
        {
            if (_sharedConnection == null || _sharedConnection.State != System.Data.ConnectionState.Open)
                return tables;

            var tableNames = new List<string>();
            using (var cmd = _sharedConnection.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    tableNames.Add(reader.GetString(0));
                }
            }

            foreach (var tableName in tableNames)
            {
                var tableSchema = new SqlTableSchema { TableName = tableName };
                using (var cmd = _sharedConnection.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA table_info('{tableName}');";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        // 1: name, 2: type, 3: notnull, 5: pk
                        string colName = reader.GetString(1);
                        string colType = reader.IsDBNull(2) ? "TEXT" : reader.GetString(2);
                        bool notNull = reader.GetInt32(3) == 1;
                        bool isPk = reader.GetInt32(5) == 1;

                        tableSchema.Columns.Add(new SqlColumnSchema
                        {
                            Name = colName,
                            DataType = colType,
                            IsNullable = !notNull,
                            IsPrimaryKey = isPk
                        });
                    }
                }
                tables.Add(tableSchema);
            }
        }
        catch
        {
            // Graceful fallback
        }

        return tables;
    }

    public static string ExportToCsv(SqlQueryResult result)
    {
        if (result.ColumnNames.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", result.ColumnNames));

        foreach (var row in result.Rows)
        {
            var lineItems = new List<string>();
            foreach (var col in result.ColumnNames)
            {
                var val = row.TryGetValue(col, out var v) && v != null ? v.ToString() ?? "" : "";
                if (val.Contains(',') || val.Contains('"') || val.Contains('\n'))
                {
                    val = $"\"{val.Replace("\"", "\"\"")}\"";
                }
                lineItems.Add(val);
            }
            sb.AppendLine(string.Join(",", lineItems));
        }

        return sb.ToString();
    }

    public static string ExportToJson(SqlQueryResult result)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(result.Rows, options);
    }

    public static string ExportToMarkdown(SqlQueryResult result)
    {
        if (result.ColumnNames.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("| ").Append(string.Join(" | ", result.ColumnNames)).AppendLine(" |");
        sb.Append("| ").Append(string.Join(" | ", result.ColumnNames.ConvertAll(_ => "---"))).AppendLine(" |");

        foreach (var row in result.Rows)
        {
            var line = new List<string>();
            foreach (var col in result.ColumnNames)
            {
                var val = row.TryGetValue(col, out var v) && v != null ? v.ToString() ?? "" : "";
                val = val.Replace("|", "\\|").Replace("\n", " ");
                line.Add(val);
            }
            sb.Append("| ").Append(string.Join(" | ", line)).AppendLine(" |");
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        _sharedConnection?.Dispose();
        _sharedConnection = null;
    }
}
