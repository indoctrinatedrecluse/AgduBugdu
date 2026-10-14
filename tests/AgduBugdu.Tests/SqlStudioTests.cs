using System.Linq;
using AgduBugdu.Core.Database;
using Xunit;

namespace AgduBugdu.Tests;

public class SqlStudioTests
{
    [Fact]
    public void TestSqlSelectQueryExecution()
    {
        using var engine = new SqlStudioEngine();
        var result = engine.Execute("SELECT id, username, email FROM users ORDER BY id;");

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(3, result.ColumnNames.Count);
        Assert.True(result.Rows.Count >= 3);
        Assert.Equal("superadmin", result.Rows[0]["username"]?.ToString());
    }

    [Fact]
    public void TestSqlInsertAndUpdate()
    {
        using var engine = new SqlStudioEngine();
        var insertResult = engine.Execute("INSERT INTO users (username, email, role) VALUES ('testuser', 'test@example.com', 'Tester');");
        Assert.True(insertResult.Success);
        Assert.Equal(1, insertResult.RowsAffected);

        var selectResult = engine.Execute("SELECT role FROM users WHERE username = 'testuser';");
        Assert.True(selectResult.Success);
        Assert.Single(selectResult.Rows);
        Assert.Equal("Tester", selectResult.Rows[0]["role"]?.ToString());
    }

    [Fact]
    public void TestSqlSchemaDiscovery()
    {
        using var engine = new SqlStudioEngine();
        var schema = engine.GetSchema();

        Assert.NotEmpty(schema);
        var usersTable = schema.FirstOrDefault(t => t.TableName == "users");
        Assert.NotNull(usersTable);
        Assert.Contains(usersTable.Columns, c => c.Name == "username");
        Assert.Contains(usersTable.Columns, c => c.Name == "id" && c.IsPrimaryKey);
    }

    [Fact]
    public void TestSqlExportToCsvAndJsonAndMarkdown()
    {
        using var engine = new SqlStudioEngine();
        var result = engine.Execute("SELECT id, username FROM users ORDER BY id ASC LIMIT 2;");

        string csv = SqlStudioEngine.ExportToCsv(result);
        Assert.Contains("id,username", csv);
        Assert.Contains("superadmin", csv);

        string json = SqlStudioEngine.ExportToJson(result);
        Assert.StartsWith("[", json.Trim());
        Assert.Contains("superadmin", json);

        string md = SqlStudioEngine.ExportToMarkdown(result);
        Assert.Contains("| id | username |", md);
    }

    [Fact]
    public void TestSqlInvalidQueryErrorHandling()
    {
        using var engine = new SqlStudioEngine();
        var result = engine.Execute("SELECT * FROM non_existent_table_xyz;");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }
}
