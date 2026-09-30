using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    [ObservableProperty] private bool createPlaylistsForFirstLevelSubfolders;
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
        SelectedFolders.CollectionChanged += OnSelectedFoldersChanged;
    }

    public ObservableCollection<FolderNodeViewModel> Roots { get; } = [];
    public ObservableCollection<FolderNodeViewModel> SelectedFolders { get; } = [];

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
        if (_isGenerating) return;
        var selectedPath = SelectedFolder?.FullPath;
        var selectedPaths = CurrentSelection().Select(folder => folder.FullPath!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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
        SelectedFolders.Clear();
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
                await RestoreAsync(root, expandedPaths, selectedPaths, selectedPath, version, cancellation);
                if (!IsCurrent(version, cancellation)) return;
                SelectedFolder ??= SelectedFolders.FirstOrDefault();
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
                if (cancellation.IsCancellationRequested) Status = "Cancelled";
                UpdateBusy();
            }
            cancellation.Dispose();
        }
    }

    private async Task RestoreAsync(FolderNodeViewModel parent, HashSet<string> expandedPaths,
        HashSet<string> selectedPaths, string? selectedPath, long version, CancellationTokenSource cancellation)
    {
        foreach (var node in parent.Children)
        {
            if (!IsCurrent(version, cancellation)) return;
            if (node.FullPath is not { } path) continue;
            if (selectedPaths.Contains(path)) SelectedFolders.Add(node);
            if (StringComparer.OrdinalIgnoreCase.Equals(path, selectedPath)) SelectedFolder = node;
            var expanded = expandedPaths.Contains(path);
            var containsSelection = selectedPaths.Any(selected => IsDescendant(selected, path));
            var containsExpansion = expandedPaths.Any(p => IsDescendant(p, path));
            if (!expanded && !containsSelection && !containsExpansion) continue;
            await node.ExpandAsync();
            if (!IsCurrent(version, cancellation)) return;
            node.IsExpanded = expanded;
            await RestoreAsync(node, expandedPaths, selectedPaths, selectedPath, version, cancellation);
        }
    }

    private static bool IsDescendant(string path, string ancestor) =>
        path.StartsWith(ancestor.TrimEnd('\\', '/') + "\\", StringComparison.OrdinalIgnoreCase);

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        if (!CanGenerate()) return;
        var selectedFolders = CurrentSelection();
        if (selectedFolders.Count == 0) return;
        var selectionPaths = selectedFolders.Select(selected => selected.FullPath!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var folder = SelectedFolder?.CanGenerate == true ? SelectedFolder : selectedFolders[0];
        var path = folder.FullPath!;
        var batchScope = selectedFolders.Count == 1 ? path : $"{selectedFolders.Count} selected root folders";
        var version = ++_operationVersion;
        var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        _isGenerating = true;
        UpdateBusy();
        Status = "Generating playlist...";
        try
        {
            IReadOnlyList<(string Path, FolderNodeViewModel? Node, bool? Exists)> targets;
            BatchOverwritePolicy? batchOverwritePolicy = null;
            if (CreatePlaylistsForFirstLevelSubfolders)
            {
                var childTargets = new List<(string Path, FolderNodeViewModel? Node)>();
                foreach (var selected in selectedFolders)
                {
                    var children = await _loadChildren.ExecuteAsync(selected.FullPath!, cancellation.Token);
                    if (!IsOperationActive(selectionPaths, version, cancellation)) return;
                    if (children.Status is not (ExplorerLoadStatus.Success or ExplorerLoadStatus.PartialAccess))
                    {
                        Status = children.ErrorMessage ?? "Cannot read first-level subfolders";
                        _dialogs.ShowError(Status);
                        return;
                    }

                    childTargets.AddRange(children.Folders.Where(child => child.IsAvailable)
                        .Select(child => (child.Path, Node: FindLoadedChild(selected, child.Path))));
                }

                var distinctChildTargets = childTargets
                    .DistinctBy(child => child.Path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (distinctChildTargets.Length == 0)
                {
                    Status = "No first-level subfolders found";
                    return;
                }

                targets = distinctChildTargets.Select(target => (target.Path, target.Node, (bool?)null)).ToArray();
            }
            else
            {
                targets = selectedFolders.Select(selected => (selected.FullPath!, (FolderNodeViewModel?)selected, (bool?)null))
                    .ToArray();
            }

            if (CreatePlaylistsForFirstLevelSubfolders || targets.Count > 1)
            {
                var inspectedTargets = new List<(string Path, FolderNodeViewModel? Node, bool? Exists)>(targets.Count);
                foreach (var target in targets)
                {
                    var exists = await _inspectPresence.ExecuteAsync(target.Path, cancellation.Token);
                    if (!IsOperationActive(selectionPaths, version, cancellation)) return;
                    inspectedTargets.Add((target.Path, target.Node, exists));
                }
                targets = inspectedTargets;
                batchOverwritePolicy = new BatchOverwritePolicy();
                var existingPlaylistCount = inspectedTargets.Count(target => target.Exists == true);
                if (existingPlaylistCount > 0)
                {
                    batchOverwritePolicy.OverwriteExisting = _dialogs.ConfirmBatchOverwrite(batchScope, existingPlaylistCount);
                    if (!IsOperationActive(selectionPaths, version, cancellation)) return;
                }
            }

            foreach (var target in targets)
            {
                if (batchOverwritePolicy?.OverwriteExisting == false && target.Exists == true) continue;
                if (!await GenerateForPathAsync(target.Path, target.Node, batchScope, version, cancellation,
                        selectionPaths, target.Exists, batchOverwritePolicy)) return;
            }
            Status = "Done";
        }
        catch (OperationCanceledException)
        {
            if (IsCurrent(version, cancellation) && IsSelectionCurrent(selectionPaths)) Status = "Cancelled";
        }
        catch (Exception exception)
        {
            if (IsCurrent(version, cancellation) && IsSelectionCurrent(selectionPaths))
            {
                Status = exception.Message;
                _dialogs.ShowError(exception.Message);
            }
        }
        finally
        {
            if (version == _operationVersion)
            {
                _operationCancellation = null;
                _isGenerating = false;
                if (cancellation.IsCancellationRequested && IsSelectionCurrent(selectionPaths)) Status = "Cancelled";
                else if (!IsSelectionCurrent(selectionPaths) && Status is "Generating playlist..." or "Cancelling...") Status = "Ready";
                UpdateBusy();
            }
            cancellation.Dispose();
        }
    }

    private async Task<bool> GenerateForPathAsync(string targetPath, FolderNodeViewModel? node,
        string batchScope, long version, CancellationTokenSource cancellation,
        IReadOnlySet<string> selectionPaths, bool? knownPresence = null,
        BatchOverwritePolicy? batchOverwritePolicy = null)
    {
        var request = IsLongPath ? GeneratePlaylistRequest.Long(targetPath)
            : IsFullPath ? GeneratePlaylistRequest.Full(targetPath) : GeneratePlaylistRequest.Relative(targetPath);
        var exists = knownPresence;
        if (exists is null)
        {
            exists = await _inspectPresence.ExecuteAsync(targetPath, cancellation.Token);
            if (!IsOperationActive(selectionPaths, version, cancellation)) return false;
        }
        if (exists == true)
        {
            var overwrite = batchOverwritePolicy?.OverwriteExisting
                ?? _dialogs.ConfirmOverwrite(System.IO.Path.Combine(targetPath, "Playlist.mpcpl"));
            if (!overwrite)
            {
                if (batchOverwritePolicy is not null) return true;
                Status = "Cancelled";
                return false;
            }
            request = request with { OverwriteExisting = true };
        }
        if (!IsOperationActive(selectionPaths, version, cancellation)) return false;
        var result = await _generatePlaylist.ExecuteAsync(request, cancellation.Token);
        if (!IsOperationActive(selectionPaths, version, cancellation)) return false;
        if (result.Status == PlaylistGenerationStatus.OverwriteRequired && !request.OverwriteExisting)
        {
            bool overwrite;
            if (batchOverwritePolicy is not null)
            {
                batchOverwritePolicy.OverwriteExisting ??=
                    _dialogs.ConfirmBatchOverwrite(batchScope, 1);
                overwrite = batchOverwritePolicy.OverwriteExisting.Value;
            }
            else
            {
                overwrite = _dialogs.ConfirmOverwrite(System.IO.Path.Combine(targetPath, "Playlist.mpcpl"));
            }
            if (!overwrite)
            {
                if (batchOverwritePolicy is not null) return true;
                Status = "Cancelled";
                return false;
            }
            if (!IsOperationActive(selectionPaths, version, cancellation)) return false;
            result = await _generatePlaylist.ExecuteAsync(request with { OverwriteExisting = true }, cancellation.Token);
            if (!IsOperationActive(selectionPaths, version, cancellation)) return false;
        }
        if (result.Status == PlaylistGenerationStatus.Success)
        {
            var hasPlaylist = await _inspectPresence.ExecuteAsync(targetPath, cancellation.Token);
            if (!IsOperationActive(selectionPaths, version, cancellation)) return false;
            if (node is not null) node.HasPlaylist = hasPlaylist;
            return true;
        }
        if (result.Status == PlaylistGenerationStatus.NoVideos) return true;

        Status = result.Status == PlaylistGenerationStatus.OverwriteRequired
            ? "Playlist already exists; overwrite confirmation required"
            : result.ErrorMessage ?? "Playlist generation failed";
        _dialogs.ShowError(Status);
        return false;
    }

    private static FolderNodeViewModel? FindLoadedChild(FolderNodeViewModel folder, string path) =>
        folder.Children.FirstOrDefault(child => StringComparer.OrdinalIgnoreCase.Equals(child.FullPath, path));

    private bool IsOperationActive(IReadOnlySet<string> selectionPaths, long version,
        CancellationTokenSource cancellation) =>
        IsCurrent(version, cancellation) && IsSelectionCurrent(selectionPaths);

    private bool IsSelectionCurrent(IReadOnlySet<string> expectedPaths)
    {
        var currentPaths = CurrentSelection().Select(folder => folder.FullPath!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return expectedPaths.SetEquals(currentPaths);
    }

    private sealed class BatchOverwritePolicy
    {
        public bool? OverwriteExisting { get; set; }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _operationCancellation?.Cancel();
        foreach (var root in Roots) root.CancelLoads();
        Status = _isRefreshing || _isGenerating ? "Cancelling..." : "Cancelled";
        UpdateBusy();
    }

    private bool IsCurrent(long version, CancellationTokenSource cancellation) =>
        version == _operationVersion && !cancellation.IsCancellationRequested;
    private bool CanGenerate() => !IsBusy && CurrentSelection().Count > 0;
    private bool CanCancel() => IsBusy;
    private bool CanRefresh() => !_isRefreshing && !_isGenerating;
    private void OnSelectedFoldersChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.Action == NotifyCollectionChangedAction.Reset)
            foreach (var node in AllNodes()) node.IsMultiSelected = false;
        if (args.OldItems is not null)
            foreach (FolderNodeViewModel node in args.OldItems)
                if (!SelectedFolders.Contains(node)) node.IsMultiSelected = false;
        if (args.NewItems is not null)
            foreach (FolderNodeViewModel node in args.NewItems) node.IsMultiSelected = true;
        GenerateCommand.NotifyCanExecuteChanged();
    }
    private void UpdateBusy()
    {
        IsBusy = _isRefreshing || _isGenerating || AllNodes().Any(n => n.IsLoading);
        RefreshCommand.NotifyCanExecuteChanged();
    }

    private IEnumerable<FolderNodeViewModel> AllNodes() => Roots.SelectMany(DescendantsAndSelf);
    private IReadOnlyList<FolderNodeViewModel> CurrentSelection()
    {
        var selected = SelectedFolders.Where(folder => folder.CanGenerate)
            .DistinctBy(folder => folder.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (selected.Length > 0) return selected;
        return SelectedFolder?.CanGenerate == true ? [SelectedFolder] : [];
    }
    private static IEnumerable<FolderNodeViewModel> DescendantsAndSelf(FolderNodeViewModel node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in DescendantsAndSelf(child)) yield return descendant;
    }
}
