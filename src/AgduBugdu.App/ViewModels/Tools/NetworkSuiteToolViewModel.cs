using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AgduBugdu.Core.Network;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Tools;

public partial class NetworkSuiteToolViewModel : Tool
{
    private readonly WebSocketClientService _wsService = new();
    private readonly SseClientService _sseService = new();
    private readonly GraphQlClientService _graphQlService = new();

    [ObservableProperty]
    private int _selectedTabIndex; // 0 = WebSocket, 1 = SSE, 2 = GraphQL

    // --- WebSockets ---
    [ObservableProperty]
    private string _wsUrl = "wss://echo.websocket.org";

    [ObservableProperty]
    private bool _isWsConnected;

    [ObservableProperty]
    private string _wsStatusMessage = "Disconnected";

    [ObservableProperty]
    private string _wsMessageInput = "{\"event\": \"ping\", \"client\": \"AgduBugdu\"}";

    public ObservableCollection<SocketMessageItem> WsMessages { get; } = new();

    // --- SSE ---
    [ObservableProperty]
    private string _sseUrl = "https://httpbin.org/stream/5";

    [ObservableProperty]
    private bool _isSseConnected;

    [ObservableProperty]
    private string _sseStatusMessage = "Disconnected";

    public ObservableCollection<SseEventItem> SseEvents { get; } = new();

    // --- GraphQL ---
    [ObservableProperty]
    private string _graphQlEndpoint = "https://countries.trevorblades.com/";

    [ObservableProperty]
    private string _graphQlQuery = @"query GetCountries {
  countries {
    code
    name
    capital
    emoji
  }
}";

    [ObservableProperty]
    private string _graphQlVariables = "{\n  \"limit\": 10\n}";

    [ObservableProperty]
    private string _graphQlResponseJson = string.Empty;

    [ObservableProperty]
    private string _graphQlStatus = "Ready";

    [ObservableProperty]
    private bool _isGraphQlLoading;

    public NetworkSuiteToolViewModel()
    {
        Id = "NetworkSuite";
        Title = "Network & Sockets";

        _wsService.MessageReceived += msg =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                WsMessages.Insert(0, msg);
            });
        };

        _wsService.ConnectionStateChanged += (connected, status) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsWsConnected = connected;
                WsStatusMessage = status ?? (connected ? "Connected" : "Disconnected");
            });
        };

        _sseService.EventReceived += ev =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                SseEvents.Insert(0, ev);
            });
        };

        _sseService.ConnectionStateChanged += (connected, status) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsSseConnected = connected;
                SseStatusMessage = status ?? (connected ? "Connected" : "Disconnected");
            });
        };
    }

    // --- WebSocket Commands ---
    [RelayCommand]
    public async Task ConnectWsAsync()
    {
        if (string.IsNullOrWhiteSpace(WsUrl)) return;
        try
        {
            WsStatusMessage = "Connecting...";
            await _wsService.ConnectAsync(WsUrl);
        }
        catch (Exception ex)
        {
            WsStatusMessage = $"Connection failed: {ex.Message}";
            IsWsConnected = false;
        }
    }

    [RelayCommand]
    public async Task DisconnectWsAsync()
    {
        await _wsService.DisconnectAsync();
    }

    [RelayCommand]
    public async Task SendWsMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(WsMessageInput) || !IsWsConnected) return;

        try
        {
            await _wsService.SendTextAsync(WsMessageInput);
        }
        catch (Exception ex)
        {
            WsStatusMessage = $"Send failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ClearWsMessages()
    {
        WsMessages.Clear();
    }

    // --- SSE Commands ---
    [RelayCommand]
    public async Task ConnectSseAsync()
    {
        if (string.IsNullOrWhiteSpace(SseUrl)) return;
        try
        {
            SseStatusMessage = "Connecting to SSE stream...";
            await _sseService.ConnectAsync(SseUrl);
        }
        catch (Exception ex)
        {
            SseStatusMessage = $"SSE connection failed: {ex.Message}";
            IsSseConnected = false;
        }
    }

    [RelayCommand]
    public void DisconnectSse()
    {
        _sseService.Disconnect();
    }

    [RelayCommand]
    public void ClearSseEvents()
    {
        SseEvents.Clear();
    }

    // --- GraphQL Commands ---
    [RelayCommand]
    public async Task ExecuteGraphQlAsync()
    {
        if (string.IsNullOrWhiteSpace(GraphQlEndpoint) || string.IsNullOrWhiteSpace(GraphQlQuery))
            return;

        IsGraphQlLoading = true;
        GraphQlStatus = "Executing query...";

        try
        {
            var res = await _graphQlService.ExecuteAsync(GraphQlEndpoint, GraphQlQuery, GraphQlVariables);
            IsGraphQlLoading = false;
            GraphQlResponseJson = res.RawJson;

            if (res.IsSuccess)
            {
                GraphQlStatus = $"{res.StatusCode} OK ({res.ElapsedMilliseconds} ms)";
            }
            else
            {
                GraphQlStatus = res.StatusCode > 0
                    ? $"Error {res.StatusCode} ({res.ElapsedMilliseconds} ms)"
                    : $"Error: {res.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            IsGraphQlLoading = false;
            GraphQlStatus = $"Failed: {ex.Message}";
            GraphQlResponseJson = ex.ToString();
        }
    }

    [RelayCommand]
    public void LoadIntrospection()
    {
        GraphQlQuery = GraphQlClientService.GetIntrospectionQuery();
    }

    [RelayCommand]
    public void ClearGraphQl()
    {
        GraphQlResponseJson = string.Empty;
        GraphQlStatus = "Ready";
    }
}
