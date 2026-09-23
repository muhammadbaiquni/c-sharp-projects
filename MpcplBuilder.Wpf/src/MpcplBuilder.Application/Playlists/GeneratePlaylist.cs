using MpcplBuilder.Application.Abstractions;
using MpcplBuilder.Domain.Playlists;

namespace MpcplBuilder.Application.Playlists;

public sealed class GeneratePlaylist(
    IMediaFileRepository mediaFiles,
    IPlaylistOutput playlistOutput) : IGeneratePlaylist
{
    public async Task<PlaylistGenerationResult> ExecuteAsync(
        GeneratePlaylistRequest request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> videos;
        try
        {
            videos = await mediaFiles.GetVideosAsync(request.RootPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return new(PlaylistGenerationStatus.AccessFailure, null, [], exception.Message);
        }

        if (videos.Count == 0)
            return new(PlaylistGenerationStatus.NoVideos, null, [], "No supported video files were found.");

        var entries = new List<PlaylistEntry>(videos.Count);
        foreach (var video in videos.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var subtitles = await mediaFiles.GetMatchingSubtitlesAsync(video, cancellationToken);
            entries.Add(new PlaylistEntry(
                video,
                subtitles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()));
        }

        try
        {
            var outputPath = await playlistOutput.WriteAsync(
                request.RootPath,
                entries,
                request.PathMode,
                cancellationToken);
            return new(PlaylistGenerationStatus.Success, outputPath, entries, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            return new(PlaylistGenerationStatus.AccessFailure, null, entries, exception.Message);
        }
        catch (IOException exception)
        {
            return new(PlaylistGenerationStatus.OutputFailure, null, entries, exception.Message);
        }
    }
}
