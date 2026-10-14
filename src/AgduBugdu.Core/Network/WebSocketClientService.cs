using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgduBugdu.Core.Network;

public class WebSocketClientService : IDisposable
{
    private ClientWebSocket? _client;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoopTask;

    public bool IsConnected => _client?.State == WebSocketState.Open;

    public event Action<SocketMessageItem>? MessageReceived;
    public event Action<bool, string?>? ConnectionStateChanged;

    public async Task ConnectAsync(string url, IDictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();

        _client = new ClientWebSocket();
        if (headers != null)
        {
            foreach (var kvp in headers)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                {
                    _client.Options.SetRequestHeader(kvp.Key.Trim(), kvp.Value.Trim());
                }
            }
        }

        _cts = new CancellationTokenSource();
        var uri = new Uri(url);

        await _client.ConnectAsync(uri, cancellationToken);
        ConnectionStateChanged?.Invoke(true, $"Connected to {url}");

        _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_client, _cts.Token));
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        if (_client != null)
        {
            try
            {
                if (_client.State == WebSocketState.Open || _client.State == WebSocketState.CloseReceived)
                {
                    await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
                }
            }
            catch
            {
                // Ignore socket closing errors
            }
            finally
            {
                _client.Dispose();
                _client = null;
            }
        }

        ConnectionStateChanged?.Invoke(false, "Disconnected");
    }

    public async Task SendTextAsync(string message, CancellationToken cancellationToken = default)
    {
        if (_client == null || _client.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket is not connected.");

        byte[] bytes = Encoding.UTF8.GetBytes(message);
        await _client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);

        MessageReceived?.Invoke(new SocketMessageItem
        {
            Direction = SocketMessageDirection.Sent,
            Payload = message,
            Timestamp = DateTime.Now
        });
    }

    private async Task ReceiveLoopAsync(ClientWebSocket client, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();

        try
        {
            while (!ct.IsCancellationRequested && client.State == WebSocketState.Open)
            {
                sb.Clear();
                WebSocketReceiveResult result;

                do
                {
                    result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await DisconnectAsync();
                        return;
                    }

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                string messageText = sb.ToString();
                MessageReceived?.Invoke(new SocketMessageItem
                {
                    Direction = SocketMessageDirection.Received,
                    Payload = messageText,
                    Timestamp = DateTime.Now
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancel
        }
        catch (Exception ex)
        {
            ConnectionStateChanged?.Invoke(false, $"Connection error: {ex.Message}");
        }
        finally
        {
            ConnectionStateChanged?.Invoke(false, "Connection closed");
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _client?.Dispose();
        _cts?.Dispose();
    }
}
