namespace MpcplBuilder.Wpf.Services;
public sealed class WindowsFolderPicker : IFolderPicker
{
    public string? SelectFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select root folder to scan (recursive)",
            ShowNewFolderButton = false
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }
}
