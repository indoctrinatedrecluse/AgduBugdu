using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AgduBugdu.Core.Diagnostics;

public class ProcessMetricSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public double WorkingSetMb { get; set; }
    public double PrivateMemoryMb { get; set; }
    public double ManagedMemoryMb { get; set; }
    public double CpuPercent { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public int Gen0Count { get; set; }
    public int Gen1Count { get; set; }
    public int Gen2Count { get; set; }
    public TimeSpan Uptime { get; set; }
}

public class ProcessDiagnosticsEngine
{
    private DateTime _lastCpuSampleTime = DateTime.UtcNow;
    private TimeSpan _lastCpuTotalProcessorTime = TimeSpan.Zero;
    private readonly Process _process;
    private readonly List<ProcessMetricSnapshot> _history = new();
    private readonly int _maxHistory = 60;

    public IReadOnlyList<ProcessMetricSnapshot> History => _history;

    public ProcessDiagnosticsEngine()
    {
        _process = Process.GetCurrentProcess();
        _lastCpuSampleTime = DateTime.UtcNow;
        try
        {
            _lastCpuTotalProcessorTime = _process.TotalProcessorTime;
        }
        catch
        {
            _lastCpuTotalProcessorTime = TimeSpan.Zero;
        }
    }

    public ProcessMetricSnapshot CaptureSnapshot()
    {
        _process.Refresh();

        var now = DateTime.UtcNow;
        double cpuPercent = 0.0;
        try
        {
            var currentTotal = _process.TotalProcessorTime;
            var timePassed = (now - _lastCpuSampleTime).TotalMilliseconds;
            if (timePassed > 100)
            {
                var cpuPassed = (currentTotal - _lastCpuTotalProcessorTime).TotalMilliseconds;
                cpuPercent = Math.Round((cpuPassed / (timePassed * Environment.ProcessorCount)) * 100.0, 1);
                cpuPercent = Math.Clamp(cpuPercent, 0.0, 100.0);

                _lastCpuSampleTime = now;
                _lastCpuTotalProcessorTime = currentTotal;
            }
        }
        catch
        {
            cpuPercent = 0.0;
        }

        TimeSpan uptime = TimeSpan.Zero;
        try
        {
            uptime = DateTime.Now - _process.StartTime;
        }
        catch
        {
            uptime = TimeSpan.FromMinutes(1);
        }

        int threadCount = 1;
        try
        {
            threadCount = _process.Threads.Count;
        }
        catch
        {
            threadCount = 1;
        }

        int handleCount = 0;
        try
        {
            handleCount = _process.HandleCount;
        }
        catch
        {
            handleCount = 0;
        }

        var snapshot = new ProcessMetricSnapshot
        {
            Timestamp = DateTime.Now,
            WorkingSetMb = Math.Round(_process.WorkingSet64 / (1024.0 * 1024.0), 2),
            PrivateMemoryMb = Math.Round(_process.PrivateMemorySize64 / (1024.0 * 1024.0), 2),
            ManagedMemoryMb = Math.Round(GC.GetTotalMemory(false) / (1024.0 * 1024.0), 2),
            CpuPercent = cpuPercent,
            ThreadCount = threadCount,
            HandleCount = handleCount,
            Gen0Count = GC.CollectionCount(0),
            Gen1Count = GC.CollectionCount(1),
            Gen2Count = GC.CollectionCount(2),
            Uptime = uptime
        };

        _history.Add(snapshot);
        if (_history.Count > _maxHistory)
        {
            _history.RemoveAt(0);
        }

        return snapshot;
    }

    public void ForceGarbageCollection(int generation = 2)
    {
        GC.Collect(Math.Clamp(generation, 0, 2), GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(Math.Clamp(generation, 0, 2), GCCollectionMode.Forced, true, true);
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize")]
    private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

    public void TrimWorkingSet()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
            ForceGarbageCollection(2);
        }
        catch
        {
            // Platform specific fallback
        }
    }
}
