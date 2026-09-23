using System.Text;
using MpcplBuilder.Domain.Playlists;
using MpcplBuilder.Infrastructure.Playlists;

namespace MpcplBuilder.Infrastructure.Tests;

public sealed class MpcplPlaylistOutputTests
{
    [Fact]
    public async Task WriteAsync_WritesBomCrLfNumberingAndSubtitles()
    {
        using var temp = new TemporaryDirectory();
        var video1 = temp.CreateFile("Alpha.mkv");
        var subtitle = temp.CreateFile("Alpha.en.srt");
        var video2 = temp.CreateFile("Beta.mp4");
        var output = new MpcplPlaylistOutput();

        var path = await output.WriteAsync(
            temp.Path,
            [new PlaylistEntry(video1, [subtitle]), new PlaylistEntry(video2, [])],
            PathMode.Relative,
            overwriteExisting: false,
            CancellationToken.None);

        File.ReadAllText(path).Should().Be(
            "MPCPLAYLIST\r\n" +
            "1,type,0\r\n1,filename,Alpha.mkv\r\n1,subtitle,Alpha.en.srt\r\n" +
            "2,type,0\r\n2,filename,Beta.mp4\r\n");
        File.ReadAllBytes(path).Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
    }

    [Fact]
    public async Task GetMetadataAsync_ExistingOutput_ReturnsTimestampAndLength()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.PathFor("Playlist.mpcpl");
        await File.WriteAllTextAsync(path, "content", Encoding.UTF8);

        var metadata = await new MpcplPlaylistOutput().GetMetadataAsync(temp.Path, CancellationToken.None);

        metadata.Should().NotBeNull();
        metadata!.Length.Should().Be(new FileInfo(path).Length);
        metadata.LastWriteTime.Should().BeCloseTo(File.GetLastWriteTime(path), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task WriteAsync_Cancelled_ThrowsWithoutCreatingOutput()
    {
        using var temp = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var act = () => new MpcplPlaylistOutput().WriteAsync(
            temp.Path,
            [new PlaylistEntry(temp.PathFor("Movie.mkv"), [])],
            PathMode.Relative,
            overwriteExisting: false,
            cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(temp.PathFor("Playlist.mpcpl")).Should().BeFalse();
    }
}
