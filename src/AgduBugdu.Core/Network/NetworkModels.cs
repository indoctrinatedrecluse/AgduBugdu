using System;
using System.Collections.Generic;

namespace AgduBugdu.Core.Network;

public enum NetworkProtocolType
{
    WebSocket,
    ServerSentEvents,
    GraphQL
}

public enum SocketMessageDirection
{
    Sent,
    Received,
    System
}

public class SocketMessageItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public SocketMessageDirection Direction { get; set; }
    public string Payload { get; set; } = string.Empty;
    public int ByteLength => Payload.Length;

    public string DirectionBadge => Direction switch
    {
        SocketMessageDirection.Sent => "⬆ TX",
        SocketMessageDirection.Received => "⬇ RX",
        SocketMessageDirection.System => "⚙ SYS",
        _ => "•"
    };

    public string BadgeColor => Direction switch
    {
        SocketMessageDirection.Sent => "#3B82F6",    // Blue
        SocketMessageDirection.Received => "#10B981",// Emerald
        SocketMessageDirection.System => "#F59E0B",  // Amber
        _ => "#888888"
    };

    public string FormattedTime => Timestamp.ToString("HH:mm:ss.fff");
}

public class SseEventItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string EventType { get; set; } = "message";
    public string EventId { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public int RetryIntervalMs { get; set; }

    public string FormattedTime => Timestamp.ToString("HH:mm:ss.fff");
    public bool HasEventId => !string.IsNullOrEmpty(EventId);
}

public class GraphQlRequest
{
    public string Query { get; set; } = string.Empty;
    public string? OperationName { get; set; }
    public Dictionary<string, object?>? Variables { get; set; }
}

public class GraphQlResponse
{
    public int StatusCode { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public string RawJson { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}
