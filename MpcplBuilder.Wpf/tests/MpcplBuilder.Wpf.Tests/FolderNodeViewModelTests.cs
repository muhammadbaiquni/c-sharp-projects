using FluentAssertions;
using MpcplBuilder.Application.Explorer;
using MpcplBuilder.Wpf.Tests.Fakes;
using MpcplBuilder.Wpf.ViewModels;

namespace MpcplBuilder.Wpf.Tests;

public sealed class FolderNodeViewModelTests
{
    [Fact]
    public async Task Expand_WhilePending_SharesOneLoadAndReplacesPlaceholder()
    {
        var pending = new TaskCompletionSource<ExplorerLoadResult>();
        var loader = new FakeLoadExplorerChildren { Handler = (_, _) => pending.Task };
        var node = Create(loader);
        node.Children.Should().ContainSingle().Which.IsPlaceholder.Should().BeTrue();

        var first = node.ExpandAsync();
        var second = node.ExpandAsync();
        loader.Requests.Should().ContainSingle();
        node.IsLoading.Should().BeTrue();
        pending.SetResult(Success(new ExplorerFolder(@"D:\Media\Child", "Child", false, true)));
        await Task.WhenAll(first, second);

        node.Children.Should().ContainSingle().Which.FullPath.Should().Be(@"D:\Media\Child");
        node.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task Collapse_PreservesLoadedChildrenAndReopeningDoesNotReload()
    {
        var loader = new FakeLoadExplorerChildren { Handler = (_, _) => Task.FromResult(Success(new ExplorerFolder(@"D:\Media\Child", "Child", false, true))) };
        var node = Create(loader);
        await node.ExpandAsync();
        var child = node.Children.Single();
        node.IsExpanded = false;
        await node.ExpandAsync();
        node.Children.Single().Should().BeSameAs(child);
        loader.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task AccessFailure_RemainsRetryableAndShowsAccessibleError()
    {
        var loader = new FakeLoadExplorerChildren { Handler = (_, _) => Task.FromResult(new ExplorerLoadResult(ExplorerLoadStatus.AccessFailure, [], "Access denied")) };
        var node = Create(loader);
        await node.ExpandAsync();
        node.Status.Should().Contain("Access denied");
        node.Children.Should().ContainSingle().Which.IsPlaceholder.Should().BeTrue();
        loader.Handler = (_, _) => Task.FromResult(Success());
        await node.ExpandAsync();
        node.Children.Should().BeEmpty();
        loader.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Expand_WhenFolderDisappeared_MarksUnavailableAndNotifiesGenerationEligibility()
    {
        var loader = new FakeLoadExplorerChildren
        {
            Handler = (_, _) => Task.FromResult(new ExplorerLoadResult(ExplorerLoadStatus.Missing, [], "Folder was removed"))
        };
        var node = Create(loader);
        var changed = new List<string?>();
        node.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        await node.ExpandAsync();

        node.IsAvailable.Should().BeFalse();
        node.CanGenerate.Should().BeFalse();
        node.Children.Should().BeEmpty();
        node.Status.Should().Be("Folder was removed");
        changed.Should().Contain(nameof(FolderNodeViewModel.IsAvailable)).And.Contain(nameof(FolderNodeViewModel.CanGenerate));
    }

    [Fact]
    public async Task PartialAccess_KeepsUsableChildrenAndWarning()
    {
        var loader = new FakeLoadExplorerChildren { Handler = (_, _) => Task.FromResult(new ExplorerLoadResult(
            ExplorerLoadStatus.PartialAccess, [new(@"D:\Media\Visible", "Visible", true, true)], "Some folders could not be read")) };
        var node = Create(loader);
        await node.ExpandAsync();
        node.Children.Should().ContainSingle().Which.FullPath.Should().Be(@"D:\Media\Visible");
        node.Status.Should().Contain("Some folders could not be read");
        node.Children.Single().IconKey.Should().Be("GreenFolderIcon");
    }

    [Theory]
    [InlineData(true, "GreenFolderIcon", "Playlist present")]
    [InlineData(false, "YellowFolderIcon", "No playlist")]
    public void Presence_IsDirectAndExposedAsText(bool hasPlaylist, string icon, string text)
    {
        var node = new FolderNodeViewModel(new(@"D:\Media", "Media", hasPlaylist, true), new FakeLoadExplorerChildren());
        node.IconKey.Should().Be(icon);
        node.PlaylistStatus.Should().Be(text);
    }

    [Fact]
    public async Task CancelLoad_DiscardsLateResultsAndAllowsRetry()
    {
        var pending = new TaskCompletionSource<ExplorerLoadResult>();
        var loader = new FakeLoadExplorerChildren { Handler = (_, _) => pending.Task };
        var node = Create(loader);
        var oldLoad = node.ExpandAsync();
        node.CancelLoads();
        loader.Requests.Single().Token.IsCancellationRequested.Should().BeTrue();
        loader.Handler = (_, _) => Task.FromResult(Success(new ExplorerFolder(@"D:\Media\New", "New", false, true)));
        await node.ExpandAsync();
        pending.SetResult(Success(new ExplorerFolder(@"D:\Media\Old", "Old", false, true)));
        await oldLoad;
        node.Children.Single().DisplayName.Should().Be("New");
    }

    private static FolderNodeViewModel Create(FakeLoadExplorerChildren loader) => new(new(@"D:\Media", "Media", false, true), loader);
    internal static ExplorerLoadResult Success(params ExplorerFolder[] folders) => new(ExplorerLoadStatus.Success, folders, null);
}
