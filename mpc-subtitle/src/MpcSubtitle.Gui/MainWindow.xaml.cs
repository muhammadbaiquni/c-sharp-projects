using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using MpcSubtitle.Core;
using MpcSubtitle.Gui.ViewModels;

namespace MpcSubtitle.Gui;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Func<string, string, ConflictDecision> resolver = (video, srt) =>
        {
            var dialog = new ConflictDialog(video, srt) { Owner = this };
            dialog.ShowDialog();
            return dialog.Decision;
        };

        var viewModel = new MainViewModel(resolver);
        DataContext = viewModel;

        // Keep the newest log line in view, deferred so we never touch the
        // ItemsControl's generated items while its change notification is in flight.
        viewModel.LogLines.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ScrollToEnd));
            }
        };
    }

    private void ScrollToEnd()
    {
        if (LogList.Items.Count > 0)
        {
            LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
        }
    }

    private void OnBrowseFfmpeg(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select ffmpeg.exe",
            Filter = "ffmpeg executable (ffmpeg.exe)|ffmpeg.exe|Executables (*.exe)|*.exe|All files (*.*)|*.*",
            FileName = "ffmpeg.exe",
            DefaultExt = "exe",
        };

        if (dialog.ShowDialog(this) == true && DataContext is MainViewModel viewModel)
        {
            viewModel.FfmpegPath = dialog.FileName;
        }
    }
}
