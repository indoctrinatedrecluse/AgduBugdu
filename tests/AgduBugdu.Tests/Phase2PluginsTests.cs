using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgduBugdu.App.Docking;
using AgduBugdu.App.ViewModels.Documents;
using AgduBugdu.App.ViewModels.Tools;
using AgduBugdu.Plugin.ColorPicker;
using AgduBugdu.Plugin.DataGridLive;
using AgduBugdu.Plugin.HexViewer;
using AgduBugdu.Plugin.RestClient;
using AgduBugdu.Plugin.SqliteExplorer;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AgduBugdu.Tests;

public class Phase2PluginsTests
{
    // ==========================================
    // 1. REST Client Tests
    // ==========================================
    [Fact]
    public void TestRestClient_HttpFileParser()
    {
        string httpFileContent = @"
@baseUrl = https://api.agdubugdu.test
@token = secret_12345

### Get Users List
# @name GetUsers
GET {{baseUrl}}/api/users?page=1
Accept: application/json
Authorization: Bearer {{token}}

### Create New User
POST {{baseUrl}}/api/users
Content-Type: application/json

{
  ""username"": ""antigravity"",
  ""role"": ""developer""
}
";
        var requests = HttpFileParser.ParseHttpFile(httpFileContent);
        Assert.Equal(2, requests.Count);

        var getReq = requests[0];
        Assert.Equal("GET", getReq.Method);
        Assert.Equal("https://api.agdubugdu.test/api/users?page=1", getReq.Url);
        Assert.Equal("application/json", getReq.Headers["Accept"]);
        Assert.Equal("Bearer secret_12345", getReq.Headers["Authorization"]);
        Assert.Null(getReq.Body);

        var postReq = requests[1];
        Assert.Equal("POST", postReq.Method);
        Assert.Equal("https://api.agdubugdu.test/api/users", postReq.Url);
        Assert.Equal("application/json", postReq.Headers["Content-Type"]);
        Assert.NotNull(postReq.Body);
        Assert.Contains("\"username\": \"antigravity\"", postReq.Body);
    }

    [Fact]
    public void TestRestClient_ViewModelBuildAndFormat()
    {
        var vm = new RestClientViewModel
        {
            Method = "POST",
            Url = "https://httpbin.org/post",
            HeadersText = "Content-Type: application/json\nX-Custom: Testing",
            BodyText = "{\"status\": \"ok\"}"
        };

        var req = vm.BuildCurrentRequest();
        Assert.Equal("POST", req.Method);
        Assert.Equal("https://httpbin.org/post", req.Url);
        Assert.Equal("application/json", req.Headers["Content-Type"]);
        Assert.Equal("Testing", req.Headers["X-Custom"]);

        string httpFormat = vm.ToHttpFileFormat();
        Assert.Contains("POST https://httpbin.org/post", httpFormat);
        Assert.Contains("Content-Type: application/json", httpFormat);
        Assert.Contains("{\"status\": \"ok\"}", httpFormat);
    }

    // ==========================================
    // 2. Hex Viewer Tests
    // ==========================================
    [Fact]
    public void TestHexViewer_DumperAndStats()
    {
        // 32 test bytes: first 16 ASCII "Hello, World! 12", next 16 zero bytes
        byte[] testBytes = new byte[32];
        Encoding.ASCII.GetBytes("Hello, World! 12").CopyTo(testBytes, 0);

        var lines = HexDumper.DumpBytes(testBytes, 0);
        Assert.Equal(2, lines.Count);

        // Line 0
        Assert.Equal(0, lines[0].Offset);
        Assert.Equal("00000000", lines[0].OffsetHex);
        Assert.Contains("48 65 6C 6C 6F", lines[0].HexFormatted);
        Assert.Equal("Hello, World! 12", lines[0].AsciiFormatted);

        // Line 1 (all zeros)
        Assert.Equal(16, lines[1].Offset);
        Assert.Equal("00000010", lines[1].OffsetHex);
        Assert.Equal("................", lines[1].AsciiFormatted);

        var stats = HexDumper.CalculateStats(testBytes);
        Assert.Equal(32, stats.FileSizeBytes);
        Assert.Equal(0.5, stats.NullByteRatio);
        Assert.Equal(0.5, stats.PrintableAsciiRatio);
        Assert.True(stats.ShannonEntropy > 0);
    }

    [Fact]
    public void TestHexViewer_ViewModelSearchAndPaging()
    {
        byte[] testBytes = new byte[8192]; // 2 pages of 4096
        for (int i = 0; i < testBytes.Length; i++) testBytes[i] = (byte)(i % 256);

        // Embed a distinct sequence at offset 5000
        byte[] needle = Encoding.ASCII.GetBytes("AGDUSIGNATURE");
        needle.CopyTo(testBytes, 5000);

        string tempFile = Path.Combine(Path.GetTempPath(), "hex_test_" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(tempFile, testBytes);

        try
        {
            var vm = new HexViewerViewModel();
            vm.LoadFile(tempFile);

            Assert.Equal(2, vm.TotalPages);
            Assert.Equal(1, vm.CurrentPage);
            Assert.NotEmpty(vm.DisplayLines);

            // Test Next Page
            vm.NextPage();
            Assert.Equal(2, vm.CurrentPage);
            Assert.Equal(4096, vm.CurrentOffset);

            // Test Search
            var found = vm.Search("AGDUSIGNATURE");
            Assert.NotNull(found);
            Assert.Equal(5000, found.Value);
            Assert.Equal(2, vm.CurrentPage); // 5000 is on page 2

            // Test Jump
            vm.GoToOffset(0);
            Assert.Equal(1, vm.CurrentPage);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    // ==========================================
    // 3. SQLite Explorer Tests
    // ==========================================
    [Fact]
    public async Task TestSqliteExplorer_SchemaAndQueryExecution()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), "test_db_" + Guid.NewGuid().ToString("N") + ".db");

        try
        {
            // Seed a small SQLite database
            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
CREATE TABLE Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL,
    Score REAL DEFAULT 100.0
);
INSERT INTO Users (Username, Score) VALUES ('alice', 98.5);
INSERT INTO Users (Username, Score) VALUES ('bob', 87.0);
INSERT INTO Users (Username, Score) VALUES ('charlie', 92.3);
";
                cmd.ExecuteNonQuery();
            }

            var vm = new SqliteExplorerViewModel();
            vm.OpenDatabase(dbPath);

            Assert.Single(vm.Tables);
            var table = vm.Tables[0];
            Assert.Equal("Users", table.TableName);
            Assert.Equal(3, table.RowCount);
            Assert.Equal(3, table.Columns.Count);
            Assert.Contains(table.Columns, c => c.Name == "Id" && c.IsPrimaryKey);
            Assert.Contains(table.Columns, c => c.Name == "Username" && c.DataType == "TEXT");

            // Execute SQL Query
            var result = await vm.ExecuteQueryAsync("SELECT Username, Score FROM Users ORDER BY Score DESC;");
            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.ColumnNames.Count);
            Assert.Equal(3, result.Rows.Count);
            Assert.Equal("alice", result.Rows[0][0]?.ToString());

            // Markdown Table Formatting
            string md = vm.FormatResultAsMarkdownTable();
            Assert.Contains("| Username | Score |", md);
            Assert.Contains("| alice |", md);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }

    // ==========================================
    // 4. Color Picker Tests
    // ==========================================
    [Fact]
    public void TestColorPicker_HexAndRgbConversions()
    {
        // Test parsing 6-digit hex
        Assert.True(ColorValue.TryParse("#3B82F6", out var c1));
        Assert.Equal(0x3B, c1.R);
        Assert.Equal(0x82, c1.G);
        Assert.Equal(0xF6, c1.B);
        Assert.Equal("#3B82F6", c1.Hex6);

        // Test parsing 3-digit shorthand
        Assert.True(ColorValue.TryParse("#FFF", out var c2));
        Assert.Equal(255, c2.R);
        Assert.Equal(255, c2.G);
        Assert.Equal(255, c2.B);

        // Test parsing rgb(r, g, b)
        Assert.True(ColorValue.TryParse("rgb(255, 128, 64)", out var c3));
        Assert.Equal(255, c3.R);
        Assert.Equal(128, c3.G);
        Assert.Equal(64, c3.B);

        // Test 8-digit Avalonia Hex
        Assert.True(ColorValue.TryParse("0xFF3B82F6", out var c4));
        Assert.Equal(255, c4.A);
        Assert.Equal("#3B82F6", c4.Hex6);
        Assert.Equal("0xFF3B82F6", c4.HexAvalonia);
    }

    [Fact]
    public void TestColorPicker_HslConversions()
    {
        // Pure Red: (0, 100%, 50%) -> RGB(255, 0, 0)
        var red = ColorValue.FromHsl(0, 100, 50);
        Assert.Equal(255, red.R);
        Assert.Equal(0, red.G);
        Assert.Equal(0, red.B);

        var (h, s, l) = red.ToHsl();
        Assert.Equal(0, (int)h);
        Assert.Equal(100, (int)s);
        Assert.Equal(50, (int)l);
    }

    [Fact]
    public void TestColorPicker_ViewModelSwatches()
    {
        var vm = new ColorPickerViewModel();
        Assert.NotEmpty(vm.Swatches);

        var first = vm.Swatches[0];
        vm.SelectSwatch(first);
        Assert.Equal(first.HexCode, vm.Hex6);
        Assert.Contains(vm.RecentSwatches, s => s.HexCode == first.HexCode);

        // Add custom swatch
        vm.Red = 120;
        vm.Green = 200;
        vm.Blue = 50;
        vm.AddCustomSwatch("MyMint");
        Assert.Contains(vm.Swatches, s => s.Name == "MyMint");
    }

    // ==========================================
    // 5. CsvDocumentViewModel & MarkdownDocumentViewModel Tests
    // ==========================================
    [Fact]
    public void TestCsvDocumentViewModel_LoadAndFilter()
    {
        var tempCsv = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempCsv, "Id,Name,Department,Salary\n1,Alice,Engineering,120000\n2,Bob,Design,95000\n3,Charlie,Engineering,110000");

            var doc = new CsvDocumentViewModel();
            doc.LoadFile(tempCsv);

            Assert.False(doc.HasError);
            Assert.Equal(3, doc.TotalRows);
            Assert.Equal(4, doc.TotalColumns);
            Assert.Equal(4, doc.Headers.Count);
            Assert.Equal(3, doc.DisplayRows.Count);
            Assert.Contains("CSV", doc.DelimiterDisplay);

            // Filter for Engineering
            doc.SearchQuery = "Engineering";
            Assert.Equal(2, doc.FilteredCount);
            Assert.Equal(2, doc.DisplayRows.Count);

            // Clear filter
            doc.ClearFilterCommand.Execute(null);
            Assert.Equal(string.Empty, doc.SearchQuery);
            Assert.Equal(3, doc.FilteredCount);
            Assert.Equal(3, doc.DisplayRows.Count);
        }
        finally
        {
            if (File.Exists(tempCsv))
            {
                try { File.Delete(tempCsv); } catch { }
            }
        }
    }

    [Fact]
    public void TestMarkdownDocumentViewModel_ParseBlocks()
    {
        var tempMd = Path.ChangeExtension(Path.GetTempFileName(), ".md");
        try
        {
            var content = @"# Main Title
## Sub Heading
A regular introduction paragraph.

> This is a notable quote block.

---

- Bullet item one
- Bullet item two

```csharp
public class Sample
{
    public int Value { get; set; }
}
```
";
            File.WriteAllText(tempMd, content);

            var doc = new MarkdownDocumentViewModel();
            doc.LoadFile(tempMd);

            Assert.False(doc.HasError);
            Assert.True(doc.IsVisualMode);
            Assert.NotEmpty(doc.Blocks);

            var headings = doc.Blocks.OfType<MarkdownHeadingBlock>().ToList();
            Assert.Equal(2, headings.Count);
            Assert.Equal("Main Title", headings[0].Text);
            Assert.Equal(1, headings[0].Level);
            Assert.Equal("Sub Heading", headings[1].Text);
            Assert.Equal(2, headings[1].Level);

            var paragraphs = doc.Blocks.OfType<MarkdownParagraphBlock>().ToList();
            Assert.Contains(paragraphs, p => p.Text.Contains("regular introduction paragraph"));

            var quote = doc.Blocks.OfType<MarkdownQuoteBlock>().Single();
            Assert.Contains("notable quote block", quote.Text);

            var divider = doc.Blocks.OfType<MarkdownDividerBlock>().Single();
            Assert.NotNull(divider);

            var listItems = doc.Blocks.OfType<MarkdownListItemBlock>().ToList();
            Assert.Equal(2, listItems.Count);
            Assert.Equal("Bullet item one", listItems[0].Text);

            var codeBlock = doc.Blocks.OfType<MarkdownCodeBlock>().Single();
            Assert.Equal("csharp", codeBlock.Language);
            Assert.Contains("public class Sample", codeBlock.Code);

            // Toggle view mode
            doc.ToggleViewModeCommand.Execute(null);
            Assert.False(doc.IsVisualMode);
        }
        finally
        {
            if (File.Exists(tempMd))
            {
                try { File.Delete(tempMd); } catch { }
            }
        }
    }

    // ==========================================
    // 6. MainDockFactory Plugin Tool Windows
    // ==========================================
    [Fact]
    public void TestMainDockFactory_PluginToolsRegistered()
    {
        var factory = new MainDockFactory();
        factory.CreateLayout();

        Assert.NotNull(factory.RestClientTool);
        Assert.Equal("RestClient", factory.RestClientTool.Id);
        Assert.Equal("REST Client", factory.RestClientTool.Title);

        Assert.NotNull(factory.HexViewerTool);
        Assert.Equal("HexViewer", factory.HexViewerTool.Id);
        Assert.Equal("Hex Inspector", factory.HexViewerTool.Title);

        Assert.NotNull(factory.SqliteExplorerTool);
        Assert.Equal("SqliteExplorer", factory.SqliteExplorerTool.Id);
        Assert.Equal("SQLite Explorer", factory.SqliteExplorerTool.Title);

        Assert.NotNull(factory.ColorPickerTool);
        Assert.Equal("ColorPicker", factory.ColorPickerTool.Id);
        Assert.Equal("Color Picker", factory.ColorPickerTool.Title);
    }

    [Fact]
    public void TestPluginToolViewModels_AttachModels()
    {
        // 1. RestClientToolViewModel
        var restTool = new RestClientToolViewModel();
        var restModel = new RestClientViewModel { Method = "GET", Url = "https://example.com" };
        restTool.AttachModel(restModel);
        Assert.Equal("GET", restTool.Method);
        Assert.Equal("https://example.com", restTool.Url);

        // 2. HexViewerToolViewModel
        var hexTool = new HexViewerToolViewModel();
        var hexModel = new HexViewerViewModel();
        hexTool.AttachModel(hexModel);
        Assert.Equal("Hex Inspector", hexTool.Title);

        // 3. SqliteExplorerToolViewModel
        var sqliteTool = new SqliteExplorerToolViewModel();
        var sqliteModel = new SqliteExplorerViewModel();
        sqliteTool.AttachModel(sqliteModel);
        Assert.Equal("SQLite Explorer", sqliteTool.Title);

        // 4. ColorPickerToolViewModel
        var colorTool = new ColorPickerToolViewModel();
        var colorModel = new ColorPickerViewModel();
        colorTool.AttachModel(colorModel);
        Assert.Equal("Color Picker", colorTool.Title);
        Assert.NotEmpty(colorTool.Swatches);
    }
}
