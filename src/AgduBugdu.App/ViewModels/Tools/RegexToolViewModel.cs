using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using AgduBugdu.Core.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Tools;

public partial class RegexToolViewModel : Tool
{
    [ObservableProperty]
    private int _selectedTabIndex; // 0 = RegEx Playground, 1 = Text Utilities

    // --- RegEx Playground ---
    [ObservableProperty]
    private string _pattern = @"([a-zA-Z0-9._%+-]+)@([a-zA-Z0-9.-]+\.[a-zA-Z]{2,})";

    [ObservableProperty]
    private string _testInput = "Send inquiries to dev@agdubugdu.com or support@company.org anytime!";

    [ObservableProperty]
    private string _replacementPattern = "[$1 AT $2]";

    [ObservableProperty]
    private string _replacementOutput = string.Empty;

    [ObservableProperty]
    private bool _flagIgnoreCase = true;

    [ObservableProperty]
    private bool _flagMultiline = true;

    [ObservableProperty]
    private bool _flagSingleline;

    [ObservableProperty]
    private bool _flagIgnoreWhitespace;

    [ObservableProperty]
    private string _matchStatus = "Ready";

    [ObservableProperty]
    private bool _isPatternValid = true;

    public ObservableCollection<RegexMatchItem> Matches { get; } = new();
    public ObservableCollection<RegexPreset> Presets { get; } = new();

    [ObservableProperty]
    private RegexPreset? _selectedPreset;

    // --- Text Utilities ---
    [ObservableProperty]
    private string _utilityInput = "Welcome to AgduBugdu Text Utility Lab!";

    [ObservableProperty]
    private string _utilityOutput = string.Empty;

    [ObservableProperty]
    private HashBundle _hashes = new();

    [ObservableProperty]
    private JwtTokenInfo? _jwtInfo;

    [ObservableProperty]
    private string _activeUtilityTitle = "Text Utilities";

    public RegexToolViewModel()
    {
        Id = "RegexTool";
        Title = "RegEx & Text Lab";

        foreach (var p in RegexPlaygroundEngine.Presets)
        {
            Presets.Add(p);
        }

        RunRegexAnalysis();
        ComputeHashes();
    }

    partial void OnPatternChanged(string value) => RunRegexAnalysis();
    partial void OnTestInputChanged(string value) => RunRegexAnalysis();
    partial void OnReplacementPatternChanged(string value) => RunRegexAnalysis();
    partial void OnFlagIgnoreCaseChanged(bool value) => RunRegexAnalysis();
    partial void OnFlagMultilineChanged(bool value) => RunRegexAnalysis();
    partial void OnFlagSinglelineChanged(bool value) => RunRegexAnalysis();
    partial void OnFlagIgnoreWhitespaceChanged(bool value) => RunRegexAnalysis();
    partial void OnSelectedPresetChanged(RegexPreset? value)
    {
        if (value != null)
        {
            LoadPreset(value);
        }
    }

    public void RunRegexAnalysis()
    {
        var options = RegexOptions.None;
        if (FlagIgnoreCase) options |= RegexOptions.IgnoreCase;
        if (FlagMultiline) options |= RegexOptions.Multiline;
        if (FlagSingleline) options |= RegexOptions.Singleline;
        if (FlagIgnoreWhitespace) options |= RegexOptions.IgnorePatternWhitespace;

        var result = RegexPlaygroundEngine.Analyze(Pattern, TestInput, options, ReplacementPattern);

        Matches.Clear();
        IsPatternValid = result.IsValid;

        if (result.IsValid)
        {
            foreach (var m in result.Matches)
            {
                Matches.Add(m);
            }
            ReplacementOutput = result.ReplacementOutput;
            MatchStatus = $"{result.MatchCount} match{(result.MatchCount == 1 ? "" : "es")} ({result.ElapsedMilliseconds} ms)";
        }
        else
        {
            ReplacementOutput = string.Empty;
            MatchStatus = $"Error: {result.ErrorMessage}";
        }
    }

    [RelayCommand]
    public void LoadPreset(RegexPreset? preset)
    {
        if (preset == null) return;
        Pattern = preset.Pattern;
        TestInput = preset.SampleText;
        ReplacementPattern = string.Empty;
    }

    [RelayCommand]
    public void ClearRegex()
    {
        Pattern = string.Empty;
        TestInput = string.Empty;
        ReplacementPattern = string.Empty;
        ReplacementOutput = string.Empty;
        Matches.Clear();
        MatchStatus = "Ready";
    }

    // --- Text Utilities Actions ---
    [RelayCommand]
    public void Base64Encode()
    {
        ActiveUtilityTitle = "Base64 Encoded Output";
        UtilityOutput = TextUtilitiesEngine.Base64Encode(UtilityInput);
    }

    [RelayCommand]
    public void Base64Decode()
    {
        ActiveUtilityTitle = "Base64 Decoded Output";
        try
        {
            UtilityOutput = TextUtilitiesEngine.Base64Decode(UtilityInput);
        }
        catch (Exception ex)
        {
            UtilityOutput = $"Error decoding Base64: {ex.Message}";
        }
    }

    [RelayCommand]
    public void UrlEncode()
    {
        ActiveUtilityTitle = "URL Encoded Output";
        UtilityOutput = TextUtilitiesEngine.UrlEncode(UtilityInput);
    }

    [RelayCommand]
    public void UrlDecode()
    {
        ActiveUtilityTitle = "URL Decoded Output";
        UtilityOutput = TextUtilitiesEngine.UrlDecode(UtilityInput);
    }

    [RelayCommand]
    public void DecodeJwt()
    {
        ActiveUtilityTitle = "Decoded JWT Claims & Signature";
        JwtInfo = TextUtilitiesEngine.DecodeJwt(UtilityInput);
        if (JwtInfo.IsValid)
        {
            UtilityOutput = $"--- HEADER ({JwtInfo.Algorithm}) ---\n{JwtInfo.HeaderJson}\n\n--- PAYLOAD (Expires: {JwtInfo.Expiration}) ---\n{JwtInfo.PayloadJson}";
        }
        else
        {
            UtilityOutput = $"JWT Parse Failed: {JwtInfo.ErrorMessage}";
        }
    }

    [RelayCommand]
    public void ComputeHashes()
    {
        ActiveUtilityTitle = "Cryptographic Hashes";
        Hashes = TextUtilitiesEngine.ComputeHashes(UtilityInput);
        UtilityOutput = $"MD5:    {Hashes.Md5}\nSHA1:   {Hashes.Sha1}\nSHA256: {Hashes.Sha256}\nSHA512: {Hashes.Sha512}";
    }

    [RelayCommand]
    public void FormatJson()
    {
        ActiveUtilityTitle = "Prettified JSON";
        try
        {
            UtilityOutput = TextUtilitiesEngine.FormatJson(UtilityInput);
        }
        catch (Exception ex)
        {
            UtilityOutput = $"Invalid JSON: {ex.Message}";
        }
    }

    [RelayCommand]
    public void MinifyJson()
    {
        ActiveUtilityTitle = "Minified JSON";
        try
        {
            UtilityOutput = TextUtilitiesEngine.MinifyJson(UtilityInput);
        }
        catch (Exception ex)
        {
            UtilityOutput = $"Invalid JSON: {ex.Message}";
        }
    }
}
