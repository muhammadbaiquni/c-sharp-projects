namespace MpcplBuilder.Wpf.Services;
public interface IUserDialogService
{
    bool ConfirmOverwrite(string outputPath);
    void ShowError(string message);
}
