using FluentAssertions.Execution;
using MpcplBuilder.Application.Abstractions;
using MpcplBuilder.Application.Playlists;
using MpcplBuilder.Infrastructure.Files;
using MpcplBuilder.Infrastructure.Playlists;

namespace MpcplBuilder.Infrastructure.Tests;

public sealed class GeneratePlaylistOutputSafetyTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOutputAppearsDuringDiscoveryWithoutApproval_PreservesFileAndRequiresOverwrite()
    {
        using var temp = new TemporaryDirectory();
        temp.CreateFile("Movie.mkv");
        var media = new PausedDiscoveryRepository();
        var generation = new GeneratePlaylist(media, new MpcplPlaylistOutput())
            .ExecuteAsync(GeneratePlaylistRequest.Relative(temp.Path), CancellationToken.None);
        await media.DiscoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var outputPath = temp.PathFor("Playlist.mpcpl");
        await File.WriteAllTextAsync(outputPath, "External playlist: keep me");

        media.ContinueDiscovery.SetResult();
        var result = await generation;

        using var assertions = new AssertionScope();
        File.ReadAllText(outputPath).Should().Be("External playlist: keep me");
        result.Status.Should().Be(PlaylistGenerationStatus.OverwriteRequired);
        result.OutputPath.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenOutputAppearsDuringDiscoveryWithApproval_ReplacesFile()
    {
        using var temp = new TemporaryDirectory();
        temp.CreateFile("Movie.mkv");
        var media = new PausedDiscoveryRepository();
        var generation = new GeneratePlaylist(media, new MpcplPlaylistOutput())
            .ExecuteAsync(GeneratePlaylistRequest.Relative(temp.Path, overwrite: true), CancellationToken.None);
        await media.DiscoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var outputPath = temp.PathFor("Playlist.mpcpl");
        await File.WriteAllTextAsync(outputPath, "External playlist");

        media.ContinueDiscovery.SetResult();
        var result = await generation;

        result.Status.Should().Be(PlaylistGenerationStatus.Success);
        result.OutputPath.Should().Be(outputPath);
        File.ReadAllText(outputPath).Should().Be("MPCPLAYLIST\r\n1,type,0\r\n1,filename,Movie.mkv\r\n");
    }

    private sealed class PausedDiscoveryRepository : IMediaFileRepository
    {
        private readonly PhysicalMediaFileRepository _inner = new();
        public TaskCompletionSource DiscoveryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ContinueDiscovery { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool DirectoryExists(string rootPath) => _inner.DirectoryExists(rootPath);
        public Task<bool> HasAnyVideoAsync(string rootPath, CancellationToken cancellationToken) =>
            _inner.HasAnyVideoAsync(rootPath, cancellationToken);

        public async Task<IReadOnlyList<string>> GetVideosAsync(string rootPath, CancellationToken cancellationToken)
        {
            DiscoveryStarted.SetResult();
            await ContinueDiscovery.Task.WaitAsync(cancellationToken);
            return await _inner.GetVideosAsync(rootPath, cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetMatchingSubtitlesAsync(string videoPath, CancellationToken cancellationToken) =>
            _inner.GetMatchingSubtitlesAsync(videoPath, cancellationToken);
    }
}
