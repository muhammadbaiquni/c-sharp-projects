using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MpcplBuilder.Application.Explorer;

namespace MpcplBuilder.Wpf.ViewModels;

public sealed partial class FolderNodeViewModel : ObservableObject
{
    private readonly ILoadExplorerChildren? _loadChildren;
    private readonly Action? _stateChanged;
    private CancellationTokenSource? _loadCancellation;
    private Task? _loadTask;
    private long _loadVersion;
    private bool _childrenLoaded;

    [ObservableProperty] private bool isExpanded;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanGenerate))] private bool isAvailable;
    [ObservableProperty] private string status = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IconKey)), NotifyPropertyChangedFor(nameof(PlaylistStatus))]
    private bool hasPlaylist;

    public FolderNodeViewModel(ExplorerFolder folder, ILoadExplorerChildren loadChildren)
        : this(folder, loadChildren, null) { }

    internal FolderNodeViewModel(ExplorerFolder folder, ILoadExplorerChildren loadChildren, Action? stateChanged)
    {
        FullPath = folder.Path;
        DisplayName = folder.DisplayName;
        HasPlaylist = folder.HasPlaylist;
        IsAvailable = folder.IsAvailable;
        _loadChildren = loadChildren;
        _stateChanged = stateChanged;
        if (IsAvailable) Children.Add(new FolderNodeViewModel(isPlaceholder: true));
        else Status = "Folder unavailable";
    }

    private FolderNodeViewModel(bool isPlaceholder)
    {
        IsPlaceholder = isPlaceholder;
        DisplayName = isPlaceholder ? "Loading..." : "This PC";
        _childrenLoaded = true;
    }

    internal static FolderNodeViewModel ThisPc(IEnumerable<FolderNodeViewModel> drives, bool expanded = true)
    {
        var root = new FolderNodeViewModel(isPlaceholder: false) { IsExpanded = expanded };
        foreach (var drive in drives) root.Children.Add(drive);
        return root;
    }

    public string? FullPath { get; }
    public string DisplayName { get; }
    public bool IsPlaceholder { get; }
    public bool CanGenerate => FullPath is not null && IsAvailable && !IsPlaceholder;
    public ObservableCollection<FolderNodeViewModel> Children { get; } = [];
    public string IconKey => HasPlaylist ? "GreenFolderIcon" : "YellowFolderIcon";
    public string PlaylistStatus => FullPath is null ? DisplayName : HasPlaylist ? "Playlist present" : "No playlist";

    partial void OnIsExpandedChanged(bool value)
    {
        if (value) _ = EnsureChildrenAsync();
    }

    partial void OnIsLoadingChanged(bool value) => _stateChanged?.Invoke();

    public Task ExpandAsync()
    {
        if (IsExpanded) return EnsureChildrenAsync();
        IsExpanded = true;
        return _loadTask ?? Task.CompletedTask;
    }

    private Task EnsureChildrenAsync()
    {
        if (_childrenLoaded || !IsAvailable || _loadChildren is null) return Task.CompletedTask;
        if (IsLoading) return _loadTask ?? Task.CompletedTask;
        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        var version = ++_loadVersion;
        IsLoading = true;
        Status = "Loading folders...";
        return _loadTask = LoadChildrenAsync(version, cancellation);
    }

    private async Task LoadChildrenAsync(long version, CancellationTokenSource cancellation)
    {
        try
        {
            var result = await _loadChildren!.ExecuteAsync(FullPath!, cancellation.Token);
            if (version != _loadVersion || cancellation.IsCancellationRequested) return;
            if (result.Status is ExplorerLoadStatus.Success or ExplorerLoadStatus.PartialAccess)
            {
                Children.Clear();
                foreach (var folder in result.Folders)
                    Children.Add(new FolderNodeViewModel(folder, _loadChildren, _stateChanged));
                _childrenLoaded = true;
                Status = result.Status == ExplorerLoadStatus.PartialAccess
                    ? result.ErrorMessage ?? "Some folders could not be read"
                    : PlaylistStatus;
            }
            else
            {
                if (result.Status == ExplorerLoadStatus.Missing)
                {
                    IsAvailable = false;
                    Children.Clear();
                }
                Status = result.ErrorMessage ?? (result.Status == ExplorerLoadStatus.Missing ? "Folder unavailable" : "Cannot read folder");
            }
        }
        catch (OperationCanceledException)
        {
            if (version == _loadVersion) Status = "Cancelled";
        }
        catch (Exception exception)
        {
            if (version == _loadVersion) Status = exception.Message;
        }
        finally
        {
            if (version == _loadVersion)
            {
                _loadCancellation = null;
                IsLoading = false;
            }
            cancellation.Dispose();
        }
    }

    // Refresh invalidates even providers that finish successfully after cancellation.
    public void CancelLoads()
    {
        ++_loadVersion;
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        _loadTask = null;
        if (IsLoading) Status = "Cancelled";
        IsLoading = false;
        foreach (var child in Children) child.CancelLoads();
    }
}
