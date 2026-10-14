using System.Linq;
using System.Threading.Tasks;
using AgduBugdu.Core.Docker;
using Xunit;

namespace AgduBugdu.Tests;

public class DockerWorkbenchTests
{
    [Fact]
    public void DockerContainerItem_StateFormatting_ReturnsCorrectColorsAndBadges()
    {
        var running = new DockerContainerItem
        {
            Id = "1234567890abcdef",
            Name = "web-app",
            State = "running"
        };

        Assert.Equal("1234567890ab", running.ShortId);
        Assert.True(running.IsRunning);
        Assert.False(running.IsPaused);
        Assert.Equal("#10B981", running.StateBadgeColor);
        Assert.Equal("▶", running.StateIcon);

        var exited = new DockerContainerItem
        {
            Id = "abcdef123456",
            Name = "db",
            State = "exited"
        };

        Assert.False(exited.IsRunning);
        Assert.Equal("#EF4444", exited.StateBadgeColor);
        Assert.Equal("⏹", exited.StateIcon);

        var paused = new DockerContainerItem
        {
            Id = "998877665544",
            Name = "cache",
            State = "paused"
        };

        Assert.True(paused.IsPaused);
        Assert.Equal("#F59E0B", paused.StateBadgeColor);
        Assert.Equal("⏸", paused.StateIcon);
    }

    [Fact]
    public void ParseContainerLines_ParsesTabSeparatedDockerPs()
    {
        string raw = "a1b2c3d4e5f6\tmy-app\tnode:20-alpine\trunning\tUp 5 minutes\t0.0.0.0:3000->3000/tcp\t2026-10-14\n" +
                     "f6e5d4c3b2a1\tdatabase\tpostgres:16\texited\tExited (0) 1 hour ago\t\t2026-10-13";

        var list = DockerCliService.ParseContainerLines(raw);

        Assert.Equal(2, list.Count);
        Assert.Equal("a1b2c3d4e5f6", list[0].Id);
        Assert.Equal("my-app", list[0].Name);
        Assert.Equal("node:20-alpine", list[0].Image);
        Assert.Equal("running", list[0].State);
        Assert.Equal("0.0.0.0:3000->3000/tcp", list[0].Ports);

        Assert.Equal("f6e5d4c3b2a1", list[1].Id);
        Assert.Equal("database", list[1].Name);
        Assert.Equal("exited", list[1].State);
    }

    [Fact]
    public void ParseImageLines_ParsesTabSeparatedDockerImages()
    {
        string raw = "sha256:112233445566\tredis\t7.2\t45 MB\t2 weeks ago\n" +
                     "sha256:665544332211\tpython\t3.12-slim\t120 MB\t3 days ago";

        var list = DockerCliService.ParseImageLines(raw);

        Assert.Equal(2, list.Count);
        Assert.Equal("redis", list[0].Repository);
        Assert.Equal("7.2", list[0].Tag);
        Assert.Equal("redis:7.2", list[0].FullName);
        Assert.Equal("45 MB", list[0].Size);

        Assert.Equal("python", list[1].Repository);
        Assert.Equal("3.12-slim", list[1].Tag);
    }

    [Fact]
    public async Task DockerCliService_DemoMode_LifecycleOperationsWorkWithoutDaemon()
    {
        var service = new DockerCliService();
        var containers = await service.GetContainersAsync();

        Assert.NotEmpty(containers);

        var first = containers[0];
        string originalId = first.Id;

        // Test stop
        await service.StopContainerAsync(originalId);
        var updated = await service.GetContainersAsync();
        var match = updated.First(c => c.Id == originalId);
        Assert.Equal("exited", match.State);

        // Test start
        await service.StartContainerAsync(originalId);
        updated = await service.GetContainersAsync();
        match = updated.First(c => c.Id == originalId);
        Assert.Equal("running", match.State);

        // Test logs
        string logs = await service.GetContainerLogsAsync(originalId);
        Assert.Contains(originalId, logs);

        // Test images
        var images = await service.GetImagesAsync();
        Assert.NotEmpty(images);
    }
}
