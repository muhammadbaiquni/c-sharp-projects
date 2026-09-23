using MpcplBuilder.Application.Explorer;
using MpcplBuilder.Application.Tests.Fakes;

namespace MpcplBuilder.Application.Tests.Explorer;

public sealed class ExplorerUseCaseTests
{
    [Fact]
    public async Task LoadRoots_SortsDriveRootsAndIncludesDirectPlaylistPresence()
    {
        var fileSystem = new FakeExplorerFileSystem
        {
            Drives = [@"Z:\", @"C:\"],
            PlaylistFolders = { @"Z:\" }
        };

        var result = await new LoadExplorerRoots(fileSystem).ExecuteAsync(CancellationToken.None);

        result.Status.Should().Be(ExplorerLoadStatus.Success);
        result.ErrorMessage.Should().BeNull();
        result.Folders.Select(x => (x.Path, x.DisplayName, x.HasPlaylist, x.IsAvailable)).Should().Equal(
            (@"C:\", @"C:\", false, true),
            (@"Z:\", @"Z:\", true, true));
    }

    [Fact]
    public async Task LoadChildren_ReturnsOnlyImmediateFoldersSortedByDisplayName()
    {
        var fileSystem = new FakeExplorerFileSystem
        {
            Children = { [@"D:\Media"] = [@"D:\Media\Zulu", @"D:\Media\alpha"] }
        };

        var result = await new LoadExplorerChildren(fileSystem)
            .ExecuteAsync(@"D:\Media", CancellationToken.None);

        result.Status.Should().Be(ExplorerLoadStatus.Success);
        result.Folders.Select(x => (x.Path, x.DisplayName, x.IsAvailable)).Should().Equal(
            (@"D:\Media\alpha", "alpha", true),
            (@"D:\Media\Zulu", "Zulu", true));
    }

    [Fact]
    public async Task LoadChildren_MapsDirectPlaylistPresenceForEachFolder()
    {
        var fileSystem = new FakeExplorerFileSystem
        {
            Children = { [@"D:\Media"] = [@"D:\Media\A", @"D:\Media\B"] },
            PlaylistFolders = { @"D:\Media\B" }
        };

        var result = await new LoadExplorerChildren(fileSystem)
            .ExecuteAsync(@"D:\Media", CancellationToken.None);

        result.Folders.Select(x => (x.Path, x.HasPlaylist)).Should().Equal(
            (@"D:\Media\A", false), (@"D:\Media\B", true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InspectPlaylistPresence_ReturnsDirectStatus(bool hasPlaylist)
    {
        var fileSystem = new FakeExplorerFileSystem();
        if (hasPlaylist)
            fileSystem.PlaylistFolders.Add(@"D:\Media");

        var result = await new InspectPlaylistPresence(fileSystem)
            .ExecuteAsync(@"D:\Media", CancellationToken.None);

        result.Should().Be(hasPlaylist);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Load_MissingPath_ReturnsMissingWithNoFolders(bool loadRoots)
    {
        var fileSystem = new FakeExplorerFileSystem
        {
            ExceptionToThrow = loadRoots
                ? new DriveNotFoundException("drive missing")
                : new DirectoryNotFoundException("folder missing")
        };

        var result = loadRoots
            ? await new LoadExplorerRoots(fileSystem).ExecuteAsync(CancellationToken.None)
            : await new LoadExplorerChildren(fileSystem).ExecuteAsync(@"D:\Missing", CancellationToken.None);

        result.Status.Should().Be(ExplorerLoadStatus.Missing);
        result.Folders.Should().BeEmpty();
        result.ErrorMessage.Should().Contain("missing");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Load_AccessFailure_ReturnsAccessFailureWithNoFolders(bool loadRoots)
    {
        var fileSystem = new FakeExplorerFileSystem
        {
            ExceptionToThrow = loadRoots
                ? new UnauthorizedAccessException("denied")
                : new IOException("offline")
        };

        var result = loadRoots
            ? await new LoadExplorerRoots(fileSystem).ExecuteAsync(CancellationToken.None)
            : await new LoadExplorerChildren(fileSystem).ExecuteAsync(@"D:\Media", CancellationToken.None);

        result.Status.Should().Be(ExplorerLoadStatus.AccessFailure);
        result.Folders.Should().BeEmpty();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LoadRoots_Cancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var act = () => new LoadExplorerRoots(new FakeExplorerFileSystem())
            .ExecuteAsync(cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LoadChildren_Cancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var act = () => new LoadExplorerChildren(new FakeExplorerFileSystem())
            .ExecuteAsync(@"D:\Media", cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task InspectPlaylistPresence_Cancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var act = () => new InspectPlaylistPresence(new FakeExplorerFileSystem())
            .ExecuteAsync(@"D:\Media", cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LoadRoots_PreCancelledToken_PropagatesWhenPortIgnoresCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var fileSystem = new FakeExplorerFileSystem { IgnoreCancellation = true };

        var act = () => new LoadExplorerRoots(fileSystem).ExecuteAsync(cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LoadChildren_PreCancelledToken_PropagatesWhenPortIgnoresCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var fileSystem = new FakeExplorerFileSystem { IgnoreCancellation = true };

        var act = () => new LoadExplorerChildren(fileSystem)
            .ExecuteAsync(@"D:\Media", cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task InspectPlaylistPresence_PreCancelledToken_PropagatesWhenPortIgnoresCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var fileSystem = new FakeExplorerFileSystem { IgnoreCancellation = true };

        var act = () => new InspectPlaylistPresence(fileSystem)
            .ExecuteAsync(@"D:\Media", cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LoadRoots_CancelledDuringDriveLoad_PropagatesWithNoDrives()
    {
        using var cancellation = new CancellationTokenSource();
        var fileSystem = new FakeExplorerFileSystem
        {
            IgnoreCancellation = true,
            OnGetDrives = cancellation.Cancel
        };

        var act = () => new LoadExplorerRoots(fileSystem).ExecuteAsync(cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LoadChildren_CancelledDuringChildLoad_PropagatesWithNoChildren()
    {
        using var cancellation = new CancellationTokenSource();
        var fileSystem = new FakeExplorerFileSystem
        {
            IgnoreCancellation = true,
            OnGetChildFolders = cancellation.Cancel
        };

        var act = () => new LoadExplorerChildren(fileSystem)
            .ExecuteAsync(@"D:\Media", cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LoadRoots_CancelledDuringPlaylistInspection_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        var fileSystem = new FakeExplorerFileSystem
        {
            Drives = [@"D:\"],
            IgnoreCancellation = true,
            OnHasPlaylist = cancellation.Cancel
        };

        var act = () => new LoadExplorerRoots(fileSystem).ExecuteAsync(cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LoadChildren_CancelledDuringPlaylistInspection_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        var fileSystem = new FakeExplorerFileSystem
        {
            Children = { [@"D:\Media"] = [@"D:\Media\A"] },
            IgnoreCancellation = true,
            OnHasPlaylist = cancellation.Cancel
        };

        var act = () => new LoadExplorerChildren(fileSystem)
            .ExecuteAsync(@"D:\Media", cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task InspectPlaylistPresence_CancelledDuringPortCall_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        var fileSystem = new FakeExplorerFileSystem
        {
            IgnoreCancellation = true,
            OnHasPlaylist = cancellation.Cancel
        };

        var act = () => new InspectPlaylistPresence(fileSystem)
            .ExecuteAsync(@"D:\Media", cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Load_ResultFoldersCannotBeMutated(bool loadRoots)
    {
        var fileSystem = new FakeExplorerFileSystem
        {
            Drives = [@"D:\"],
            Children = { [@"D:\Media"] = [@"D:\Media\A"] }
        };
        var result = loadRoots
            ? await new LoadExplorerRoots(fileSystem).ExecuteAsync(CancellationToken.None)
            : await new LoadExplorerChildren(fileSystem).ExecuteAsync(@"D:\Media", CancellationToken.None);

        var act = () => ((IList<ExplorerFolder>)result.Folders)[0] =
            new ExplorerFolder(@"Z:\Other", "Other", false, true);

        act.Should().Throw<NotSupportedException>();
    }
}
