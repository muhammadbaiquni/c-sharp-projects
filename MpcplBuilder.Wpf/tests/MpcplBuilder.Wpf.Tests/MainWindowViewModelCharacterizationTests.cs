using System.Reflection;
using FluentAssertions;

namespace MpcplBuilder.Wpf.Tests;

public sealed class MainWindowViewModelCharacterizationTests
{
    [Fact]
    public async Task SelectingFolder_StartsExactlyOneVideoScan()
    {
        var scanCount = 0;
        Func<string, bool> scanner = _ =>
        {
            scanCount++;
            return false;
        };

        var constructor = typeof(MainWindowViewModel).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(Func<string, bool>)],
            modifiers: null);
        constructor.Should().NotBeNull();

        var viewModel = (MainWindowViewModel)constructor!.Invoke([scanner]);
        var selectFolder = typeof(MainWindowViewModel).GetMethod(
            "SetRootPathFromBrowseAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        selectFolder.Should().NotBeNull();

        using var temp = new TemporaryDirectory();
        var task = (Task?)selectFolder!.Invoke(viewModel, [temp.Path]);
        task.Should().NotBeNull();

        await task!;

        scanCount.Should().Be(1);
        viewModel.RootPath.Should().Be(temp.Path);
    }
}
