using System.Text.RegularExpressions;
using AgduBugdu.Core.Utilities;
using Xunit;

namespace AgduBugdu.Tests;

public class RegexAndTextUtilityTests
{
    [Fact]
    public void RegexPlayground_ValidPattern_ExtractsMatchesAndGroups()
    {
        string pattern = @"(\w+):(\d+)";
        string input = "host:8080 and port:3000";

        var result = RegexPlaygroundEngine.Analyze(pattern, input, RegexOptions.None, "[$1->$2]");

        Assert.True(result.IsValid);
        Assert.Equal(2, result.MatchCount);
        Assert.Equal("host:8080", result.Matches[0].Value);
        Assert.Equal(3, result.Matches[0].Groups.Count);
        Assert.Equal("host", result.Matches[0].Groups[1].Value);
        Assert.Equal("8080", result.Matches[0].Groups[2].Value);
        Assert.Equal("[host->8080] and [port->3000]", result.ReplacementOutput);
    }

    [Fact]
    public void RegexPlayground_InvalidPattern_ReturnsErrorWithoutThrowing()
    {
        string pattern = @"[unclosed-bracket";
        string input = "test input";

        var result = RegexPlaygroundEngine.Analyze(pattern, input, RegexOptions.None);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void RegexPlayground_Presets_AreValidAndMatchesSample()
    {
        foreach (var preset in RegexPlaygroundEngine.Presets)
        {
            var result = RegexPlaygroundEngine.Analyze(preset.Pattern, preset.SampleText, RegexOptions.None);
            Assert.True(result.IsValid, $"Preset {preset.Name} failed to compile");
            Assert.True(result.MatchCount > 0, $"Preset {preset.Name} matched 0 items on its sample text");
        }
    }

    [Fact]
    public void TextUtilities_Base64Roundtrip_MatchesOriginal()
    {
        string original = "Hello, AgduBugdu World! 🚀";
        string encoded = TextUtilitiesEngine.Base64Encode(original);
        string decoded = TextUtilitiesEngine.Base64Decode(encoded);

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void TextUtilities_UrlEncodeRoundtrip_MatchesOriginal()
    {
        string original = "https://example.com/search?q=c# & avalonia=100%";
        string encoded = TextUtilitiesEngine.UrlEncode(original);
        string decoded = TextUtilitiesEngine.UrlDecode(encoded);

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void TextUtilities_DecodeJwt_ParsesClaimsAndHeader()
    {
        // Standard test JWT: {"alg":"HS256","typ":"JWT"}.{"sub":"user123","name":"Developer","exp":1893456000}.signature
        string testJwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ1c2VyMTIzIiwibmFtZSI6IkRldmVsb3BlciIsImV4cCI6MTg5MzQ1NjAwMH0.dummy_sig";

        var info = TextUtilitiesEngine.DecodeJwt(testJwt);

        Assert.True(info.IsValid);
        Assert.Equal("HS256", info.Algorithm);
        Assert.Contains("Developer", info.PayloadJson);
        Assert.NotNull(info.Expiration);
        Assert.False(info.IsExpired); // 1893456000 is year 2030
    }

    [Fact]
    public void TextUtilities_ComputeHashes_ReturnsValidKnownHashes()
    {
        // MD5 of "hello" = 5d41402abc4b2a76b9719d911017c592
        // SHA256 of "hello" = 2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824
        var hashes = TextUtilitiesEngine.ComputeHashes("hello");

        Assert.Equal("5d41402abc4b2a76b9719d911017c592", hashes.Md5);
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", hashes.Sha256);
        Assert.NotEmpty(hashes.Sha1);
        Assert.NotEmpty(hashes.Sha512);
    }

    [Fact]
    public void TextUtilities_FormatAndMinifyJson_WorksCorrectly()
    {
        string raw = "{\"a\": 1, \"b\": [2, 3]}";
        string formatted = TextUtilitiesEngine.FormatJson(raw);
        Assert.Contains("\n", formatted);

        string minified = TextUtilitiesEngine.MinifyJson(formatted);
        Assert.Equal("{\"a\":1,\"b\":[2,3]}", minified);
    }
}
