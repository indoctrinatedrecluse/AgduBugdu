using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AgduBugdu.Plugin.RestClient;

public class RestClientViewModel
{
    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public string Title => "REST API Client";
    public string Method { get; set; } = "GET";
    public string Url { get; set; } = "https://httpbin.org/get";
    public string HeadersText { get; set; } = "User-Agent: AgduBugdu-RestClient/1.0\nAccept: application/json";
    public string BodyText { get; set; } = "{\n  \"message\": \"Hello from AgduBugdu!\"\n}";
    public bool IsSending { get; private set; }
    public HttpResponseModel? LastResponse { get; private set; }
    public string StatusSummary { get; private set; } = "Ready to send requests";
    public List<HttpResponseModel> History { get; } = new();

    public event EventHandler? ResponseReceived;
    public event EventHandler? RequestStarted;

    public void SetRequest(HttpRequestModel request)
    {
        Method = request.Method;
        Url = request.Url;
        var sb = new StringBuilder();
        foreach (var h in request.Headers)
        {
            sb.AppendLine($"{h.Key}: {h.Value}");
        }
        HeadersText = sb.ToString().TrimEnd();
        BodyText = request.Body ?? string.Empty;
    }

    public HttpRequestModel BuildCurrentRequest()
    {
        var req = new HttpRequestModel
        {
            Method = Method.ToUpperInvariant(),
            Url = Url.Trim(),
            Body = string.IsNullOrWhiteSpace(BodyText) ? null : BodyText
        };

        if (!string.IsNullOrWhiteSpace(HeadersText))
        {
            using var reader = new System.IO.StringReader(HeadersText);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                int colonIdx = line.IndexOf(':');
                if (colonIdx > 0)
                {
                    req.AddHeader(line[..colonIdx], line[(colonIdx + 1)..]);
                }
            }
        }

        return req;
    }

    public async Task<HttpResponseModel> SendRequestAsync(HttpRequestModel? model = null, CancellationToken ct = default)
    {
        var reqModel = model ?? BuildCurrentRequest();
        IsSending = true;
        StatusSummary = $"Sending {reqModel.Method} {reqModel.Url}...";
        RequestStarted?.Invoke(this, EventArgs.Empty);

        var stopwatch = Stopwatch.StartNew();
        var responseModel = new HttpResponseModel();

        try
        {
            using var msg = new HttpRequestMessage(new HttpMethod(reqModel.Method), reqModel.Url);

            // Add headers
            foreach (var h in reqModel.Headers)
            {
                if (h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    continue;

                msg.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }

            // Body
            if (!string.IsNullOrEmpty(reqModel.Body) &&
                (reqModel.Method == "POST" || reqModel.Method == "PUT" || reqModel.Method == "PATCH" || reqModel.Method == "DELETE"))
            {
                string contentType = reqModel.Headers.TryGetValue("Content-Type", out var ctHeader)
                    ? ctHeader
                    : "application/json";

                msg.Content = new StringContent(reqModel.Body, Encoding.UTF8, contentType);
            }

            using var response = await HttpClient.SendAsync(msg, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
            stopwatch.Stop();

            responseModel.StatusCode = (int)response.StatusCode;
            responseModel.StatusDescription = response.ReasonPhrase ?? response.StatusCode.ToString();
            responseModel.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;

            foreach (var header in response.Headers)
            {
                responseModel.Headers[header.Key] = string.Join(", ", header.Value);
            }
            if (response.Content != null)
            {
                foreach (var header in response.Content.Headers)
                {
                    responseModel.Headers[header.Key] = string.Join(", ", header.Value);
                }
                responseModel.ContentType = response.Content.Headers.ContentType?.ToString() ?? string.Empty;
                responseModel.ContentLength = response.Content.Headers.ContentLength ?? 0;

                string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                responseModel.RawBody = body;
                responseModel.FormattedBody = TryFormatJson(body);
            }

            StatusSummary = $"Completed: {responseModel.StatusCode} {responseModel.StatusDescription} in {responseModel.ElapsedMilliseconds} ms";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            responseModel.StatusCode = 0;
            responseModel.StatusDescription = "Request Failed";
            responseModel.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            responseModel.RawBody = ex.ToString();
            responseModel.FormattedBody = $"Error: {ex.Message}";
            StatusSummary = $"Error: {ex.Message}";
        }
        finally
        {
            IsSending = false;
            LastResponse = responseModel;
            History.Insert(0, responseModel);
            if (History.Count > 50) History.RemoveAt(History.Count - 1);
            ResponseReceived?.Invoke(this, EventArgs.Empty);
        }

        return responseModel;
    }

    private static string TryFormatJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        string trimmed = raw.Trim();
        if (!((trimmed.StartsWith("{") && trimmed.EndsWith("}")) || (trimmed.StartsWith("[") && trimmed.EndsWith("]"))))
            return raw;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return raw;
        }
    }

    public string ToHttpFileFormat()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### {Method} Request");
        sb.AppendLine($"{Method} {Url}");
        if (!string.IsNullOrWhiteSpace(HeadersText))
        {
            sb.AppendLine(HeadersText);
        }
        if (!string.IsNullOrWhiteSpace(BodyText) && (Method == "POST" || Method == "PUT" || Method == "PATCH"))
        {
            sb.AppendLine();
            sb.AppendLine(BodyText);
        }
        return sb.ToString();
    }
}
