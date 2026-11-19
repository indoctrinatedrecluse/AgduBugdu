using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AgduBugdu.Plugin.RestClient;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Tools;

public partial class RestClientToolViewModel : Tool
{
    private RestClientViewModel? _model;

    public event EventHandler? ResetSizeRequested;
    public event EventHandler? MinimizeRequested;

    [ObservableProperty]
    private string _method = "GET";

    [ObservableProperty]
    private string _url = "https://httpbin.org/get";

    [ObservableProperty]
    private string _headersText = "User-Agent: AgduBugdu-RestClient/1.0\nAccept: application/json";

    [ObservableProperty]
    private string _bodyText = "{\n  \"message\": \"Hello from AgduBugdu!\"\n}";

    [ObservableProperty]
    private string _responseStatus = "Ready to send requests";

    [ObservableProperty]
    private string _responseHeaders = string.Empty;

    [ObservableProperty]
    private string _responseBody = string.Empty;

    [ObservableProperty]
    private long _elapsedMs;

    [ObservableProperty]
    private int _statusCode;

    [ObservableProperty]
    private bool _isSending;

    [ObservableProperty]
    private bool _allowUntrustedCertificates;

    public ObservableCollection<string> AvailableMethods { get; } = new()
    {
        "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"
    };

    public ObservableCollection<HttpResponseModel> History { get; } = new();

    public RestClientToolViewModel()
    {
        Id = "RestClient";
        Title = "REST Client";
    }

    public void AttachModel(RestClientViewModel model)
    {
        _model = model;
        Method = model.Method;
        Url = model.Url;
        HeadersText = model.HeadersText;
        BodyText = model.BodyText;
        AllowUntrustedCertificates = model.AllowUntrustedCertificates;

        _model.RequestStarted += (s, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsSending = true;
                ResponseStatus = _model.StatusSummary;
            });
        };

        _model.ResponseReceived += (s, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(SyncFromModel);
        };

        SyncFromModel();
    }

    [RelayCommand]
    public async Task SendRequestAsync()
    {
        if (_model == null)
            _model = new RestClientViewModel();

        _model.Method = Method;
        _model.Url = Url;
        _model.HeadersText = HeadersText;
        _model.BodyText = BodyText;
        _model.AllowUntrustedCertificates = AllowUntrustedCertificates;

        IsSending = true;
        ResponseStatus = $"Sending {Method} {Url}...";

        var resp = await _model.SendRequestAsync();
        SyncFromModel();
    }

    [RelayCommand]
    public void LoadFromHistory(HttpResponseModel? item)
    {
        if (item == null) return;
        ResponseBody = item.FormattedBody;
        StatusCode = item.StatusCode;
        ElapsedMs = item.ElapsedMilliseconds;
        ResponseStatus = $"{item.StatusCode} {item.StatusDescription} ({item.ElapsedMilliseconds} ms)";

        var sb = new System.Text.StringBuilder();
        foreach (var h in item.Headers)
        {
            sb.AppendLine($"{h.Key}: {h.Value}");
        }
        ResponseHeaders = sb.ToString().TrimEnd();
    }

    [RelayCommand]
    public void ClearHistory()
    {
        History.Clear();
        _model?.History.Clear();
    }

    private void SyncFromModel()
    {
        if (_model == null) return;

        IsSending = _model.IsSending;
        ResponseStatus = _model.StatusSummary;

        if (_model.LastResponse != null)
        {
            var r = _model.LastResponse;
            StatusCode = r.StatusCode;
            ElapsedMs = r.ElapsedMilliseconds;
            ResponseBody = r.FormattedBody;

            var sb = new System.Text.StringBuilder();
            foreach (var h in r.Headers)
            {
                sb.AppendLine($"{h.Key}: {h.Value}");
            }
            ResponseHeaders = sb.ToString().TrimEnd();
        }

        History.Clear();
        foreach (var h in _model.History)
        {
            History.Add(h);
        }
    }

    [RelayCommand]
    public void ResetSize() => ResetSizeRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    public void Minimize() => MinimizeRequested?.Invoke(this, EventArgs.Empty);
}
