using MpcplBuilder.Wpf.Services;

namespace MpcplBuilder.Wpf.Tests.Fakes;

internal sealed class FakeFolderPicker : IFolderPicker
{
    public string? SelectedPath { get; set; }
    public int Calls { get; private set; }

    public string? SelectFolder()
    {
        Calls++;
        return SelectedPath;
    }
}
