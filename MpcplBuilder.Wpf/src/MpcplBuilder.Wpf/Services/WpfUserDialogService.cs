using System.Windows;
namespace MpcplBuilder.Wpf.Services;
public sealed class WpfUserDialogService : IUserDialogService
{
    public bool ConfirmOverwrite(string outputPath) => System.Windows.MessageBox.Show(
        $"A playlist file already exists:\n{outputPath}\n\nDo you want to overwrite it?", "Confirm Overwrite",
        MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    public bool ConfirmBatchOverwrite(string rootPath, int playlistCount)
    {
        var folderLabel = playlistCount == 1 ? "folder" : "folders";
        return System.Windows.MessageBox.Show(
            $"Playlist files already exist in {playlistCount} first-level {folderLabel} under:\n{rootPath}\n\n" +
            "Do you want to overwrite all existing playlists?\n\n" +
            "Choose No to keep them and create only missing playlists.",
            "Confirm Batch Overwrite", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }
    public void ShowError(string message) => System.Windows.MessageBox.Show(
        message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
}
