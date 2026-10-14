using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace AgduBugdu.Plugin.SqliteExplorer;

public class SqliteExplorerViewModel
{
    private string? _databasePath;

    public string Title => "SQLite Database Explorer";
    public string? DatabasePath => _databasePath;
    public string DatabaseName => string.IsNullOrEmpty(_databasePath) ? "No Database Opened" : Path.GetFileName(_databasePath);
    public List<SqliteTableSchema> Tables { get; private set; } = new();
    public SqliteTableSchema? SelectedTable { get; set; }
    public string CurrentQueryText { get; set; } = "SELECT * FROM sqlite_master LIMIT 50;";
    public SqliteQueryResult? LastQueryResult { get; private set; }
    public string StatusMessage { get; private set; } = "Ready";

    public event EventHandler? DatabaseOpened;
    public event EventHandler? QueryExecuted;

    public void OpenDatabase(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Database file not found", path);

        _databasePath = path;
        RefreshSchema();
        DatabaseOpened?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshSchema()
    {
        if (string.IsNullOrEmpty(_databasePath) || !File.Exists(_databasePath))
            return;

        var tables = new List<SqliteTableSchema>();
        string connStr = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        try
        {
            using var conn = new SqliteConnection(connStr);
            conn.Open();

            // Query tables and views
            const string masterQuery = "SELECT type, name, sql FROM sqlite_master WHERE type IN ('table', 'view') AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            using var cmd = new SqliteCommand(masterQuery, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var table = new SqliteTableSchema
                {
                    Type = reader.GetString(0),
                    TableName = reader.GetString(1),
                    Sql = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
                };
                tables.Add(table);
            }

            // Populate columns & row counts
            foreach (var table in tables)
            {
                try
                {
                    using var colCmd = new SqliteCommand($"PRAGMA table_info(\"{table.TableName}\");", conn);
                    using var colReader = colCmd.ExecuteReader();
                    while (colReader.Read())
                    {
                        table.Columns.Add(new SqliteColumnInfo
                        {
                            ColumnId = colReader.GetInt32(0),
                            Name = colReader.GetString(1),
                            DataType = colReader.IsDBNull(2) ? "TEXT" : colReader.GetString(2),
                            NotNull = colReader.GetInt32(3) == 1,
                            DefaultValue = colReader.IsDBNull(4) ? null : colReader.GetString(4),
                            IsPrimaryKey = colReader.GetInt32(5) >= 1
                        });
                    }

                    if (table.Type == "table")
                    {
                        using var countCmd = new SqliteCommand($"SELECT COUNT(*) FROM \"{table.TableName}\";", conn);
                        var countObj = countCmd.ExecuteScalar();
                        if (countObj is long cnt) table.RowCount = cnt;
                        else if (countObj != null) table.RowCount = Convert.ToInt64(countObj);
                    }
                }
                catch { }
            }

            Tables = tables;
            StatusMessage = $"Loaded {tables.Count} tables/views from {DatabaseName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening database: {ex.Message}";
        }
    }

    public async Task<SqliteQueryResult> ExecuteQueryAsync(string? sql = null)
    {
        string query = string.IsNullOrWhiteSpace(sql) ? CurrentQueryText : sql;
        var result = new SqliteQueryResult { QueryText = query };

        if (string.IsNullOrEmpty(_databasePath) || !File.Exists(_databasePath))
        {
            result.ErrorMessage = "No active database opened.";
            LastQueryResult = result;
            return result;
        }

        string connStr = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        var sw = Stopwatch.StartNew();

        try
        {
            await using var conn = new SqliteConnection(connStr);
            await conn.OpenAsync().ConfigureAwait(false);

            await using var cmd = new SqliteCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

            for (int i = 0; i < reader.FieldCount; i++)
            {
                result.ColumnNames.Add(reader.GetName(i));
            }

            int count = 0;
            while (await reader.ReadAsync().ConfigureAwait(false) && count < 1000) // cap to 1000 rows
            {
                count++;
                var row = new List<object?>(reader.FieldCount);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i));
                }
                result.Rows.Add(row);
            }

            sw.Stop();
            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            result.RowsAffected = count;
            StatusMessage = $"Executed query in {result.ElapsedMilliseconds} ms ({result.Rows.Count} rows)";
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            result.ErrorMessage = ex.Message;
            StatusMessage = $"Query error: {ex.Message}";
        }
        finally
        {
            LastQueryResult = result;
            QueryExecuted?.Invoke(this, EventArgs.Empty);
        }

        return result;
    }

    public string FormatResultAsMarkdownTable()
    {
        if (LastQueryResult == null || LastQueryResult.ColumnNames.Count == 0)
            return "No query results to display.";

        var sb = new StringBuilder();
        // Header row
        sb.Append("| ");
        sb.Append(string.Join(" | ", LastQueryResult.ColumnNames));
        sb.AppendLine(" |");

        // Separator row
        sb.Append("| ");
        sb.Append(string.Join(" | ", LastQueryResult.ColumnNames.ConvertAll(_ => "---")));
        sb.AppendLine(" |");

        // Data rows
        foreach (var row in LastQueryResult.Rows)
        {
            sb.Append("| ");
            var formattedValues = row.ConvertAll(v => v?.ToString()?.Replace("\n", " ") ?? "*NULL*");
            sb.Append(string.Join(" | ", formattedValues));
            sb.AppendLine(" |");
        }

        return sb.ToString();
    }
}
