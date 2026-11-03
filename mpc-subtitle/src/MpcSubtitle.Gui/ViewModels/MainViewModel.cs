using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MpcSubtitle.Core;

namespace MpcSubtitle.Gui.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly Func<string, string, ConflictDecision> _resolveConflict;
    private readonly SynchronizationContext _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
    private CancellationTokenSource? _cts;

    public ObservableCollection<FolderNodeViewModel> Roots { get; } = [];
    public ObservableCollection<string> LogLines { get; } = [];
    public IReadOnlyList<string> ModelNames { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isRunning;

    [ObservableProperty]
    private string selectedModel = "small";

    [ObservableProperty]
    private string language = "auto";

    [ObservableProperty]
    private string ffmpegPath = string.Empty;

    [ObservableProperty]
    private bool splitFirstLevel;

    [ObservableProperty]
    private string statusText = "Ready.";

    [ObservableProperty]
    private double progress;

    public MainViewModel(Func<string, string, ConflictDecision> resolveConflict)
    {
        _resolveConflict = resolveConflict ?? throw new ArgumentNullException(nameof(resolveConflict));
        ModelNames = ModelResolver.KnownNames.ToList();

        foreach (var drive in Environment.GetLogicalDrives())
        {
            Roots.Add(FolderNodeViewModel.ForDrive(drive));
        }
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        var roots = GetCheckedRoots();
        if (roots.Count == 0)
        {
            StatusText = "Check at least one folder first.";
            return;
        }

        var ffmpeg = FfmpegLocator.Locate(string.IsNullOrWhiteSpace(FfmpegPath) ? null : FfmpegPath.Trim());
        if (ffmpeg is null)
        {
            StatusText = "ffmpeg not found. Put ffmpeg.exe on PATH or fill the ffmpeg field.";
            return;
        }

        _cts = new CancellationTokenSource();
        IsRunning = true;
        Progress = 0;
        LogLines.Clear();
        Log($"Model: {SelectedModel}, language: {(Language.Length == 0 ? "auto" : Language)}, roots: {roots.Count} folder(s).");

        try
        {
            var modelPath = await ModelResolver.ResolveAsync(SelectedModel, null, Log, _cts.Token);
            Log("Loading model...");
            using var session = await Task.Run(() => new TranscriptionSession(modelPath), _cts.Token);

            var scanner = new BatchScanner((video, srt, ct) => Task.Run(async () =>
            {
                await SubtitleGenerator.GenerateForFileAsync(video, srt, ffmpeg, session, Language, onSegment: null, ct);
            }, ct));

            var stats = await scanner.ScanAsync(
                roots,
                new BatchScanOptions { SplitFirstLevel = SplitFirstLevel },
                _resolveConflict,
                Log,
                UpdateStats,
                _cts.Token);

            Log($"Done: {stats.Total} video(s) — {stats.Created} created, {stats.Replaced} replaced, " +
                $"{stats.Skipped} skipped, {stats.Failed} failed.");
            UpdateStats(stats);
        }
        catch (OperationCanceledException)
        {
            Log("Cancelled by user.");
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
        }
        finally
        {
            IsRunning = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private bool CanGenerate() => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    private bool CanCancel() => IsRunning;

    private List<string> GetCheckedRoots()
    {
        var roots = new List<string>();
        foreach (var root in Roots)
        {
            CollectChecked(root, roots);
        }

        return roots;
    }

    private static void CollectChecked(FolderNodeViewModel node, List<string> roots)
    {
        if (node.IsChecked && node.FullPath is not null)
        {
            roots.Add(node.FullPath);
        }

        foreach (var child in node.Children)
        {
            CollectChecked(child, roots);
        }
    }

    private void UpdateStats(ScanStats stats)
    {
        Ui(() =>
        {
            Progress = stats.Total == 0 ? 0 : stats.Done * 100.0 / stats.Total;
            StatusText =
                $"Total {stats.Total} | done {stats.Done} | created {stats.Created} | replaced {stats.Replaced} | " +
                $"skipped {stats.Skipped} | failed {stats.Failed}"
                + (stats.Current is null ? string.Empty : " | " + Path.GetFileName(stats.Current));
        });
    }

    private void Log(string message)
    {
        Ui(() => LogLines.Add(message));
    }

    private void Ui(Action action)
    {
        if (SynchronizationContext.Current == _uiContext)
        {
            action();
        }
        else
        {
            _uiContext.Post(_ => action(), null);
        }
    }
}
