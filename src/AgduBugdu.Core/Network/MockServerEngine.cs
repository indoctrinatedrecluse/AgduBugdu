using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgduBugdu.Core.Network;

public class MockRouteRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Mock Route";
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/api/v1/resource";
    public int StatusCode { get; set; } = 200;
    public int LatencyMs { get; set; }
    public string ContentType { get; set; } = "application/json";
    public string ResponseBody { get; set; } = "{\n  \"status\": \"success\"\n}";
    public bool IsEnabled { get; set; } = true;
}

public class MockRequestLogItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public string Query { get; set; } = string.Empty;
    public string RemoteEndpoint { get; set; } = string.Empty;
    public string RequestBody { get; set; } = string.Empty;
    public string MatchedRuleName { get; set; } = "Default / No Match";
    public int ResponseStatusCode { get; set; } = 200;
}

public class MockServerEngine : IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public int Port { get; private set; } = 5244;
    public bool IsRunning => _listener?.IsListening ?? false;
    public List<MockRouteRule> Routes { get; } = new();
    public List<MockRequestLogItem> RequestLogs { get; } = new();

    public event Action<MockRequestLogItem>? RequestReceived;
    public event Action<bool>? ServerStateChanged;

    public MockServerEngine(int port = 5244)
    {
        Port = port;
        InitializeDefaultRoutes();
    }

    private void InitializeDefaultRoutes()
    {
        Routes.Add(new MockRouteRule
        {
            Name = "Get Users List",
            Method = "GET",
            Path = "/api/v1/users",
            StatusCode = 200,
            LatencyMs = 25,
            ContentType = "application/json",
            ResponseBody = "[\n  {\"id\": 1, \"name\": \"Alex Rivera\", \"role\": \"Lead Architect\"},\n  {\"id\": 2, \"name\": \"Elena Rostova\", \"role\": \"Security Engineer\"}\n]"
        });

        Routes.Add(new MockRouteRule
        {
            Name = "Health Check",
            Method = "GET",
            Path = "/health",
            StatusCode = 200,
            LatencyMs = 5,
            ContentType = "application/json",
            ResponseBody = "{\n  \"status\": \"healthy\",\n  \"service\": \"AgduBugdu Mock API Gateway\"\n}"
        });

        Routes.Add(new MockRouteRule
        {
            Name = "Auth Login",
            Method = "POST",
            Path = "/api/v1/auth/login",
            StatusCode = 201,
            LatencyMs = 60,
            ContentType = "application/json",
            ResponseBody = "{\n  \"token\": \"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e30.mock_signature\",\n  \"expiresIn\": 3600\n}"
        });
    }

    public bool Start(int? customPort = null)
    {
        if (IsRunning) return true;

        if (customPort.HasValue && customPort.Value > 0)
        {
            Port = customPort.Value;
        }

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();

            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));

            ServerStateChanged?.Invoke(true);
            return true;
        }
        catch
        {
            Stop();
            ServerStateChanged?.Invoke(false);
            return false;
        }
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
        }
        catch { }
        finally
        {
            _listener = null;
            _cts?.Dispose();
            _cts = null;
            ServerStateChanged?.Invoke(false);
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context), ct);
            }
            catch when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                if (_listener == null || !_listener.IsListening) break;
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        string path = request.Url?.AbsolutePath ?? "/";
        string method = request.HttpMethod;
        string query = request.Url?.Query ?? string.Empty;
        string remoteEndpoint = request.RemoteEndPoint.ToString();

        string requestBody = string.Empty;
        if (request.HasEntityBody)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            requestBody = await reader.ReadToEndAsync();
        }

        // Match route
        MockRouteRule? matchedRule = null;
        foreach (var r in Routes)
        {
            if (r.IsEnabled &&
                string.Equals(r.Method, method, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.Path.TrimEnd('/'), path.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                matchedRule = r;
                break;
            }
        }

        int statusCode = 200;
        string responseText;
        string contentType = "application/json";

        if (matchedRule != null)
        {
            statusCode = matchedRule.StatusCode;
            responseText = matchedRule.ResponseBody;
            contentType = matchedRule.ContentType;

            if (matchedRule.LatencyMs > 0)
            {
                await Task.Delay(matchedRule.LatencyMs);
            }
        }
        else
        {
            statusCode = 404;
            responseText = $"{{\n  \"error\": \"No mock route matching {method} {path}\"\n}}";
        }

        // Log request
        var log = new MockRequestLogItem
        {
            Timestamp = DateTime.Now,
            Method = method,
            Path = path,
            Query = query,
            RemoteEndpoint = remoteEndpoint,
            RequestBody = requestBody,
            MatchedRuleName = matchedRule?.Name ?? "Unmatched 404",
            ResponseStatusCode = statusCode
        };

        RequestLogs.Insert(0, log);
        if (RequestLogs.Count > 100)
        {
            RequestLogs.RemoveAt(RequestLogs.Count - 1);
        }

        RequestReceived?.Invoke(log);

        // Send response
        try
        {
            response.StatusCode = statusCode;
            response.ContentType = contentType;
            response.AddHeader("X-Powered-By", "AgduBugdu Mock Server Engine");

            byte[] buffer = Encoding.UTF8.GetBytes(responseText);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer);
            response.OutputStream.Close();
        }
        catch
        {
            // Transient network disconnect
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
