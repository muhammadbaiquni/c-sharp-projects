using System.Windows;
using MpcplBuilder.Application.Folders;
using MpcplBuilder.Application.Playlists;
using MpcplBuilder.Infrastructure.Files;
using MpcplBuilder.Infrastructure.Playlists;
using MpcplBuilder.Wpf.Services;

namespace MpcplBuilder.Wpf;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var mediaFiles = new PhysicalMediaFileRepository();
        var output = new MpcplPlaylistOutput();
        var viewModel = new MainWindowViewModel(
            new InspectFolder(mediaFiles, output),
            new GeneratePlaylist(mediaFiles, output),
            new WindowsFolderPicker(),
            new WpfUserDialogService());
        MainWindow = new MainWindow { DataContext = viewModel };
        MainWindow.Show();
    }
}
