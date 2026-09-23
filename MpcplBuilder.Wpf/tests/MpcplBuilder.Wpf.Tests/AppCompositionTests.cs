using FluentAssertions;
using MpcplBuilder.Wpf.Tests.Fakes;
using MpcplBuilder.Wpf.ViewModels;

namespace MpcplBuilder.Wpf.Tests;

public sealed class AppCompositionTests
{
    [Fact]
    public void StartupComposition_CreatesIndependentExplorerAndDefaultWorkflows()
    {
        var shell = App.CreateMainWindowViewModel(new FakeFolderPicker(), new FakeUserDialogService());

        shell.Explorer.Should().BeOfType<ExplorerViewModel>();
        shell.Default.Should().BeOfType<DefaultViewModel>();
        shell.SelectedTabIndex.Should().Be(1);
        shell.Explorer.IsRelativePath.Should().BeTrue();
        shell.Default.IsRelativePath.Should().BeTrue();

        shell.Explorer.Status = "Explorer is loading";
        shell.Default.Status.Should().Be("Ready");
        shell.Default.Status = "Default is scanning";
        shell.Explorer.Status.Should().Be("Explorer is loading");
    }
}
