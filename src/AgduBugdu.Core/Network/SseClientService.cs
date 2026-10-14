using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgduBugdu.Core.Network;

public class SseClientService : IDisposable
{
    private static readonly HttpClient HttpClient = new();
    private CancellationTokenSource? _cts;

    public bool IsConnected { get; private set; }

    public event Action<SseEventItem>? EventReceived;
    public event Action<bool, string?>? ConnectionStateChanged;

    public async Task ConnectAsync(string url, IDictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        Disconnect();

        _cts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

        if (headers != null)
        {
            foreach (var kvp in headers)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                {
                    request.Headers.TryAddWithoutValidation(kvp.Key.Trim(), kvp.Value.Trim());
                }
            }
        }

        try
        {
            var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
            response.EnsureSuccessStatusCode();

            IsConnected = true;
            ConnectionStateChanged?.Invoke(true, $"Streaming from {url} ({response.StatusCode})");

            _ = Task.Run(async () =>
            {
                try
                {
                    using var stream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
                    using var reader = new StreamReader(stream, Encoding.UTF8);

                    string currentEvent = "message";
                    string currentId = string.Empty;
                    var dataBuffer = new StringBuilder();

                    while (!linkedCts.Token.IsCancellationRequested && !reader.EndOfStream)
                    {
                        string? line = await reader.ReadLineAsync(linkedCts.Token);
                        if (line == null) break;

                        if (string.IsNullOrWhiteSpace(line))
                        {
                            // Empty line marks end of event
                            if (dataBuffer.Length > 0 || currentEvent != "message" || !string.IsNullOrEmpty(currentId))
                            {
                                var item = new SseEventItem
                                {
                                    EventType = currentEvent,
                                    EventId = currentId,
                                    Data = dataBuffer.ToString().TrimEnd('\n', '\r'),
                                    Timestamp = DateTime.Now
                                };
                                EventReceived?.Invoke(item);
                            }

                            currentEvent = "message";
                            currentId = string.Empty;
                            dataBuffer.Clear();
                            continue;
                        }

                        if (line.StartsWith(':'))
                        {
                            // SSE comment line - ignore
                            continue;
                        }

                        if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                        {
                            currentEvent = line.Substring(6).Trim();
                        }
                        else if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            string dataPart = line.Substring(5).TrimStart(' ');
                            if (dataBuffer.Length > 0) dataBuffer.Append('\n');
                            dataBuffer.Append(dataPart);
                        }
                        else if (line.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
                        {
                            currentId = line.Substring(3).Trim();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Cancelled
                }
                catch (Exception ex)
                {
                    ConnectionStateChanged?.Invoke(false, $"SSE stream error: {ex.Message}");
                }
                finally
                {
                    IsConnected = false;
                    ConnectionStateChanged?.Invoke(false, "SSE stream terminated");
                }
            }, linkedCts.Token);
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectionStateChanged?.Invoke(false, $"Failed to connect: {ex.Message}");
            throw;
        }
    }

    public static List<SseEventItem> ParseSseChunk(string rawChunk)
    {
        var results = new List<SseEventItem>();
        if (string.IsNullOrWhiteSpace(rawChunk))
            return results;

        using var reader = new StringReader(rawChunk);
        string currentEvent = "message";
        string currentId = string.Empty;
        var dataBuffer = new StringBuilder();

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (dataBuffer.Length > 0 || currentEvent != "message" || !string.IsNullOrEmpty(currentId))
                {
                    results.Add(new SseEventItem
                    {
                        EventType = currentEvent,
                        EventId = currentId,
                        Data = dataBuffer.ToString().TrimEnd('\n', '\r'),
                        Timestamp = DateTime.Now
                    });
                }
                currentEvent = "message";
                currentId = string.Empty;
                dataBuffer.Clear();
                continue;
            }

            if (line.StartsWith(':')) continue;

            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                currentEvent = line.Substring(6).Trim();
            }
            else if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                string dataPart = line.Substring(5).TrimStart(' ');
                if (dataBuffer.Length > 0) dataBuffer.Append('\n');
                dataBuffer.Append(dataPart);
            }
            else if (line.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
            {
                currentId = line.Substring(3).Trim();
            }
        }

        if (dataBuffer.Length > 0)
        {
            results.Add(new SseEventItem
            {
                EventType = currentEvent,
                EventId = currentId,
                Data = dataBuffer.ToString().TrimEnd('\n', '\r'),
                Timestamp = DateTime.Now
            });
        }

        return results;
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        if (IsConnected)
        {
            IsConnected = false;
            ConnectionStateChanged?.Invoke(false, "Disconnected");
        }
    }

    public void Dispose()
    {
        Disconnect();
    }
}
