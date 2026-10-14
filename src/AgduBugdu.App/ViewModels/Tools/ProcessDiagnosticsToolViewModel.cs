using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using AgduBugdu.Core.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Tools;

public partial class ProcessDiagnosticsToolViewModel : Tool
{
    private readonly ProcessDiagnosticsEngine _engine = new();

    [ObservableProperty]
    private double _workingSetMb;

    [ObservableProperty]
    private double _privateMemoryMb;

    [ObservableProperty]
    private double _managedMemoryMb;

    [ObservableProperty]
    private double _cpuPercent;

    [ObservableProperty]
    private int _threadCount;

    [ObservableProperty]
    private int _handleCount;

    [ObservableProperty]
    private int _gen0Count;

    [ObservableProperty]
    private int _gen1Count;

    [ObservableProperty]
    private int _gen2Count;

    [ObservableProperty]
    private string _uptimeText = "00:00:00";

    [ObservableProperty]
    private string _statusText = "Process metrics active";

    public ObservableCollection<ProcessMetricSnapshot> Snapshots { get; } = new();

    public ProcessDiagnosticsToolViewModel()
    {
        Id = "ProcessDiagnosticsTool";
        Title = "Process Diagnostics & Profiler";

        RefreshSnapshot();
    }

    [RelayCommand]
    public void RefreshSnapshot()
    {
        var s = _engine.CaptureSnapshot();
        WorkingSetMb = s.WorkingSetMb;
        PrivateMemoryMb = s.PrivateMemoryMb;
        ManagedMemoryMb = s.ManagedMemoryMb;
        CpuPercent = s.CpuPercent;
        ThreadCount = s.ThreadCount;
        HandleCount = s.HandleCount;
        Gen0Count = s.Gen0Count;
        Gen1Count = s.Gen1Count;
        Gen2Count = s.Gen2Count;
        UptimeText = $"{(int)s.Uptime.TotalHours:D2}:{s.Uptime.Minutes:D2}:{s.Uptime.Seconds:D2}";

        Snapshots.Insert(0, s);
        if (Snapshots.Count > 30)
        {
            Snapshots.RemoveAt(Snapshots.Count - 1);
        }

        StatusText = $"Snapshot captured at {s.Timestamp:HH:mm:ss} | Managed: {s.ManagedMemoryMb} MB | WS: {s.WorkingSetMb} MB";
    }

    [RelayCommand]
    public void ForceGc()
    {
        _engine.ForceGarbageCollection(2);
        RefreshSnapshot();
        StatusText = "Triggered Full Generation 2 Garbage Collection";
    }

    [RelayCommand]
    public void TrimMemory()
    {
        _engine.TrimWorkingSet();
        RefreshSnapshot();
        StatusText = "Invoked Process.TrimWorkingSet()";
    }
}
