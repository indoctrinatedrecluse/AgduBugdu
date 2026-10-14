using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AgduBugdu.Core.Docker;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Tools;

public partial class DockerToolViewModel : Tool
{
    private readonly DockerCliService _dockerService = new();

    public ObservableCollection<DockerContainerItem> Containers { get; } = new();
    public ObservableCollection<DockerContainerItem> FilteredContainers { get; } = new();
    public ObservableCollection<DockerImageItem> Images { get; } = new();

    [ObservableProperty]
    private DockerContainerItem? _selectedContainer;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showOnlyRunning;

    [ObservableProperty]
    private string _containerLogs = "Select a container to inspect output...";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _daemonStatus = "Checking Docker...";

    [ObservableProperty]
    private int _selectedTabIndex; // 0 = Containers, 1 = Images

    public event Action<string>? OpenTerminalRequested;

    public DockerToolViewModel()
    {
        Id = "DockerWorkbench";
        Title = "Docker & Containers";

        _ = RefreshAllAsync();
    }

    public async Task RefreshAllAsync()
    {
        IsLoading = true;
        try
        {
            bool available = await _dockerService.CheckDockerAvailableAsync();
            DaemonStatus = available ? "Connected to Docker Daemon" : "Demo / Mock Mode (Docker Daemon Offline)";

            await RefreshContainersAsync();
            await RefreshImagesAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RefreshContainersAsync()
    {
        IsLoading = true;
        try
        {
            var list = await _dockerService.GetContainersAsync(all: true);
            Containers.Clear();
            foreach (var item in list)
            {
                Containers.Add(item);
            }
            ApplyFilter();

            if (SelectedContainer == null && FilteredContainers.Count > 0)
            {
                SelectedContainer = FilteredContainers[0];
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RefreshImagesAsync()
    {
        try
        {
            var list = await _dockerService.GetImagesAsync();
            Images.Clear();
            foreach (var item in list)
            {
                Images.Add(item);
            }
        }
        catch
        {
            // Ignore
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnShowOnlyRunningChanged(bool value) => ApplyFilter();

    partial void OnSelectedContainerChanged(DockerContainerItem? value)
    {
        if (value != null)
        {
            _ = ViewLogsAsync(value);
        }
    }

    private void ApplyFilter()
    {
        FilteredContainers.Clear();
        var query = Containers.AsEnumerable();

        if (ShowOnlyRunning)
        {
            query = query.Where(c => c.IsRunning);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string needle = SearchText.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.Name.ToLowerInvariant().Contains(needle) ||
                c.Image.ToLowerInvariant().Contains(needle) ||
                c.Id.ToLowerInvariant().Contains(needle));
        }

        foreach (var c in query)
        {
            FilteredContainers.Add(c);
        }
    }

    [RelayCommand]
    public async Task StartContainerAsync(DockerContainerItem? item)
    {
        var target = item ?? SelectedContainer;
        if (target == null) return;

        IsLoading = true;
        try
        {
            await _dockerService.StartContainerAsync(target.Id);
            await RefreshContainersAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task StopContainerAsync(DockerContainerItem? item)
    {
        var target = item ?? SelectedContainer;
        if (target == null) return;

        IsLoading = true;
        try
        {
            await _dockerService.StopContainerAsync(target.Id);
            await RefreshContainersAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RestartContainerAsync(DockerContainerItem? item)
    {
        var target = item ?? SelectedContainer;
        if (target == null) return;

        IsLoading = true;
        try
        {
            await _dockerService.RestartContainerAsync(target.Id);
            await RefreshContainersAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task PauseContainerAsync(DockerContainerItem? item)
    {
        var target = item ?? SelectedContainer;
        if (target == null) return;

        IsLoading = true;
        try
        {
            await _dockerService.PauseContainerAsync(target.Id);
            await RefreshContainersAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RemoveContainerAsync(DockerContainerItem? item)
    {
        var target = item ?? SelectedContainer;
        if (target == null) return;

        IsLoading = true;
        try
        {
            await _dockerService.RemoveContainerAsync(target.Id, force: true);
            await RefreshContainersAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ViewLogsAsync(DockerContainerItem? item)
    {
        var target = item ?? SelectedContainer;
        if (target == null) return;

        ContainerLogs = $"Fetching logs for {target.Name} ({target.ShortId})...";
        string logs = await _dockerService.GetContainerLogsAsync(target.Id);
        ContainerLogs = string.IsNullOrWhiteSpace(logs)
            ? $"[No output logged for container {target.Name}]"
            : logs;
    }

    [RelayCommand]
    public void AttachTerminal(DockerContainerItem? item)
    {
        var target = item ?? SelectedContainer;
        if (target == null) return;

        string cmd = $"docker exec -it {target.Id} /bin/sh";
        OpenTerminalRequested?.Invoke(cmd);
    }

    [RelayCommand]
    public async Task RemoveImageAsync(DockerImageItem? image)
    {
        if (image == null) return;
        await _dockerService.RemoveImageAsync(image.Id);
        await RefreshImagesAsync();
    }

    [RelayCommand]
    public async Task PruneContainersAsync()
    {
        IsLoading = true;
        try
        {
            await _dockerService.PruneUnusedContainersAsync();
            await RefreshContainersAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void ClearLogs()
    {
        ContainerLogs = string.Empty;
    }
}
