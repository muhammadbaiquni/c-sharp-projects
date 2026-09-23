using System.Windows;
namespace MpcplBuilder.Wpf.Services;
public sealed class WpfUserDialogService : IUserDialogService
{
    public bool ConfirmOverwrite(string outputPath) => System.Windows.MessageBox.Show(
        $"A playlist file already exists:\n{outputPath}\n\nDo you want to overwrite it?", "Confirm Overwrite",
        MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    public void ShowError(string message) => System.Windows.MessageBox.Show(
        message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
}
