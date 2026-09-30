namespace MpcplBuilder.Wpf.Services;
public interface IUserDialogService
{
    bool ConfirmOverwrite(string outputPath);
    bool ConfirmBatchOverwrite(string selectionDescription, int playlistCount);
    void ShowError(string message);
}
