using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Threading;

namespace MpcplBuilder.Wpf;

public class MainWindowViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly Func<string, bool> _videoFileChecker;

    private string _rootPath = string.Empty;
    public string RootPath
    {
        get => _rootPath;
        set
        {
            SetRootPathValue(value);

            // If the user entered or pasted an existing folder path, start a quick recursive scan
            // (no overwrite prompt when scanning from typed/pasted input)
            try
            {
                if (!string.IsNullOrWhiteSpace(_rootPath) && Directory.Exists(_rootPath))
                {
                    // Start async update but do not await (UI remains responsive)
                    _ = UpdateRootStateAsync(_rootPath, promptForOverwrite: false);
                }
            }
            catch { }
        }
    }

    private string _outputInfo = string.Empty;
    public string OutputInfo
    {
        get => _outputInfo;
        set { _outputInfo = value; OnPropertyChanged(nameof(OutputInfo)); }
    }

    private int _videosFoundCount;
    public int VideosFoundCount
    {
        get => _videosFoundCount;
        private set { _videosFoundCount = value; OnPropertyChanged(nameof(VideosFoundCount)); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); }
    }

    private bool _isOutputExists;
    public bool IsOutputExists
    {
        get => _isOutputExists;
        private set { _isOutputExists = value; OnPropertyChanged(nameof(IsOutputExists)); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); }
    }

    private bool _overwriteConfirmed;
    public bool OverwriteConfirmed
    {
        get => _overwriteConfirmed;
        set { _overwriteConfirmed = value; OnPropertyChanged(nameof(OverwriteConfirmed)); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); }
    }

    private string _status = "Ready";
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); }
    }

    private string _counts = string.Empty;
    public string Counts
    {
        get => _counts;
        set { _counts = value; OnPropertyChanged(nameof(Counts)); }
    }

    public ObservableCollection<string> PreviewItems { get; } = new ObservableCollection<string>();

    private Visibility _progressVisibility = Visibility.Collapsed;
    public Visibility ProgressVisibility
    {
        get => _progressVisibility;
        set { _progressVisibility = value; OnPropertyChanged(nameof(ProgressVisibility)); }
    }

    private bool _isProgressIndeterminate;
    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        set
        {
            _isProgressIndeterminate = value;
            OnPropertyChanged(nameof(IsProgressIndeterminate));
            // update command availability when progress state changes
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    // Path mode booleans for RadioButton bindings
    private bool _isRelativePath = true;
    public bool IsRelativePath
    {
        get => _isRelativePath;
        set { _isRelativePath = value; OnPropertyChanged(nameof(IsRelativePath)); }
    }

    private bool _isFullPath;
    public bool IsFullPath
    {
        get => _isFullPath;
        set { _isFullPath = value; OnPropertyChanged(nameof(IsFullPath)); }
    }

    private bool _isLongPath;
    public bool IsLongPath
    {
        get => _isLongPath;
        set { _isLongPath = value; OnPropertyChanged(nameof(IsLongPath)); }
    }

    public ICommand BrowseCommand { get; }
    public ICommand GenerateCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand CancelCommand { get; }

    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _generationCts;

    public MainWindowViewModel() : this(HasAnyVideoFile)
    {
    }

    internal MainWindowViewModel(Func<string, bool> videoFileChecker)
    {
        _videoFileChecker = videoFileChecker ?? throw new ArgumentNullException(nameof(videoFileChecker));
        BrowseCommand = new AsyncRelayCommand(_ => { Browse(); return Task.CompletedTask; }, _ => !IsProgressIndeterminate);
        GenerateCommand = new AsyncRelayCommand(async _ => await GenerateAsync(), _ => !string.IsNullOrWhiteSpace(RootPath) && Directory.Exists(RootPath) && VideosFoundCount > 0 && !IsProgressIndeterminate && (!IsOutputExists || OverwriteConfirmed));
        ClearCommand = new AsyncRelayCommand(_ => { Clear(); return Task.CompletedTask; }, _ => PreviewItems.Count > 0 && !IsProgressIndeterminate);
        CancelCommand = new AsyncRelayCommand(_ => { CancelActiveOperations(); return Task.CompletedTask; }, _ => IsProgressIndeterminate);

        // Requery commands when the preview collection changes (Count affects ClearCommand CanExecute)
        PreviewItems.CollectionChanged += (_, __) => System.Windows.Input.CommandManager.InvalidateRequerySuggested();

        Status = "Ready";
        OutputInfo = "(will be saved as \"Playlist.mpcpl\" in the root)";
    }

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void Browse()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select root folder to scan (recursive)",
            ShowNewFolderButton = false
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _ = SetRootPathFromBrowseAsync(dlg.SelectedPath);
        }
    }

    private void SetRootPathValue(string path)
    {
        _rootPath = path;
        OnPropertyChanged(nameof(RootPath));
        CommandManager.InvalidateRequerySuggested();
    }

    internal async Task SetRootPathFromBrowseAsync(string path)
    {
        SetRootPathValue(path);
        await UpdateRootStateAsync(path, promptForOverwrite: true);
    }

    private async Task UpdateRootStateAsync(string path, bool promptForOverwrite)
    {
        // Cancel any previous quick-scan
        try
        {
            _scanCts?.Cancel();
            _scanCts?.Dispose();
        }
        catch { }
        // We no longer do a cancellable full quick-scan. Instead run a short-circuit
        // background check that returns true on first video found.
        _scanCts = null;

        try
        {
            var hasAny = await Task.Run(() => _videoFileChecker(path));
            VideosFoundCount = hasAny ? 1 : 0; // keep existing int property semantics (0 = none, >0 = some)

            var playlistPath = Path.Combine(path, "Playlist.mpcpl");
            IsOutputExists = File.Exists(playlistPath);

            if (IsOutputExists)
            {
                var fileInfo = new FileInfo(playlistPath);
                OutputInfo = $"⚠️ Playlist already exists! (created: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}, size: {fileInfo.Length:N0} bytes)";
                Status = "Warning: Existing playlist detected";

                if (promptForOverwrite)
                {
                    var res = System.Windows.MessageBox.Show($"A playlist file already exists:\n{playlistPath}\n\nPre-confirm overwrite so Generate is enabled?", "Pre-confirm Overwrite", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    OverwriteConfirmed = (res == MessageBoxResult.Yes);
                }
                else
                {
                    OverwriteConfirmed = false;
                }
            }
            else
            {
                OutputInfo = $"(will be saved as \"Playlist.mpcpl\" in: {path})";
                Status = "Ready";
                OverwriteConfirmed = false;
            }
        }
        catch (OperationCanceledException)
        {
            // scan cancelled
            Status = "Scan cancelled";
        }
        catch
        {
            // If scanning fails, treat as no videos found but don't crash
            VideosFoundCount = 0;
            IsOutputExists = false;
            OutputInfo = "(will be saved as \"Playlist.mpcpl\" in the root)";
            Status = "Ready";
            OverwriteConfirmed = false;
        }
        finally
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    // Lightweight safe file counter for video extensions to avoid the heavier BuildEntries work
    // Lightweight short-circuit check: return true as soon as any video file is found.
    // This avoids a full count and the cancellation complexity from the old quick-scan.
    private static bool HasAnyVideoFile(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            IEnumerable<string> subDirs = Array.Empty<string>();
            IEnumerable<string> files = Array.Empty<string>();

            try { subDirs = Directory.EnumerateDirectories(current); } catch { }
            try { files = Directory.EnumerateFiles(current); } catch { }

            foreach (var f in files)
            {
                var ext = Path.GetExtension(f);
                if (PlaylistBuilder.VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    return true;
            }

            foreach (var d in subDirs)
                stack.Push(d);
        }

        return false;
    }

    private async Task GenerateAsync()
    {
        var root = RootPath?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            System.Windows.MessageBox.Show("Root folder is not valid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var output = Path.Combine(root, "Playlist.mpcpl");

        if (File.Exists(output))
        {
            var result = System.Windows.MessageBox.Show($"A playlist file already exists:\n{output}\n\nDo you want to overwrite the existing file?", "Confirm Overwrite", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                Status = "Cancelled";
                return;
            }
        }

        OutputInfo = $"Output: {output}";

        ProgressVisibility = Visibility.Visible;
        IsProgressIndeterminate = true;
        Status = "Scanning...";
        PreviewItems.Clear();
        Counts = string.Empty;

        // Cancel any in-progress quick-scan to free resources for the full generation
        try { _scanCts?.Cancel(); _scanCts?.Dispose(); _scanCts = null; } catch { }

        try
        {
            PlaylistBuilder.PathMode pathMode;
            if (IsRelativePath)
                pathMode = PlaylistBuilder.PathMode.Relative;
            else if (IsLongPath)
                pathMode = PlaylistBuilder.PathMode.LongPath;
            else
                pathMode = PlaylistBuilder.PathMode.FullPath;

            try { _generationCts?.Cancel(); _generationCts?.Dispose(); } catch { }
            _generationCts = new CancellationTokenSource();
            var genToken = _generationCts.Token;

            var result = await Task.Run(() =>
            {
                genToken.ThrowIfCancellationRequested();
                var entries = PlaylistBuilder.BuildEntries(root, genToken);
                entries.Sort((a, b) => string.Compare(a.VideoPath, b.VideoPath, StringComparison.OrdinalIgnoreCase));
                PlaylistBuilder.WriteMpcpl(output, entries, root, pathMode, genToken);
                return entries;
            }, genToken);

            Status = "Done";
            IsProgressIndeterminate = false;
            ProgressVisibility = Visibility.Collapsed;

            foreach (var eItem in result.Take(200))
            {
                PreviewItems.Add(eItem.VideoPath);
                foreach (var sub in eItem.SubtitlePaths)
                    PreviewItems.Add("  ↳ " + sub);
            }

            Counts = $"Videos: {result.Count} (preview shows first 200). Output: {output}";

            var fileInfo = new FileInfo(output);
            OutputInfo = $"✅ Playlist created successfully! ({fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}, size: {fileInfo.Length:N0} bytes)";
        }
        catch (OperationCanceledException)
        {
            IsProgressIndeterminate = false;
            ProgressVisibility = Visibility.Collapsed;
            Status = "Cancelled";
        }
        catch (Exception ex)
        {
            IsProgressIndeterminate = false;
            ProgressVisibility = Visibility.Collapsed;
            Status = "Error";
            System.Windows.MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            try { _generationCts?.Dispose(); _generationCts = null; } catch { }
        }
    }

    private void Clear()
    {
        PreviewItems.Clear();
        Counts = string.Empty;
        Status = "Preview cleared";
    }

    private void CancelActiveOperations()
    {
        // Cancel quick-scan
        try { _scanCts?.Cancel(); } catch { }

        // Cancel generation
        try { _generationCts?.Cancel(); } catch { }

        Status = "Cancelling...";
        IsProgressIndeterminate = false;
        ProgressVisibility = Visibility.Collapsed;
    }
}
