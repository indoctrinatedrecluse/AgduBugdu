using System.Linq;
using AgduBugdu.Core.Network;
using Xunit;

namespace AgduBugdu.Tests;

public class NetworkSuiteTests
{
    [Fact]
    public void SocketMessageItem_Formatting_ReturnsAccurateBadgesAndColors()
    {
        var sent = new SocketMessageItem
        {
            Direction = SocketMessageDirection.Sent,
            Payload = "{\"type\":\"ping\"}"
        };

        Assert.Equal("⬆ TX", sent.DirectionBadge);
        Assert.Equal("#3B82F6", sent.BadgeColor);
        Assert.Equal(15, sent.ByteLength);

        var received = new SocketMessageItem
        {
            Direction = SocketMessageDirection.Received,
            Payload = "{\"type\":\"pong\"}"
        };

        Assert.Equal("⬇ RX", received.DirectionBadge);
        Assert.Equal("#10B981", received.BadgeColor);

        var sys = new SocketMessageItem
        {
            Direction = SocketMessageDirection.System,
            Payload = "Connected"
        };

        Assert.Equal("⚙ SYS", sys.DirectionBadge);
        Assert.Equal("#F59E0B", sys.BadgeColor);
    }

    [Fact]
    public void SseParser_SingleMessage_ParsesEventAndData()
    {
        string raw = "event: message\ndata: Hello World\nid: 101\n\n";
        var events = SseClientService.ParseSseChunk(raw);

        Assert.Single(events);
        Assert.Equal("message", events[0].EventType);
        Assert.Equal("Hello World", events[0].Data);
        Assert.Equal("101", events[0].EventId);
    }

    [Fact]
    public void SseParser_MultiLineDataAndComments_CombinesDataAndIgnoresComments()
    {
        string raw = @": this is a comment
event: delta
id: 42
data: line 1
data: line 2
data: line 3

";
        var events = SseClientService.ParseSseChunk(raw);

        Assert.Single(events);
        Assert.Equal("delta", events[0].EventType);
        Assert.Equal("42", events[0].EventId);
        Assert.Equal("line 1\nline 2\nline 3", events[0].Data);
    }

    [Fact]
    public void SseParser_MultipleEventsInSingleChunk_ParsesAll()
    {
        string raw = @"event: greeting
data: hello

event: update
data: score: 100

";
        var events = SseClientService.ParseSseChunk(raw);

        Assert.Equal(2, events.Count);
        Assert.Equal("greeting", events[0].EventType);
        Assert.Equal("hello", events[0].Data);

        Assert.Equal("update", events[1].EventType);
        Assert.Equal("score: 100", events[1].Data);
    }

    [Fact]
    public void GraphQlClient_IntrospectionQuery_ContainsSchemaFields()
    {
        string query = GraphQlClientService.GetIntrospectionQuery();
        Assert.Contains("__schema", query);
        Assert.Contains("queryType", query);
        Assert.Contains("mutationType", query);
        Assert.Contains("types", query);
    }
}
