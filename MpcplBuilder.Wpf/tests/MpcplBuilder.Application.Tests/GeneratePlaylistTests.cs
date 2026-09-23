using MpcplBuilder.Application.Playlists;
using MpcplBuilder.Application.Tests.Fakes;
using MpcplBuilder.Domain.Playlists;

namespace MpcplBuilder.Application.Tests;

public sealed class GeneratePlaylistTests
{
    [Fact]
    public async Task ExecuteAsync_SortsVideosAndSubtitlesBeforeWriting()
    {
        var media = new FakeMediaFileRepository
        {
            Videos = [@"D:\Media\Zulu.mkv", @"D:\Media\Alpha.mkv"]
        };
        media.Subtitles[@"D:\Media\Alpha.mkv"] =
            [@"D:\Media\Alpha.id.srt", @"D:\Media\Alpha.en.srt"];
        var output = new FakePlaylistOutput();
        var useCase = new GeneratePlaylist(media, output);

        var result = await useCase.ExecuteAsync(
            new GeneratePlaylistRequest(@"D:\Media", PathMode.Relative),
            CancellationToken.None);

        result.Status.Should().Be(PlaylistGenerationStatus.Success);
        var writtenEntries = output.WrittenEntries
            ?? throw new Xunit.Sdk.XunitException("Expected entries to be written.");
        writtenEntries.Select(entry => entry.VideoPath)
            .Should().Equal(@"D:\Media\Alpha.mkv", @"D:\Media\Zulu.mkv");
        writtenEntries[0].SubtitlePaths
            .Should().Equal(@"D:\Media\Alpha.en.srt", @"D:\Media\Alpha.id.srt");
        output.WrittenPathMode.Should().Be(PathMode.Relative);
    }

    [Fact]
    public async Task ExecuteAsync_NoVideos_DoesNotWrite()
    {
        var media = new FakeMediaFileRepository { Videos = [] };
        var output = new FakePlaylistOutput();

        var result = await new GeneratePlaylist(media, output).ExecuteAsync(
            new GeneratePlaylistRequest(@"D:\Media", PathMode.Full),
            CancellationToken.None);

        result.Status.Should().Be(PlaylistGenerationStatus.NoVideos);
        output.WriteCalls.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingOutputWithoutApproval_DoesNotWrite()
    {
        var media = new FakeMediaFileRepository { Videos = [@"D:\Media\Movie.mkv"] };
        var output = new FakePlaylistOutput { Metadata = new(DateTime.Now, 42) };

        var result = await new GeneratePlaylist(media, output).ExecuteAsync(
            GeneratePlaylistRequest.Relative(@"D:\Media"), CancellationToken.None);

        result.Status.Should().Be(PlaylistGenerationStatus.OverwriteRequired);
        output.WriteCalls.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_OutputFailure_ReturnsOutputFailure()
    {
        var media = new FakeMediaFileRepository { Videos = [@"D:\Media\Movie.mkv"] };
        var output = new FakePlaylistOutput { ExceptionToThrow = new IOException("locked") };

        var result = await new GeneratePlaylist(media, output).ExecuteAsync(
            new GeneratePlaylistRequest(@"D:\Media", PathMode.Full),
            CancellationToken.None);

        result.Status.Should().Be(PlaylistGenerationStatus.OutputFailure);
        result.ErrorMessage.Should().Contain("locked");
    }

    [Fact]
    public async Task ExecuteAsync_Cancelled_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var act = () => new GeneratePlaylist(new FakeMediaFileRepository(), new FakePlaylistOutput())
            .ExecuteAsync(new GeneratePlaylistRequest(@"D:\Media", PathMode.Full), cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
