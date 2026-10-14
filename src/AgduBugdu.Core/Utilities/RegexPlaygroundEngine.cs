using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AgduBugdu.Core.Utilities;

public class RegexGroupItem
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int Length => Value.Length;
}

public class RegexMatchItem
{
    public int Index { get; set; }
    public int Length { get; set; }
    public string Value { get; set; } = string.Empty;
    public List<RegexGroupItem> Groups { get; } = new();
}

public class RegexAnalysisResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public int MatchCount => Matches.Count;
    public long ElapsedMilliseconds { get; set; }
    public List<RegexMatchItem> Matches { get; } = new();
    public string ReplacementOutput { get; set; } = string.Empty;
}

public class RegexPreset
{
    public string Name { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string SampleText { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public static class RegexPlaygroundEngine
{
    public static readonly List<RegexPreset> Presets = new()
    {
        new RegexPreset
        {
            Name = "Email Address",
            Pattern = @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
            SampleText = "Contact us at support@agdubugdu.com or sales@enterprise.io for details.",
            Description = "Standard RFC compliant email pattern"
        },
        new RegexPreset
        {
            Name = "IPv4 Address",
            Pattern = @"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b",
            SampleText = "Connected to 192.168.1.1 and gateway 10.0.0.254 (dns: 8.8.8.8)",
            Description = "IPv4 addresses with 0-255 octet bounds"
        },
        new RegexPreset
        {
            Name = "URL / Web Address",
            Pattern = @"https?://[a-zA-Z0-9\-\.]+(?::[0-9]+)?(?:/[a-zA-Z0-9\-_./?%&=]*)?",
            SampleText = "Visit https://github.com/indoctrinatedrecluse/AgduBugdu or http://localhost:8080/api/v1/health",
            Description = "HTTP/HTTPS URLs with optional port and path"
        },
        new RegexPreset
        {
            Name = "UUID / GUID",
            Pattern = @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            SampleText = "Session IDs: 4f3b89a2-9c10-4821-b3f5-7c281e0984da and 12345678-1234-1234-1234-123456789abc",
            Description = "Standard 8-4-4-4-12 hex UUID representation"
        },
        new RegexPreset
        {
            Name = "ISO 8601 Date",
            Pattern = @"\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?)?",
            SampleText = "Event logs: 2026-10-14T17:30:00Z and created at 2026-12-31",
            Description = "ISO timestamps with optional time and timezone"
        },
        new RegexPreset
        {
            Name = "Semantic Version",
            Pattern = @"v?(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)(?:-[0-9a-zA-Z.-]+)?(?:\+[0-9a-zA-Z.-]+)?",
            SampleText = "Releases: v1.4.0, v1.5.0-beta.1, and 2.0.1+build.42",
            Description = "SemVer 2.0 spec with prerelease and build metadata"
        },
        new RegexPreset
        {
            Name = "Hex Color",
            Pattern = @"#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})\b",
            SampleText = "Theme palette: #1E1E1E, #3B82F6, #10B981, and #FFF",
            Description = "Hexadecimal color codes with 3, 6, or 8 characters"
        }
    };

    public static RegexAnalysisResult Analyze(string pattern, string input, RegexOptions options, string? replacement = null)
    {
        var result = new RegexAnalysisResult();
        if (string.IsNullOrEmpty(pattern))
        {
            result.IsValid = true;
            return result;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            var regex = new Regex(pattern, options, TimeSpan.FromSeconds(2));
            var matches = regex.Matches(input ?? string.Empty);

            foreach (Match m in matches)
            {
                var matchItem = new RegexMatchItem
                {
                    Index = m.Index,
                    Length = m.Length,
                    Value = m.Value
                };

                for (int i = 0; i < m.Groups.Count; i++)
                {
                    var g = m.Groups[i];
                    string name = regex.GroupNameFromNumber(i);
                    matchItem.Groups.Add(new RegexGroupItem
                    {
                        Index = g.Index,
                        Name = name,
                        Value = g.Value
                    });
                }

                result.Matches.Add(matchItem);
            }

            if (replacement != null)
            {
                result.ReplacementOutput = regex.Replace(input ?? string.Empty, replacement);
            }

            result.IsValid = true;
        }
        catch (RegexParseException ex)
        {
            result.IsValid = false;
            result.ErrorMessage = ex.Message;
        }
        catch (RegexMatchTimeoutException)
        {
            result.IsValid = false;
            result.ErrorMessage = "Regular expression evaluation timed out (catastrophic backtracking guard).";
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            sw.Stop();
            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
        }

        return result;
    }
}
