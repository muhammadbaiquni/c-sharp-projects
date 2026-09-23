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

        result.Should().Equal(child);
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
    public async Task GetDrivesAsync_ReflectsDriveRemovalBetweenCalls()
    {
        var root = Path.GetPathRoot(Path.GetTempPath())!;
        var present = true;
        var fileSystem = new PhysicalExplorerFileSystem(
            () => present ? [new DriveInfo(root)] : [],
            _ => []);

        (await fileSystem.GetDrivesAsync(CancellationToken.None)).Should().ContainSingle().Which.Should().Be(root);
        present = false;
        (await fileSystem.GetDrivesAsync(CancellationToken.None)).Should().BeEmpty();
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
    public async Task GetChildFoldersAsync_PropagatesInaccessibleChild()
    {
        IEnumerable<string> Enumerate(string _)
        {
            yield return "first";
            throw new UnauthorizedAccessException("denied");
        }

        var fileSystem = new PhysicalExplorerFileSystem(
            () => [], Enumerate);

        var action = () => fileSystem.GetChildFoldersAsync("root", CancellationToken.None);
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
