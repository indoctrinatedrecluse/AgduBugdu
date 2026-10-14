using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AgduBugdu.Core.Network;

public class GraphQlClientService
{
    private static readonly HttpClient HttpClient = new();

    public async Task<GraphQlResponse> ExecuteAsync(
        string endpoint,
        string query,
        string? variablesJson = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["query"] = query
            };

            if (!string.IsNullOrWhiteSpace(variablesJson))
            {
                try
                {
                    var vars = JsonSerializer.Deserialize<Dictionary<string, object?>>(variablesJson);
                    payload["variables"] = vars;
                }
                catch
                {
                    // If raw string, pass as is
                }
            }

            string requestJson = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

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

            var response = await HttpClient.SendAsync(request, cancellationToken);
            sw.Stop();

            string rawContent = await response.Content.ReadAsStringAsync(cancellationToken);

            string prettyJson = rawContent;
            try
            {
                using var doc = JsonDocument.Parse(rawContent);
                prettyJson = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                // keep raw if not json
            }

            return new GraphQlResponse
            {
                StatusCode = (int)response.StatusCode,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                RawJson = prettyJson,
                IsSuccess = response.IsSuccessStatusCode
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new GraphQlResponse
            {
                StatusCode = 0,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                RawJson = string.Empty,
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public static string GetIntrospectionQuery()
    {
        return @"query IntrospectionQuery {
  __schema {
    queryType { name }
    mutationType { name }
    types {
      name
      kind
      description
      fields {
        name
        type { name kind }
      }
    }
  }
}";
    }
}
