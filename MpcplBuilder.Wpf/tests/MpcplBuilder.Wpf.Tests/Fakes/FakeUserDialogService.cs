using MpcplBuilder.Wpf.Services;

namespace MpcplBuilder.Wpf.Tests.Fakes;

internal sealed class FakeUserDialogService : IUserDialogService
{
    public bool ConfirmOverwriteResult { get; set; }
    public int ConfirmOverwriteCalls { get; private set; }
    public Action? OnConfirmOverwrite { get; set; }
    public List<string> Errors { get; } = [];

    public bool ConfirmOverwrite(string outputPath)
    {
        ConfirmOverwriteCalls++;
        OnConfirmOverwrite?.Invoke();
        return ConfirmOverwriteResult;
    }

    public void ShowError(string message) => Errors.Add(message);
}
