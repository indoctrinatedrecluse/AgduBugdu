using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgduBugdu.Plugin.RestClient;

public class HttpRequestModel
{
    public string Name { get; set; } = "Request";
    public string Method { get; set; } = "GET";
    public string Url { get; set; } = "https://httpbin.org/get";
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Body { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public void AddHeader(string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Headers[name.Trim()] = value.Trim();
        }
    }
}

public class HttpResponseModel
{
    public int StatusCode { get; set; }
    public string StatusDescription { get; set; } = string.Empty;
    public bool IsSuccess => StatusCode >= 200 && StatusCode <= 299;
    public long ElapsedMilliseconds { get; set; }
    public long ContentLength { get; set; }
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string RawBody { get; set; } = string.Empty;
    public string FormattedBody { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StatusBadge => $"{StatusCode} {StatusDescription} ({ElapsedMilliseconds} ms)";
}

public static class HttpFileParser
{
    private static readonly string[] SupportedMethods =
    {
        "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"
    };

    public static List<HttpRequestModel> ParseHttpFile(string content)
    {
        var requests = new List<HttpRequestModel>();
        if (string.IsNullOrWhiteSpace(content))
            return requests;

        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Pre-scan variables e.g. @baseUrl = https://api.example.com
        using var preReader = new StringReader(content);
        string? preLine;
        while ((preLine = preReader.ReadLine()) != null)
        {
            string trimmed = preLine.Trim();
            if (trimmed.StartsWith("@") && trimmed.Contains('='))
            {
                int eqIdx = trimmed.IndexOf('=');
                string varName = trimmed[1..eqIdx].Trim();
                string varVal = trimmed[(eqIdx + 1)..].Trim();
                variables[varName] = varVal;
            }
        }

        // Split by ### delimiter
        var blocks = Regex.Split(content, @"(?m)^###.*$");

        foreach (var rawBlock in blocks)
        {
            string block = rawBlock.Trim();
            if (string.IsNullOrWhiteSpace(block))
                continue;

            // Apply variable substitutions {{varName}}
            foreach (var kv in variables)
            {
                block = block.Replace("{{" + kv.Key + "}}", kv.Value, StringComparison.OrdinalIgnoreCase);
            }

            var req = ParseSingleRequest(block);
            if (req != null)
            {
                requests.Add(req);
            }
        }

        return requests;
    }

    private static HttpRequestModel? ParseSingleRequest(string block)
    {
        using var reader = new StringReader(block);
        string? line;
        string? method = null;
        string? url = null;
        string reqName = "Request";
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bodySb = new StringBuilder();
        bool readingBody = false;

        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();

            // Comment lines
            if (!readingBody && (trimmed.StartsWith("#") || trimmed.StartsWith("//")))
            {
                if (trimmed.StartsWith("# @name", StringComparison.OrdinalIgnoreCase))
                {
                    reqName = trimmed["# @name".Length..].Trim();
                }
                continue;
            }

            if (!readingBody && trimmed.StartsWith("@") && trimmed.Contains('='))
            {
                continue; // variable declaration handled in pre-scan
            }

            // Find request line e.g. GET https://...
            if (method == null && !readingBody)
            {
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                var parts = trimmed.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && SupportedMethods.Contains(parts[0].ToUpperInvariant()))
                {
                    method = parts[0].ToUpperInvariant();
                    url = parts[1];
                    continue;
                }
            }

            // Empty line separates headers from body
            if (!readingBody && string.IsNullOrWhiteSpace(trimmed))
            {
                if (method != null)
                {
                    readingBody = true;
                }
                continue;
            }

            if (!readingBody)
            {
                int colonIdx = trimmed.IndexOf(':');
                if (colonIdx > 0)
                {
                    string hKey = trimmed[..colonIdx].Trim();
                    string hVal = trimmed[(colonIdx + 1)..].Trim();
                    headers[hKey] = hVal;
                }
            }
            else
            {
                bodySb.AppendLine(line);
            }
        }

        if (method == null || url == null)
            return null;

        var request = new HttpRequestModel
        {
            Name = reqName,
            Method = method,
            Url = url,
            Body = bodySb.Length > 0 ? bodySb.ToString().TrimEnd() : null
        };

        foreach (var h in headers)
        {
            request.AddHeader(h.Key, h.Value);
        }

        return request;
    }
}
