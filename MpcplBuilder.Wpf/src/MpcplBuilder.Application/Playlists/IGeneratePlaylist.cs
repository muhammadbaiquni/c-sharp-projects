namespace MpcplBuilder.Application.Playlists;

public interface IGeneratePlaylist
{
    Task<PlaylistGenerationResult> ExecuteAsync(
        GeneratePlaylistRequest request,
        CancellationToken cancellationToken);
}
