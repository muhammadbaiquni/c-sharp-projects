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
        var viewModel = CreateMainWindowViewModel(new WindowsFolderPicker(), new WpfUserDialogService());
        MainWindow = new MainWindow { DataContext = viewModel };
        MainWindow.Show();
    }

    internal static MainWindowViewModel CreateMainWindowViewModel(IFolderPicker folderPicker, IUserDialogService dialogs)
    {
        var mediaFiles = new PhysicalMediaFileRepository();
        var output = new MpcplPlaylistOutput();
        var generatePlaylist = new GeneratePlaylist(mediaFiles, output);
        var defaultViewModel = new DefaultViewModel(
            new InspectFolder(mediaFiles, output),
            generatePlaylist,
            folderPicker,
            dialogs);
        var explorerFileSystem = new PhysicalExplorerFileSystem();
        var explorerViewModel = new ExplorerViewModel(
            new LoadExplorerRoots(explorerFileSystem),
            new LoadExplorerChildren(explorerFileSystem),
            new InspectPlaylistPresence(explorerFileSystem),
            generatePlaylist,
            dialogs);
        return new MainWindowViewModel(defaultViewModel, explorerViewModel);
    }
}
