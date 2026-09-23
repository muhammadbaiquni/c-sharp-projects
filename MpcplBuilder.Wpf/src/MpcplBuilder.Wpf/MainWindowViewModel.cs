using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MpcplBuilder.Application.Folders;
using MpcplBuilder.Application.Playlists;
using MpcplBuilder.Wpf.Services;

namespace MpcplBuilder.Wpf;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IInspectFolder _inspectFolder;
    private readonly IGeneratePlaylist _generatePlaylist;
    private readonly IFolderPicker _folderPicker;
    private readonly IUserDialogService _dialogs;
    private CancellationTokenSource? _inspectionCancellation;
    private CancellationTokenSource? _generationCancellation;
    private long _inspectionVersion;
    private bool _suppressRootInspection;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private string rootPath = string.Empty;
    [ObservableProperty] private string outputInfo = "(will be saved as \"Playlist.mpcpl\" in the root)";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GenerateCommand))] private bool hasVideos;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GenerateCommand))] private bool isOutputExists;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GenerateCommand))] private bool overwriteConfirmed;
    [ObservableProperty] private string status = "Ready";
    [ObservableProperty] private string counts = string.Empty;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GenerateCommand)), NotifyCanExecuteChangedFor(nameof(BrowseCommand)), NotifyCanExecuteChangedFor(nameof(ClearCommand)), NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isBusy;
    [ObservableProperty] private Visibility progressVisibility = Visibility.Collapsed;
    [ObservableProperty] private bool isProgressIndeterminate;
    [ObservableProperty] private bool isRelativePath = true;
    [ObservableProperty] private bool isFullPath;
    [ObservableProperty] private bool isLongPath;

    public MainWindowViewModel(IInspectFolder inspectFolder, IGeneratePlaylist generatePlaylist,
        IFolderPicker folderPicker, IUserDialogService dialogs)
    {
        _inspectFolder = inspectFolder;
        _generatePlaylist = generatePlaylist;
        _folderPicker = folderPicker;
        _dialogs = dialogs;
        PreviewItems.CollectionChanged += (_, _) => ClearCommand.NotifyCanExecuteChanged();
    }

    public ObservableCollection<string> PreviewItems { get; } = [];
    public int VideosFoundCount => HasVideos ? 1 : 0;

    partial void OnRootPathChanged(string value)
    {
        if (!_suppressRootInspection) _ = SelectFolderAsync(value);
    }
    partial void OnHasVideosChanged(bool value) => OnPropertyChanged(nameof(VideosFoundCount));
    partial void OnIsBusyChanged(bool value)
    {
        IsProgressIndeterminate = value;
        ProgressVisibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task BrowseAsync()
    {
        var path = _folderPicker.SelectFolder();
        if (!string.IsNullOrWhiteSpace(path)) await SelectFolderAsync(path);
    }
    private bool CanBrowse() => !IsBusy;

    public async Task SelectFolderAsync(string path)
    {
        var version = Interlocked.Increment(ref _inspectionVersion);
        _inspectionCancellation?.Cancel();
        _inspectionCancellation?.Dispose();
        _inspectionCancellation = new CancellationTokenSource();
        var token = _inspectionCancellation.Token;
        SetRootPathWithoutInspection(path);
        HasVideos = IsOutputExists = OverwriteConfirmed = false;
        Status = "Scanning...";

        try
        {
            var result = await _inspectFolder.ExecuteAsync(path, token);
            if (version != _inspectionVersion) return;
            ApplyInspection(result);
        }
        catch (OperationCanceledException)
        {
            if (version == _inspectionVersion) Status = "Scan cancelled";
        }
        catch (Exception exception)
        {
            if (version != _inspectionVersion) return;
            Status = "Error";
            _dialogs.ShowError(exception.Message);
        }
    }

    private void ApplyInspection(FolderInspectionResult result)
    {
        SetRootPathWithoutInspection(result.RootPath);
        HasVideos = result.HasVideos;
        IsOutputExists = result.ExistingOutput is not null;
        OverwriteConfirmed = !IsOutputExists;
        if (result.ExistingOutput is { } metadata)
        {
            OutputInfo = $"⚠️ Playlist already exists! (created: {metadata.LastWriteTime:yyyy-MM-dd HH:mm:ss}, size: {metadata.Length:N0} bytes)";
            OverwriteConfirmed = _dialogs.ConfirmOverwrite(System.IO.Path.Combine(result.RootPath, "Playlist.mpcpl"));
        }
        else OutputInfo = $"(will be saved as \"Playlist.mpcpl\" in: {result.RootPath})";

        Status = result.Status switch
        {
            FolderInspectionStatus.Ready when IsOutputExists && !OverwriteConfirmed => "Overwrite not confirmed",
            FolderInspectionStatus.Ready => "Ready",
            FolderInspectionStatus.NoVideos => "No video files found",
            FolderInspectionStatus.InvalidFolder => "Invalid folder",
            _ => "Error"
        };
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage)) _dialogs.ShowError(result.ErrorMessage);
    }

    private void SetRootPathWithoutInspection(string value)
    {
        _suppressRootInspection = true;
        RootPath = value;
        _suppressRootInspection = false;
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        _generationCancellation?.Cancel();
        _generationCancellation?.Dispose();
        _generationCancellation = new CancellationTokenSource();
        var token = _generationCancellation.Token;
        IsBusy = true;
        Status = "Scanning...";
        PreviewItems.Clear();
        Counts = string.Empty;
        try
        {
            var result = await _generatePlaylist.ExecuteAsync(
                SelectedRequest(), token);
            if (result.Status != PlaylistGenerationStatus.Success)
            {
                Status = "Error";
                _dialogs.ShowError(result.ErrorMessage ?? "Playlist generation failed.");
                return;
            }
            foreach (var entry in result.Entries.Take(200))
            {
                PreviewItems.Add(entry.VideoPath);
                foreach (var subtitle in entry.SubtitlePaths) PreviewItems.Add("  ↳ " + subtitle);
            }
            Status = "Done";
            Counts = $"Videos: {result.Entries.Count} (preview shows first 200). Output: {result.OutputPath}";
            OutputInfo = $"✅ Playlist created successfully! ({result.OutputPath})";
        }
        catch (OperationCanceledException) { Status = "Cancelled"; }
        catch (Exception exception) { Status = "Error"; _dialogs.ShowError(exception.Message); }
        finally { IsBusy = false; }
    }
    private bool CanGenerate() => !IsBusy && !string.IsNullOrWhiteSpace(RootPath) && HasVideos && (!IsOutputExists || OverwriteConfirmed);

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear() { PreviewItems.Clear(); Counts = string.Empty; Status = "Preview cleared"; }
    private bool CanClear() => !IsBusy && PreviewItems.Count > 0;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _inspectionCancellation?.Cancel();
        _generationCancellation?.Cancel();
        Status = "Cancelling...";
        IsBusy = false;
    }
    private bool CanCancel() => IsBusy;
    private GeneratePlaylistRequest SelectedRequest() => IsLongPath
        ? GeneratePlaylistRequest.Long(RootPath)
        : IsFullPath ? GeneratePlaylistRequest.Full(RootPath) : GeneratePlaylistRequest.Relative(RootPath);
}
