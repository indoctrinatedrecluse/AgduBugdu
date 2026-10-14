using System;
using System.Collections.ObjectModel;
using AgduBugdu.Core.Network;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Tools;

public partial class MockServerToolViewModel : Tool
{
    private readonly MockServerEngine _engine;

    [ObservableProperty]
    private int _port = 5244;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleButtonText))]
    [NotifyPropertyChangedFor(nameof(ToggleButtonColor))]
    [NotifyPropertyChangedFor(nameof(ServerStatusColor))]
    private bool _isRunning;

    public string ToggleButtonText => IsRunning ? "Stop Server" : "Start Server";
    public string ToggleButtonColor => IsRunning ? "#EF4444" : "#10B981";
    public string ServerStatusColor => IsRunning ? "#10B981" : "#AAAAAA";

    [ObservableProperty]
    private string _statusText = "Mock Server Stopped";

    [ObservableProperty]
    private MockRouteRule? _selectedRoute;

    [ObservableProperty]
    private MockRequestLogItem? _selectedLog;

    public ObservableCollection<MockRouteRule> Routes { get; } = new();
    public ObservableCollection<MockRequestLogItem> RequestLogs { get; } = new();

    public MockServerToolViewModel()
    {
        Id = "MockServerTool";
        Title = "API Mock Server";

        _engine = new MockServerEngine(Port);
        foreach (var r in _engine.Routes)
        {
            Routes.Add(r);
        }
        if (Routes.Count > 0)
        {
            SelectedRoute = Routes[0];
        }

        _engine.RequestReceived += log =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                RequestLogs.Insert(0, log);
                if (RequestLogs.Count > 100)
                {
                    RequestLogs.RemoveAt(RequestLogs.Count - 1);
                }
            });
        };

        _engine.ServerStateChanged += running =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsRunning = running;
                StatusText = running 
                    ? $"Running on http://localhost:{Port}/" 
                    : "Mock Server Stopped";
            });
        };
    }

    [RelayCommand]
    public void ToggleServer()
    {
        if (IsRunning)
        {
            _engine.Stop();
        }
        else
        {
            bool ok = _engine.Start(Port);
            if (!ok)
            {
                StatusText = $"Failed to bind port {Port}. Try another port (e.g. 5245+).";
            }
        }
    }

    [RelayCommand]
    public void AddRoute()
    {
        var newRoute = new MockRouteRule
        {
            Name = "New Endpoint",
            Method = "GET",
            Path = $"/api/v1/resource{Routes.Count + 1}",
            StatusCode = 200,
            ResponseBody = "{\n  \"message\": \"Hello from mock endpoint\"\n}"
        };
        _engine.Routes.Add(newRoute);
        Routes.Add(newRoute);
        SelectedRoute = newRoute;
    }

    [RelayCommand]
    public void DeleteRoute()
    {
        if (SelectedRoute != null)
        {
            _engine.Routes.Remove(SelectedRoute);
            Routes.Remove(SelectedRoute);
            SelectedRoute = Routes.Count > 0 ? Routes[0] : null;
        }
    }

    [RelayCommand]
    public void ClearLogs()
    {
        RequestLogs.Clear();
        _engine.RequestLogs.Clear();
    }
}
