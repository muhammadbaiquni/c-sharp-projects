using FluentAssertions;
using MpcplBuilder.Application.Explorer;
using MpcplBuilder.Domain.Playlists;
using MpcplBuilder.Wpf.Tests.Fakes;
using MpcplBuilder.Wpf.ViewModels;
using static MpcplBuilder.Wpf.Tests.FolderNodeViewModelTests;

namespace MpcplBuilder.Wpf.Tests;

public sealed class ExplorerViewModelTests
{
    [Fact]
    public async Task Initialize_DefaultsToRelativeAndThisPcCannotGenerate()
    {
        var vm = Create();
        vm.IsRelativePath.Should().BeTrue();
        vm.GenerateCommand.CanExecute(null).Should().BeFalse();
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.Roots.Should().ContainSingle().Which.DisplayName.Should().Be("This PC");
        vm.SelectedFolder = vm.Roots.Single();
        vm.GenerateCommand.CanExecute(null).Should().BeFalse();
        vm.SelectedFolder = vm.Roots.Single().Children.Single();
        vm.GenerateCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Refresh_RestoresExpandedPathsAndSelectionCaseInsensitivelyWithoutLoadingOtherBranches()
    {
        var roots = Roots();
        var children = new FakeLoadExplorerChildren { Handler = (path, _) => Task.FromResult(
            path.Equals(@"D:\", StringComparison.OrdinalIgnoreCase)
                ? Success(new ExplorerFolder(@"D:\One", "Same", false, true), new(@"D:\Two", "Same", false, true))
                : Success(new ExplorerFolder(@"D:\Two\Deep", "Deep", false, true))) };
        var vm = Create(roots, children);
        await vm.InitializeCommand.ExecuteAsync(null);
        var drive = vm.Roots.Single().Children.Single();
        await drive.ExpandAsync();
        var second = drive.Children.Last();
        await second.ExpandAsync();
        vm.SelectedFolder = second.Children.Single();
        roots.Handler = _ => Task.FromResult(Success(new ExplorerFolder(@"d:\", "Drive", false, true)));
        children.Requests.Clear();

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.SelectedFolder!.FullPath.Should().Be(@"D:\Two\Deep");
        vm.SelectedFolder.Should().NotBeSameAs(second.Children.Single());
        var refreshed = vm.Roots.Single().Children.Single();
        refreshed.IsExpanded.Should().BeTrue();
        refreshed.Children.Last().IsExpanded.Should().BeTrue();
        refreshed.Children.First().IsExpanded.Should().BeFalse();
        children.Requests.Select(r => r.Path).Should().BeEquivalentTo(@"d:\", @"D:\Two");
    }

    [Fact]
    public async Task Refresh_RemovedDriveClearsSelection()
    {
        var roots = Roots();
        var vm = Create(roots);
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.SelectedFolder = vm.Roots.Single().Children.Single();
        roots.Handler = _ => Task.FromResult(Success());
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedFolder.Should().BeNull();
        vm.GenerateCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_CancelsOldNodesAndIgnoresTheirLateCompletion()
    {
        var pending = new TaskCompletionSource<ExplorerLoadResult>();
        var loader = new FakeLoadExplorerChildren { Handler = (_, _) => pending.Task };
        var vm = Create(children: loader);
        await vm.InitializeCommand.ExecuteAsync(null);
        var oldDrive = vm.Roots.Single().Children.Single();
        var oldLoad = oldDrive.ExpandAsync();
        loader.Handler = (_, _) => Task.FromResult(Success(new ExplorerFolder(@"D:\New", "New", false, true)));
        await vm.RefreshCommand.ExecuteAsync(null);
        pending.SetResult(Success(new ExplorerFolder(@"D:\Old", "Old", false, true)));
        await oldLoad;
        loader.Requests.First().Token.IsCancellationRequested.Should().BeTrue();
        oldDrive.Children.Should().NotContain(n => n.DisplayName == "Old");
        vm.Roots.Single().Children.Single().Children.Single().DisplayName.Should().Be("New");
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_PendingRootsIgnoresLateSuccess()
    {
        var pending = new TaskCompletionSource<ExplorerLoadResult>();
        var roots = new FakeLoadExplorerRoots { Handler = _ => pending.Task };
        var vm = Create(roots);
        var load = vm.InitializeCommand.ExecuteAsync(null);
        vm.CancelCommand.CanExecute(null).Should().BeTrue();
        vm.CancelCommand.Execute(null);
        pending.SetResult(Success(new ExplorerFolder(@"Z:\", "Late", false, true)));
        await load;
        roots.Tokens.Single().IsCancellationRequested.Should().BeTrue();
        vm.Roots.SelectMany(n => n.Children).Should().BeEmpty();
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Initialize_WhilePendingAndAfterSuccess_LoadsRootsOnce()
    {
        var pending = new TaskCompletionSource<ExplorerLoadResult>();
        var roots = new FakeLoadExplorerRoots { Handler = _ => pending.Task };
        var vm = Create(roots);
        var first = vm.InitializeCommand.ExecuteAsync(null);
        var second = vm.InitializeCommand.ExecuteAsync(null);
        roots.Tokens.Should().ContainSingle();
        pending.SetResult(Success(new ExplorerFolder(@"D:\", "Drive", false, true)));
        await Task.WhenAll(first, second);
        await vm.InitializeCommand.ExecuteAsync(null);
        roots.Tokens.Should().ContainSingle();
        vm.Roots.Single().Children.Single().FullPath.Should().Be(@"D:\");
    }

    [Fact]
    public async Task Refresh_WhenOlderRootsFinishLast_PreservesNewestTreeAndStatus()
    {
        var pending = new TaskCompletionSource<ExplorerLoadResult>();
        var roots = new FakeLoadExplorerRoots { Handler = _ => pending.Task };
        var vm = Create(roots);
        var oldLoad = vm.RefreshAsync();
        roots.Handler = _ => Task.FromResult(Success(new ExplorerFolder(@"E:\", "New drive", false, true)));
        await vm.RefreshAsync();
        pending.SetResult(new(ExplorerLoadStatus.AccessFailure, [], "Old error"));
        await oldLoad;
        vm.Roots.Single().Children.Single().FullPath.Should().Be(@"E:\");
        vm.Status.Should().Be("Ready");
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_PendingChildLoadDisablesGenerationAndPreventsLateChildren()
    {
        var pending = new TaskCompletionSource<ExplorerLoadResult>();
        var children = new FakeLoadExplorerChildren { Handler = (_, _) => pending.Task };
        var vm = Create(children: children);
        await vm.InitializeCommand.ExecuteAsync(null);
        var drive = vm.Roots.Single().Children.Single();
        vm.SelectedFolder = drive;
        var load = drive.ExpandAsync();
        vm.IsBusy.Should().BeTrue();
        vm.GenerateCommand.CanExecute(null).Should().BeFalse();
        vm.CancelCommand.Execute(null);
        pending.SetResult(Success(new ExplorerFolder(@"D:\Late", "Late", false, true)));
        await load;
        drive.Children.Should().NotContain(n => n.DisplayName == "Late");
        vm.IsBusy.Should().BeFalse();
        vm.GenerateCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Generate_UsesSelectedFullPathAndRelativeDefault()
    {
        var generator = new FakeGeneratePlaylist();
        var vm = Create(generate: generator);
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.SelectedFolder = vm.Roots.Single().Children.Single();
        await vm.GenerateCommand.ExecuteAsync(null);
        generator.LastRequest!.RootPath.Should().Be(@"D:\");
        generator.LastRequest.PathMode.Should().Be(PathMode.Relative);
        vm.IsBusy.Should().BeFalse();
    }

    private static FakeLoadExplorerRoots Roots() => new() { Handler = _ => Task.FromResult(Success(new ExplorerFolder(@"D:\", "Drive", false, true))) };
    private static ExplorerViewModel Create(FakeLoadExplorerRoots? roots = null, FakeLoadExplorerChildren? children = null,
        FakeGeneratePlaylist? generate = null) => new(roots ?? Roots(), children ?? new(), new FakeInspectPlaylistPresence(), generate ?? new());
}
