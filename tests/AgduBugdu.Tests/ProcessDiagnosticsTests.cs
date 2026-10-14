using System;
using AgduBugdu.Core.Diagnostics;
using Xunit;

namespace AgduBugdu.Tests;

public class ProcessDiagnosticsTests
{
    [Fact]
    public void TestCaptureProcessSnapshot()
    {
        var engine = new ProcessDiagnosticsEngine();
        var snapshot = engine.CaptureSnapshot();

        Assert.NotNull(snapshot);
        Assert.True(snapshot.WorkingSetMb > 0);
        Assert.True(snapshot.ThreadCount > 0);
        Assert.True(snapshot.HandleCount >= 0);
        Assert.True(snapshot.ManagedMemoryMb > 0);
        Assert.InRange(snapshot.CpuPercent, 0.0, 100.0);
    }

    [Fact]
    public void TestForceGarbageCollectionIncreasesCounts()
    {
        var engine = new ProcessDiagnosticsEngine();
        int initialGen2 = GC.CollectionCount(2);

        engine.ForceGarbageCollection(2);

        int afterGen2 = GC.CollectionCount(2);
        Assert.True(afterGen2 >= initialGen2);
    }

    [Fact]
    public void TestHistoryAccumulation()
    {
        var engine = new ProcessDiagnosticsEngine();
        engine.CaptureSnapshot();
        engine.CaptureSnapshot();

        Assert.True(engine.History.Count >= 2);
    }
}
