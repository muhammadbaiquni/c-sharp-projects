namespace MpcplBuilder.Wpf.Services;
public interface IUserDialogService
{
    bool ConfirmOverwrite(string outputPath);
    bool ConfirmBatchOverwrite(string rootPath, int playlistCount);
    void ShowError(string message);
}
