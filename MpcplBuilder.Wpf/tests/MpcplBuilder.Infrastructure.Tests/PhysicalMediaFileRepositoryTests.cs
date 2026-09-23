using MpcplBuilder.Infrastructure.Files;

namespace MpcplBuilder.Infrastructure.Tests;

public sealed class PhysicalMediaFileRepositoryTests
{
    [Fact]
    public async Task GetVideosAsync_RecursesAndFiltersCaseInsensitively()
    {
        using var temp = new TemporaryDirectory();
        var first = temp.CreateFile("Alpha.MKV");
        var second = temp.CreateFile("nested/Beta.mp4");
        temp.CreateFile("nested/readme.md");
        var repository = new PhysicalMediaFileRepository();

        var videos = await repository.GetVideosAsync(temp.Path, CancellationToken.None);

        videos.Should().BeEquivalentTo(first, second);
    }

    [Fact]
    public async Task GetMatchingSubtitlesAsync_ReturnsOnlyMatchesFromVideoDirectory()
    {
        using var temp = new TemporaryDirectory();
        var video = temp.CreateFile("nested/Movie.mkv");
        var exact = temp.CreateFile("nested/Movie.srt");
        var language = temp.CreateFile("nested/Movie.en.ass");
        temp.CreateFile("nested/MovieTrailer.srt");
        temp.CreateFile("Other/Movie.id.srt");
        var repository = new PhysicalMediaFileRepository();

        var subtitles = await repository.GetMatchingSubtitlesAsync(video, CancellationToken.None);

        subtitles.Should().BeEquivalentTo(exact, language);
    }

    [Fact]
    public void EnumerateFiles_UnauthorizedChild_SkipsChildAndReturnsAccessibleSibling()
    {
        var root = @"D:\Root";
        var allowed = @"D:\Root\Allowed";
        var denied = @"D:\Root\Denied";
        var expected = @"D:\Root\Allowed\Movie.mkv";
        var traversal = new DirectoryTraversal(
            path => path == root ? [allowed, denied] : [],
            path => path == denied
                ? throw new UnauthorizedAccessException("denied")
                : path == allowed ? [expected] : []);

        var files = traversal.EnumerateFiles(root, CancellationToken.None);

        files.Should().ContainSingle().Which.Should().Be(expected);
    }

    [Fact]
    public async Task GetVideosAsync_Cancelled_Throws()
    {
        using var temp = new TemporaryDirectory();
        temp.CreateFile("Movie.mkv");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = () => new PhysicalMediaFileRepository().GetVideosAsync(temp.Path, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
