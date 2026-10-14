using System.Net.Http;
using System.Threading.Tasks;
using AgduBugdu.Core.Network;
using Xunit;

namespace AgduBugdu.Tests;

public class MockServerTests
{
    [Fact]
    public void TestDefaultRoutesAreLoaded()
    {
        using var engine = new MockServerEngine(port: 5991);
        Assert.NotEmpty(engine.Routes);
        Assert.Contains(engine.Routes, r => r.Path == "/health");
        Assert.Contains(engine.Routes, r => r.Path == "/api/v1/users");
    }

    [Fact]
    public async Task TestMockServerLifecycleAndRequestHandling()
    {
        int testPort = 5992;
        using var engine = new MockServerEngine(port: testPort);
        bool started = engine.Start();

        if (started)
        {
            try
            {
                using var client = new HttpClient();
                var resp = await client.GetAsync($"http://localhost:{testPort}/health");
                Assert.True(resp.IsSuccessStatusCode);

                string content = await resp.Content.ReadAsStringAsync();
                Assert.Contains("healthy", content);

                Assert.NotEmpty(engine.RequestLogs);
                Assert.Equal("GET", engine.RequestLogs[0].Method);
                Assert.Equal("/health", engine.RequestLogs[0].Path);
            }
            finally
            {
                engine.Stop();
            }
        }
    }

    [Fact]
    public void TestAddCustomRoute()
    {
        using var engine = new MockServerEngine(port: 5993);
        engine.Routes.Add(new MockRouteRule
        {
            Name = "Order Details",
            Method = "GET",
            Path = "/api/v1/orders/123",
            StatusCode = 200,
            ResponseBody = "{\"orderId\": 123, \"total\": 99.99}"
        });

        Assert.Contains(engine.Routes, r => r.Path == "/api/v1/orders/123");
    }
}
