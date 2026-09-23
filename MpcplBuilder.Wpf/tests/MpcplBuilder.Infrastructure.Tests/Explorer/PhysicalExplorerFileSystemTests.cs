using MpcplBuilder.Application.Explorer;
using MpcplBuilder.Infrastructure.Explorer;

namespace MpcplBuilder.Infrastructure.Tests.Explorer;

public sealed class PhysicalExplorerFileSystemTests
{
    [Fact]
    public async Task GetChildFoldersAsync_ReturnsOnlyImmediateDirectories()
    {
        using var temp = new TemporaryDirectory();
        var child = Directory.CreateDirectory(temp.PathFor("Child")).FullName;
        Directory.CreateDirectory(temp.PathFor("Child/Grandchild"));
        temp.CreateFile("video.mkv");

        var result = await new PhysicalExplorerFileSystem()
            .GetChildFoldersAsync(temp.Path, CancellationToken.None);

        result.Paths.Should().Equal(child);
        result.AccessWarning.Should().BeNull();
    }

    [Fact]
    public async Task HasPlaylistAsync_IgnoresDescendantPlaylist()
    {
        using var temp = new TemporaryDirectory();
        temp.CreateFile("Child/Playlist.mpcpl");
        var fileSystem = new PhysicalExplorerFileSystem();

        (await fileSystem.HasPlaylistAsync(temp.Path, CancellationToken.None)).Should().BeFalse();
        (await fileSystem.HasPlaylistAsync(temp.PathFor("Child"), CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task GetChildFoldersAsync_StopsWhenCanceledDuringEnumeration()
    {
        using var cancellation = new CancellationTokenSource();
        IEnumerable<string> Enumerate(string _)
        {
            yield return "first";
            cancellation.Cancel();
            yield return "second";
        }

        var fileSystem = new PhysicalExplorerFileSystem(
            () => [], Enumerate);

        var action = () => fileSystem.GetChildFoldersAsync("root", cancellation.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetDrivesAsync_IncludesReadySupportedTypesAndExcludesUnreadyDrives()
    {
        var fileSystem = new PhysicalExplorerFileSystem(
            () =>
            [
                new TestDrive(DriveType.Fixed, true, @"C:\"),
                new TestDrive(DriveType.Removable, true, @"D:\"),
                new TestDrive(DriveType.Network, true, @"E:\"),
                new TestDrive(DriveType.Ram, true, @"F:\"),
                new TestDrive(DriveType.Fixed, false, @"G:\"),
                new TestDrive(DriveType.CDRom, true, @"H:\")
            ],
            _ => []);

        (await fileSystem.GetDrivesAsync(CancellationToken.None)).Should().Equal(
            @"C:\", @"D:\", @"E:\", @"F:\");
    }

    [Fact]
    public async Task GetDrivesAsync_ReflectsDriveRemovalBetweenCalls()
    {
        var present = true;
        var fileSystem = new PhysicalExplorerFileSystem(
            () => present ? [new TestDrive(DriveType.Removable, true, @"R:\")] : [],
            _ => []);

        (await fileSystem.GetDrivesAsync(CancellationToken.None)).Should().Equal(@"R:\");
        present = false;
        (await fileSystem.GetDrivesAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetDrivesAsync_SkipsDriveThatFailsInspection()
    {
        var fileSystem = new PhysicalExplorerFileSystem(
            () =>
            [
                new TestDrive(DriveType.Removable, () => throw new DriveNotFoundException("removed"), @"R:\"),
                new TestDrive(DriveType.Fixed, true, @"C:\")
            ],
            _ => []);

        (await fileSystem.GetDrivesAsync(CancellationToken.None)).Should().Equal(@"C:\");
    }

    [Fact]
    public async Task GetChildFoldersAsync_PropagatesInaccessibleRoot()
    {
        var fileSystem = new PhysicalExplorerFileSystem(
            () => [],
            _ => throw new UnauthorizedAccessException("denied"));

        var action = () => fileSystem.GetChildFoldersAsync("root", CancellationToken.None);
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LoadChildren_ExpandingInaccessibleChildDoesNotDiscardItsSiblings()
    {
        const string parent = @"C:\Media";
        const string accessible = @"C:\Media\Accessible";
        const string inaccessible = @"C:\Media\Inaccessible";

        IEnumerable<string> Enumerate(string path) => path switch
        {
            parent => [accessible, inaccessible],
            inaccessible => throw new UnauthorizedAccessException("Child access denied"),
            _ => []
        };

        var fileSystem = new PhysicalExplorerFileSystem(() => [], Enumerate);
        var loadChildren = new LoadExplorerChildren(fileSystem);

        var parentResult = await loadChildren.ExecuteAsync(parent, CancellationToken.None);
        parentResult.Status.Should().Be(ExplorerLoadStatus.Success);
        parentResult.Folders.Select(folder => folder.Path).Should().Equal(accessible, inaccessible);

        var inaccessibleResult = await loadChildren.ExecuteAsync(inaccessible, CancellationToken.None);
        inaccessibleResult.Status.Should().Be(ExplorerLoadStatus.AccessFailure);
        inaccessibleResult.Folders.Should().BeEmpty();
        inaccessibleResult.ErrorMessage.Should().Contain("Child access denied");
        parentResult.Folders.Select(folder => folder.Path).Should().Equal(accessible, inaccessible);
    }

    [Fact]
    public async Task GetChildFoldersAsync_ReturnsPartialResultWhenParentEnumerationStopsMidStream()
    {
        IEnumerable<string> Enumerate(string _)
        {
            yield return "first";
            throw new UnauthorizedAccessException("denied");
        }

        var fileSystem = new PhysicalExplorerFileSystem(
            () => [], Enumerate);

        var result = await fileSystem.GetChildFoldersAsync("root", CancellationToken.None);

        result.Paths.Should().Equal("first");
        result.AccessWarning.Should().Contain("denied");
    }

    [Fact]
    public async Task GetChildFoldersAsync_PropagatesMissingDriveAfterFirstChild()
    {
        IEnumerable<string> Enumerate(string _)
        {
            yield return "first";
            throw new DriveNotFoundException("removed");
        }

        var fileSystem = new PhysicalExplorerFileSystem(() => [], Enumerate);

        var action = () => fileSystem.GetChildFoldersAsync("root", CancellationToken.None);
        await action.Should().ThrowAsync<DriveNotFoundException>();
    }

    private sealed class TestDrive : IExplorerDriveSnapshot
    {
        private readonly Func<bool> _isReady;

        public TestDrive(DriveType type, bool isReady, string rootPath)
            : this(type, () => isReady, rootPath)
        {
        }

        public TestDrive(DriveType type, Func<bool> isReady, string rootPath)
        {
            Type = type;
            _isReady = isReady;
            RootPath = rootPath;
        }

        public DriveType Type { get; }
        public bool IsReady => _isReady();
        public string RootPath { get; }
    }
}
