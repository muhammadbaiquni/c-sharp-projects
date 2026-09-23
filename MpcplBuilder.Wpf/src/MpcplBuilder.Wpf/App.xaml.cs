using System.Windows;
using MpcplBuilder.Application.Explorer;
using MpcplBuilder.Application.Folders;
using MpcplBuilder.Application.Playlists;
using MpcplBuilder.Infrastructure.Files;
using MpcplBuilder.Infrastructure.Explorer;
using MpcplBuilder.Infrastructure.Playlists;
using MpcplBuilder.Wpf.Services;
using MpcplBuilder.Wpf.ViewModels;

namespace MpcplBuilder.Wpf;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var mediaFiles = new PhysicalMediaFileRepository();
        var output = new MpcplPlaylistOutput();
        var defaultViewModel = new DefaultViewModel(
            new InspectFolder(mediaFiles, output),
            new GeneratePlaylist(mediaFiles, output),
            new WindowsFolderPicker(),
            new WpfUserDialogService());
        var explorerFileSystem = new PhysicalExplorerFileSystem();
        var explorerViewModel = new ExplorerViewModel(
            new LoadExplorerRoots(explorerFileSystem),
            new LoadExplorerChildren(explorerFileSystem),
            new InspectPlaylistPresence(explorerFileSystem),
            new GeneratePlaylist(mediaFiles, output));
        var viewModel = new MainWindowViewModel(defaultViewModel, explorerViewModel);
        MainWindow = new MainWindow { DataContext = viewModel };
        MainWindow.Show();
    }
}
