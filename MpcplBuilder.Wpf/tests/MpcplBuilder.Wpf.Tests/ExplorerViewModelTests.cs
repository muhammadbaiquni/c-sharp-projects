using FluentAssertions;
using MpcplBuilder.Application.Explorer;
using MpcplBuilder.Application.Playlists;
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
    public async Task Expand_WhenSelectedFolderDisappeared_DisablesGenerateAfterLoadSettles()
    {
        var children = new FakeLoadExplorerChildren
        {
            Handler = (_, _) => Task.FromResult(new ExplorerLoadResult(ExplorerLoadStatus.Missing, [], "Drive was removed"))
        };
        var vm = Create(children: children);
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.SelectedFolder = vm.Roots.Single().Children.Single();
        vm.GenerateCommand.CanExecute(null).Should().BeTrue();
        var eligibility = new List<bool>();
        vm.GenerateCommand.CanExecuteChanged += (_, _) => eligibility.Add(vm.GenerateCommand.CanExecute(null));

        await vm.SelectedFolder.ExpandAsync();

        vm.IsBusy.Should().BeFalse();
        vm.GenerateCommand.CanExecute(null).Should().BeFalse();
        eligibility.Should().NotBeEmpty().And.OnlyContain(enabled => !enabled);
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

    [Fact]
    public async Task Generate_WhenSuccessful_RefreshesOriginalNodeWithoutReplacingSelectionOrExpansion()
    {
        var inspections = 0;
        var presence = new FakeInspectPlaylistPresence
        {
            Handler = (_, _) => Task.FromResult(++inspections == 2)
        };
        var vm = Create(presence: presence);
        await vm.InitializeCommand.ExecuteAsync(null);
        var node = vm.Roots.Single().Children.Single();
        await node.ExpandAsync();
        vm.SelectedFolder = node;

        await vm.GenerateCommand.ExecuteAsync(null);

        inspections.Should().Be(2);
        vm.SelectedFolder.Should().BeSameAs(node);
        node.IsExpanded.Should().BeTrue();
        node.HasPlaylist.Should().BeTrue();
        vm.Status.Should().Be("Done");
    }

    [Fact]
    public async Task Generate_WhenPlaylistAppearsAfterSelection_DecliningOverwriteSkipsGeneration()
    {
        var generator = new FakeGeneratePlaylist();
        var dialogs = new FakeUserDialogService { ConfirmOverwriteResult = false };
        var presence = new FakeInspectPlaylistPresence { Handler = (_, _) => Task.FromResult(true) };
        var vm = Create(generate: generator, presence: presence, dialogs: dialogs);
        await vm.InitializeCommand.ExecuteAsync(null);
        var node = vm.Roots.Single().Children.Single();
        vm.SelectedFolder = node;
        node.HasPlaylist.Should().BeFalse();

        await vm.GenerateCommand.ExecuteAsync(null);

        dialogs.ConfirmOverwriteCalls.Should().Be(1);
        generator.Calls.Should().Be(0);
        node.HasPlaylist.Should().BeFalse();
        vm.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task Generate_WhenPlaylistAppearsAfterPresenceCheck_ConfirmsBeforeRetry()
    {
        var requests = new List<GeneratePlaylistRequest>();
        var generator = new SequenceGenerator(request =>
        {
            requests.Add(request);
            return requests.Count == 1
                ? new PlaylistGenerationResult(PlaylistGenerationStatus.OverwriteRequired, null, [], null)
                : new PlaylistGenerationResult(PlaylistGenerationStatus.Success, @"D:\Playlist.mpcpl", [], null);
        });
        var inspections = 0;
        var presence = new FakeInspectPlaylistPresence { Handler = (_, _) => Task.FromResult(++inspections == 2) };
        var dialogs = new FakeUserDialogService { ConfirmOverwriteResult = true };
        var vm = new ExplorerViewModel(Roots(), new FakeLoadExplorerChildren(), presence, generator, dialogs);
        await vm.InitializeCommand.ExecuteAsync(null);
        var node = vm.Roots.Single().Children.Single();
        vm.SelectedFolder = node;

        await vm.GenerateCommand.ExecuteAsync(null);

        requests.Select(r => r.OverwriteExisting).Should().Equal(false, true);
        dialogs.ConfirmOverwriteCalls.Should().Be(1);
        node.HasPlaylist.Should().BeTrue();
    }

    [Fact]
    public async Task Generate_WhenCancelledDuringOverwritePrompt_DoesNotStartGeneration()
    {
        var generator = new FakeGeneratePlaylist();
        var dialogs = new FakeUserDialogService { ConfirmOverwriteResult = true };
        var presence = new FakeInspectPlaylistPresence { Handler = (_, _) => Task.FromResult(true) };
        var vm = Create(generate: generator, presence: presence, dialogs: dialogs);
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.SelectedFolder = vm.Roots.Single().Children.Single();
        dialogs.OnConfirmOverwrite = () => vm.CancelCommand.Execute(null);

        await vm.GenerateCommand.ExecuteAsync(null);

        generator.Calls.Should().Be(0);
        vm.Status.Should().Be("Cancelled");
        vm.SelectedFolder.HasPlaylist.Should().BeFalse();
    }

    [Theory]
    [InlineData(PathMode.Full)]
    [InlineData(PathMode.Long)]
    public async Task Generate_UsesActivePathMode(PathMode pathMode)
    {
        var generator = new FakeGeneratePlaylist();
        var vm = Create(generate: generator);
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.SelectedFolder = vm.Roots.Single().Children.Single();
        vm.IsRelativePath = false;
        vm.IsFullPath = pathMode == PathMode.Full;
        vm.IsLongPath = pathMode == PathMode.Long;

        await vm.GenerateCommand.ExecuteAsync(null);

        generator.LastRequest!.PathMode.Should().Be(pathMode);
        generator.LastRequest.RootPath.Should().Be(@"D:\");
    }

    [Fact]
    public async Task Generate_SelectedNestedFolderDelegatesItsPathToRecursiveGenerator()
    {
        var generator = new FakeGeneratePlaylist();
        var children = new FakeLoadExplorerChildren { Handler = (_, _) => Task.FromResult(
            Success(new ExplorerFolder(@"D:\Series", "Series", false, true))) };
        var vm = Create(children: children, generate: generator);
        await vm.InitializeCommand.ExecuteAsync(null);
        var drive = vm.Roots.Single().Children.Single();
        await drive.ExpandAsync();
        vm.SelectedFolder = drive.Children.Single();

        await vm.GenerateCommand.ExecuteAsync(null);

        generator.Calls.Should().Be(1);
        generator.LastRequest!.RootPath.Should().Be(@"D:\Series");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generate_WhenDirectPlaylistExists_RequiresConsentAndUsesOverwriteFlag(bool initiallyGreen)
    {
        var roots = new FakeLoadExplorerRoots { Handler = _ => Task.FromResult(
            Success(new ExplorerFolder(@"D:\", "Drive", initiallyGreen, true))) };
        var generator = new FakeGeneratePlaylist();
        var dialogs = new FakeUserDialogService { ConfirmOverwriteResult = true };
        var presence = new FakeInspectPlaylistPresence { Handler = (_, _) => Task.FromResult(true) };
        var vm = Create(roots: roots, generate: generator, presence: presence, dialogs: dialogs);
        await vm.InitializeCommand.ExecuteAsync(null);
        var node = vm.Roots.Single().Children.Single();
        vm.SelectedFolder = node;

        await vm.GenerateCommand.ExecuteAsync(null);

        dialogs.ConfirmOverwriteCalls.Should().Be(1);
        generator.LastRequest!.OverwriteExisting.Should().BeTrue();
        node.HasPlaylist.Should().BeTrue();
        vm.SelectedFolder.Should().BeSameAs(node);
    }

    [Theory]
    [InlineData(PlaylistGenerationStatus.NoVideos)]
    [InlineData(PlaylistGenerationStatus.AccessFailure)]
    [InlineData(PlaylistGenerationStatus.OutputFailure)]
    public async Task Generate_WhenGenerationFails_ShowsOneErrorAndLeavesSelectedNodeYellow(PlaylistGenerationStatus failure)
    {
        var generator = new FakeGeneratePlaylist { Response = Task.FromResult(
            new PlaylistGenerationResult(failure, null, [], "generation error")) };
        var inspections = 0;
        var presence = new FakeInspectPlaylistPresence { Handler = (_, _) =>
        {
            inspections++;
            return Task.FromResult(false);
        } };
        var dialogs = new FakeUserDialogService();
        var vm = Create(generate: generator, presence: presence, dialogs: dialogs);
        await vm.InitializeCommand.ExecuteAsync(null);
        var node = vm.Roots.Single().Children.Single();
        vm.SelectedFolder = node;

        await vm.GenerateCommand.ExecuteAsync(null);

        inspections.Should().Be(1);
        node.HasPlaylist.Should().BeFalse();
        vm.Status.Should().Be("generation error");
        dialogs.Errors.Should().Equal("generation error");
    }

    [Theory]
    [InlineData("presence")]
    [InlineData("generation")]
    [InlineData("postwrite presence")]
    public async Task Generate_WhenCurrentOperationThrows_ShowsOneError(string failureSource)
    {
        var generator = new FakeGeneratePlaylist();
        if (failureSource == "generation")
            generator.Response = Task.FromException<PlaylistGenerationResult>(new IOException("current failure"));
        var inspections = 0;
        var presence = new FakeInspectPlaylistPresence
        {
            Handler = (_, _) => ++inspections == 1 && failureSource == "presence"
                || inspections == 2 && failureSource == "postwrite presence"
                ? Task.FromException<bool>(new IOException("current failure")) : Task.FromResult(false)
        };
        var dialogs = new FakeUserDialogService();
        var vm = Create(generate: generator, presence: presence, dialogs: dialogs);
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.SelectedFolder = vm.Roots.Single().Children.Single();

        await vm.GenerateCommand.ExecuteAsync(null);

        dialogs.Errors.Should().Equal("current failure");
        vm.Status.Should().Be("current failure");
        vm.SelectedFolder.HasPlaylist.Should().BeFalse();
        vm.IsBusy.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Generate_WhenFailureIsStale_DoesNotShowError(bool cancel, bool throws)
    {
        var pending = new TaskCompletionSource<PlaylistGenerationResult>();
        var generator = new FakeGeneratePlaylist { Response = pending.Task };
        var dialogs = new FakeUserDialogService();
        var roots = new FakeLoadExplorerRoots { Handler = _ => Task.FromResult(Success(
            new ExplorerFolder(@"D:\", "D", false, true), new ExplorerFolder(@"E:\", "E", false, true))) };
        var vm = Create(roots: roots, generate: generator, dialogs: dialogs);
        await vm.InitializeCommand.ExecuteAsync(null);
        var original = vm.Roots.Single().Children.First();
        vm.SelectedFolder = original;
        var generation = vm.GenerateCommand.ExecuteAsync(null);
        if (cancel) vm.CancelCommand.Execute(null);
        else vm.SelectedFolder = vm.Roots.Single().Children.Last();

        if (throws) pending.SetException(new IOException("stale failure"));
        else pending.SetResult(new(PlaylistGenerationStatus.OutputFailure, null, [], "stale failure"));
        await generation;

        dialogs.Errors.Should().BeEmpty();
        vm.Status.Should().Be(cancel ? "Cancelled" : "Ready");
        original.HasPlaylist.Should().BeFalse();
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Generate_WhenCancelledBeforeCompletion_IgnoresLateSuccess()
    {
        var pending = new TaskCompletionSource<PlaylistGenerationResult>();
        var generator = new FakeGeneratePlaylist { Response = pending.Task };
        var roots = Roots();
        var vm = Create(roots: roots, generate: generator);
        await vm.InitializeCommand.ExecuteAsync(null);
        var node = vm.Roots.Single().Children.Single();
        vm.SelectedFolder = node;
        var generation = vm.GenerateCommand.ExecuteAsync(null);
        vm.CancelCommand.Execute(null);
        vm.IsBusy.Should().BeTrue("cancellation is only a request until the generator settles");
        vm.RefreshCommand.CanExecute(null).Should().BeFalse();
        vm.GenerateCommand.CanExecute(null).Should().BeFalse();
        vm.Status.Should().Be("Cancelling...");
        await vm.RefreshAsync();
        roots.Tokens.Should().ContainSingle("refresh must not race a generator that is still running");
        vm.SelectedFolder.Should().BeSameAs(node);
        pending.SetResult(new PlaylistGenerationResult(PlaylistGenerationStatus.Success, @"D:\Playlist.mpcpl", [], null));
        await generation;

        generator.LastToken.IsCancellationRequested.Should().BeTrue();
        node.HasPlaylist.Should().BeFalse();
        vm.Status.Should().Be("Cancelled");
        vm.IsBusy.Should().BeFalse();
        vm.RefreshCommand.CanExecute(null).Should().BeTrue();
        vm.GenerateCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Generate_WhenSelectionChangesDuringGeneration_DoesNotUpdateOldNode()
    {
        var pending = new TaskCompletionSource<PlaylistGenerationResult>();
        var generator = new FakeGeneratePlaylist { Response = pending.Task };
        var roots = new FakeLoadExplorerRoots { Handler = _ => Task.FromResult(Success(
            new ExplorerFolder(@"D:\", "D", false, true), new ExplorerFolder(@"E:\", "E", false, true))) };
        var vm = Create(roots: roots, generate: generator);
        await vm.InitializeCommand.ExecuteAsync(null);
        var first = vm.Roots.Single().Children.First();
        var second = vm.Roots.Single().Children.Last();
        vm.SelectedFolder = first;
        var generation = vm.GenerateCommand.ExecuteAsync(null);
        vm.SelectedFolder = second;
        pending.SetResult(new PlaylistGenerationResult(PlaylistGenerationStatus.Success, @"D:\Playlist.mpcpl", [], null));
        await generation;

        first.HasPlaylist.Should().BeFalse();
        vm.SelectedFolder.Should().BeSameAs(second);
        vm.IsBusy.Should().BeFalse();
        vm.Status.Should().Be("Ready");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generate_WhenSelectionChangesAroundCancellation_SettlesNeutralStatus(bool cancelBeforeSelection)
    {
        var pending = new TaskCompletionSource<PlaylistGenerationResult>();
        var generator = new FakeGeneratePlaylist { Response = pending.Task };
        var roots = new FakeLoadExplorerRoots { Handler = _ => Task.FromResult(Success(
            new ExplorerFolder(@"D:\", "D", false, true), new ExplorerFolder(@"E:\", "E", false, true))) };
        var vm = Create(roots: roots, generate: generator);
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.SelectedFolder = vm.Roots.Single().Children.First();
        var generation = vm.GenerateCommand.ExecuteAsync(null);
        if (cancelBeforeSelection) vm.CancelCommand.Execute(null);
        vm.SelectedFolder = vm.Roots.Single().Children.Last();
        if (!cancelBeforeSelection) vm.CancelCommand.Execute(null);

        pending.SetException(new OperationCanceledException());
        await generation;

        vm.SelectedFolder.FullPath.Should().Be(@"E:\");
        vm.SelectedFolder.HasPlaylist.Should().BeFalse();
        vm.IsBusy.Should().BeFalse();
        vm.Status.Should().Be("Ready");
    }

    [Fact]
    public async Task Generate_WhenSelectionChangesBeforeLateCancellation_DoesNotShowStaleStatus()
    {
        var pending = new TaskCompletionSource<PlaylistGenerationResult>();
        var generator = new FakeGeneratePlaylist { Response = pending.Task };
        var roots = new FakeLoadExplorerRoots { Handler = _ => Task.FromResult(Success(
            new ExplorerFolder(@"D:\", "D", false, true), new ExplorerFolder(@"E:\", "E", false, true))) };
        var vm = Create(roots: roots, generate: generator);
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.SelectedFolder = vm.Roots.Single().Children.First();
        var generation = vm.GenerateCommand.ExecuteAsync(null);
        vm.SelectedFolder = vm.Roots.Single().Children.Last();
        vm.Status = "New selection";

        pending.SetException(new OperationCanceledException());
        await generation;

        vm.Status.Should().Be("New selection");
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Generate_WhenSelectionChangesBeforeLatePresenceException_DoesNotShowStaleError()
    {
        var pending = new TaskCompletionSource<bool>();
        var presence = new FakeInspectPlaylistPresence { Handler = (_, _) => pending.Task };
        var generator = new FakeGeneratePlaylist();
        var dialogs = new FakeUserDialogService();
        var roots = new FakeLoadExplorerRoots { Handler = _ => Task.FromResult(Success(
            new ExplorerFolder(@"D:\", "D", false, true), new ExplorerFolder(@"E:\", "E", false, true))) };
        var vm = Create(roots: roots, generate: generator, presence: presence, dialogs: dialogs);
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.SelectedFolder = vm.Roots.Single().Children.First();
        var generation = vm.GenerateCommand.ExecuteAsync(null);
        vm.SelectedFolder = vm.Roots.Single().Children.Last();
        vm.Status = "New selection";

        pending.SetException(new IOException("old folder failed"));
        await generation;

        generator.Calls.Should().Be(0);
        vm.Status.Should().Be("New selection");
        dialogs.Errors.Should().BeEmpty();
        vm.IsBusy.Should().BeFalse();
    }

    private static FakeLoadExplorerRoots Roots() => new() { Handler = _ => Task.FromResult(Success(new ExplorerFolder(@"D:\", "Drive", false, true))) };
    private static ExplorerViewModel Create(FakeLoadExplorerRoots? roots = null, FakeLoadExplorerChildren? children = null,
        FakeGeneratePlaylist? generate = null, FakeInspectPlaylistPresence? presence = null, FakeUserDialogService? dialogs = null) =>
        new(roots ?? Roots(), children ?? new(), presence ?? new FakeInspectPlaylistPresence(), generate ?? new(), dialogs ?? new());

    private sealed class SequenceGenerator(Func<GeneratePlaylistRequest, PlaylistGenerationResult> respond) : IGeneratePlaylist
    {
        public Task<PlaylistGenerationResult> ExecuteAsync(GeneratePlaylistRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
