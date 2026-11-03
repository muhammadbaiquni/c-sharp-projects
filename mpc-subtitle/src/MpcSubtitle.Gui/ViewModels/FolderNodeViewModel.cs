using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using MpcSubtitle.Core;

namespace MpcSubtitle.Gui.ViewModels;

/// <summary>A lazily-loaded folder node in the explorer tree.</summary>
public sealed partial class FolderNodeViewModel : ObservableObject
{
    public string Name { get; }
    public string? FullPath { get; }
    public SubtitleCoverage Coverage { get; }
    public ObservableCollection<FolderNodeViewModel> Children { get; } = [];

    [ObservableProperty]
    private bool isExpanded;

    [ObservableProperty]
    private bool isChecked;

    private bool _loaded;
    private bool _loadQueued;

    private FolderNodeViewModel(string name, string? fullPath)
    {
        Name = name;
        FullPath = fullPath;
        Coverage = fullPath is null ? SubtitleCoverage.None : SubtitleCoverageAnalyzer.Compute(fullPath);
    }

    public static FolderNodeViewModel ForDrive(string drive)
    {
        var node = new FolderNodeViewModel(DriveLabel(drive), drive);
        node.Children.Add(new FolderNodeViewModel("…", null));
        return node;
    }

    public static FolderNodeViewModel ForDirectory(string fullPath)
    {
        var name = Path.GetFileName(fullPath);
        var node = new FolderNodeViewModel(string.IsNullOrEmpty(name) ? fullPath : name, fullPath);
        node.Children.Add(new FolderNodeViewModel("…", null));
        return node;
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value || _loadQueued)
        {
            return;
        }

        // Load after the current layout pass so we never mutate the ItemsSource
        // (Children) while the TreeViewItem is generating its containers.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            EnsureLoaded(); // no dispatcher (unit tests)
            return;
        }

        _loadQueued = true;
        dispatcher.BeginInvoke(new Action(() =>
        {
            _loadQueued = false;
            EnsureLoaded();
        }));
    }

    private void EnsureLoaded()
    {
        if (_loaded || FullPath is not { } path)
        {
            return;
        }

        _loaded = true;
        Children.Clear(); // drop the placeholder

        foreach (var directory in FolderExplorer.ListSubdirectories(path))
        {
            Children.Add(ForDirectory(directory));
        }
    }

    private static string DriveLabel(string drive)
    {
        var label = drive.TrimEnd(Path.DirectorySeparatorChar);
        try
        {
            var info = new DriveInfo(drive);
            if (info.IsReady && !string.IsNullOrWhiteSpace(info.VolumeLabel))
            {
                label = $"{label} ({info.VolumeLabel})";
            }
        }
        catch
        {
            // Drive not ready; fall back to the bare letter.
        }

        return label;
    }
}
