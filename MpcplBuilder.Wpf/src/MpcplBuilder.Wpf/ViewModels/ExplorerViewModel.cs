using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MpcplBuilder.Application.Explorer;
using MpcplBuilder.Application.Playlists;
using MpcplBuilder.Wpf.Services;

namespace MpcplBuilder.Wpf.ViewModels;

public sealed partial class ExplorerViewModel : ObservableObject
{
    private readonly ILoadExplorerRoots _loadRoots;
    private readonly ILoadExplorerChildren _loadChildren;
    private readonly IInspectPlaylistPresence _inspectPresence;
    private readonly IGeneratePlaylist _generatePlaylist;
    private readonly IUserDialogService _dialogs;
    private CancellationTokenSource? _operationCancellation;
    private long _operationVersion;
    private bool _isRefreshing;
    private bool _isGenerating;
    private bool _initialized;
    private Task? _initializationTask;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private FolderNodeViewModel? selectedFolder;
    [ObservableProperty] private bool isRelativePath = true;
    [ObservableProperty] private bool isFullPath;
    [ObservableProperty] private bool isLongPath;
    [ObservableProperty] private string status = "Ready";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GenerateCommand)), NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isBusy;

    public ExplorerViewModel(ILoadExplorerRoots loadRoots, ILoadExplorerChildren loadChildren,
        IInspectPlaylistPresence inspectPresence, IGeneratePlaylist generatePlaylist, IUserDialogService dialogs)
    {
        _loadRoots = loadRoots;
        _loadChildren = loadChildren;
        _inspectPresence = inspectPresence;
        _generatePlaylist = generatePlaylist;
        _dialogs = dialogs;
    }

    public ObservableCollection<FolderNodeViewModel> Roots { get; } = [];

    [RelayCommand]
    private Task InitializeAsync()
    {
        if (_initialized) return Task.CompletedTask;
        if (_isRefreshing) return _initializationTask ?? Task.CompletedTask;
        return _initializationTask = RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task RefreshAsync()
    {
        var selectedPath = SelectedFolder?.FullPath;
        var expandedPaths = AllNodes().Where(n => n.IsExpanded && n.FullPath is not null)
            .Select(n => n.FullPath!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var thisPcExpanded = Roots.FirstOrDefault()?.IsExpanded ?? true;
        var version = ++_operationVersion;
        _operationCancellation?.Cancel();
        foreach (var root in Roots) root.CancelLoads();
        var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        _isRefreshing = true;
        UpdateBusy();
        SelectedFolder = null;
        Status = "Loading drives...";
        try
        {
            var result = await _loadRoots.ExecuteAsync(cancellation.Token);
            if (!IsCurrent(version, cancellation)) return;
            Roots.Clear();
            var root = FolderNodeViewModel.ThisPc(result.Folders.Select(f => new FolderNodeViewModel(f, _loadChildren, UpdateBusy)), thisPcExpanded);
            Roots.Add(root);
            if (result.Status is ExplorerLoadStatus.Success or ExplorerLoadStatus.PartialAccess)
            {
                await RestoreAsync(root, expandedPaths, selectedPath, version, cancellation);
                if (!IsCurrent(version, cancellation)) return;
                _initialized = true;
            }
            Status = result.Status == ExplorerLoadStatus.Success ? "Ready" : result.ErrorMessage ?? "Cannot read drives";
        }
        catch (OperationCanceledException)
        {
            if (version == _operationVersion) Status = "Cancelled";
        }
        catch (Exception exception)
        {
            if (version == _operationVersion) Status = exception.Message;
        }
        finally
        {
            if (version == _operationVersion)
            {
                _operationCancellation = null;
                _isRefreshing = false;
                UpdateBusy();
            }
            cancellation.Dispose();
        }
    }

    private async Task RestoreAsync(FolderNodeViewModel parent, HashSet<string> expandedPaths,
        string? selectedPath, long version, CancellationTokenSource cancellation)
    {
        foreach (var node in parent.Children)
        {
            if (!IsCurrent(version, cancellation)) return;
            if (node.FullPath is not { } path) continue;
            if (StringComparer.OrdinalIgnoreCase.Equals(path, selectedPath)) SelectedFolder = node;
            var expanded = expandedPaths.Contains(path);
            var containsSelection = selectedPath is not null && IsDescendant(selectedPath, path);
            var containsExpansion = expandedPaths.Any(p => IsDescendant(p, path));
            if (!expanded && !containsSelection && !containsExpansion) continue;
            await node.ExpandAsync();
            if (!IsCurrent(version, cancellation)) return;
            node.IsExpanded = expanded;
            await RestoreAsync(node, expandedPaths, selectedPath, version, cancellation);
        }
    }

    private static bool IsDescendant(string path, string ancestor) =>
        path.StartsWith(ancestor.TrimEnd('\\', '/') + "\\", StringComparison.OrdinalIgnoreCase);

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        if (!CanGenerate()) return;
        var folder = SelectedFolder!;
        var path = folder.FullPath!;
        var request = IsLongPath ? GeneratePlaylistRequest.Long(path)
            : IsFullPath ? GeneratePlaylistRequest.Full(path) : GeneratePlaylistRequest.Relative(path);
        var version = ++_operationVersion;
        var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        _isGenerating = true;
        UpdateBusy();
        Status = "Generating playlist...";
        try
        {
            var exists = await _inspectPresence.ExecuteAsync(path, cancellation.Token);
            if (!IsCurrent(version, cancellation) || !IsSelectedPath(path)) return;
            if (exists)
            {
                if (!_dialogs.ConfirmOverwrite(System.IO.Path.Combine(path, "Playlist.mpcpl")))
                {
                    Status = "Cancelled";
                    return;
                }
                request = request with { OverwriteExisting = true };
            }
            if (!IsCurrent(version, cancellation) || !IsSelectedPath(path)) return;
            var result = await _generatePlaylist.ExecuteAsync(request, cancellation.Token);
            if (!IsCurrent(version, cancellation) || !IsSelectedPath(path)) return;
            if (result.Status == PlaylistGenerationStatus.OverwriteRequired && !request.OverwriteExisting)
            {
                if (!_dialogs.ConfirmOverwrite(System.IO.Path.Combine(path, "Playlist.mpcpl")))
                {
                    Status = "Cancelled";
                    return;
                }
                if (!IsCurrent(version, cancellation) || !IsSelectedPath(path)) return;
                result = await _generatePlaylist.ExecuteAsync(request with { OverwriteExisting = true }, cancellation.Token);
                if (!IsCurrent(version, cancellation) || !IsSelectedPath(path)) return;
            }
            if (result.Status == PlaylistGenerationStatus.Success)
            {
                var hasPlaylist = await _inspectPresence.ExecuteAsync(path, cancellation.Token);
                if (!IsCurrent(version, cancellation) || !IsSelectedPath(path)) return;
                folder.HasPlaylist = hasPlaylist;
            }
            Status = result.Status == PlaylistGenerationStatus.Success ? "Done"
                : result.Status == PlaylistGenerationStatus.OverwriteRequired ? "Playlist already exists; overwrite confirmation required"
                : result.ErrorMessage ?? "Playlist generation failed";
        }
        catch (OperationCanceledException)
        {
            if (version == _operationVersion) Status = "Cancelled";
        }
        catch (Exception exception)
        {
            if (version == _operationVersion) Status = exception.Message;
        }
        finally
        {
            if (version == _operationVersion)
            {
                _operationCancellation = null;
                _isGenerating = false;
                UpdateBusy();
            }
            cancellation.Dispose();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        ++_operationVersion;
        _operationCancellation?.Cancel();
        _operationCancellation = null;
        foreach (var root in Roots) root.CancelLoads();
        _isRefreshing = _isGenerating = false;
        Status = "Cancelled";
        UpdateBusy();
    }

    private bool IsCurrent(long version, CancellationTokenSource cancellation) =>
        version == _operationVersion && !cancellation.IsCancellationRequested;
    private bool IsSelectedPath(string path) =>
        StringComparer.OrdinalIgnoreCase.Equals(SelectedFolder?.FullPath, path);
    private bool CanGenerate() => !IsBusy && SelectedFolder?.CanGenerate == true;
    private bool CanCancel() => IsBusy;
    private bool CanRefresh() => !_isRefreshing && !_isGenerating;
    private void UpdateBusy()
    {
        IsBusy = _isRefreshing || _isGenerating || AllNodes().Any(n => n.IsLoading);
        RefreshCommand.NotifyCanExecuteChanged();
    }

    private IEnumerable<FolderNodeViewModel> AllNodes() => Roots.SelectMany(DescendantsAndSelf);
    private static IEnumerable<FolderNodeViewModel> DescendantsAndSelf(FolderNodeViewModel node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in DescendantsAndSelf(child)) yield return descendant;
    }
}
