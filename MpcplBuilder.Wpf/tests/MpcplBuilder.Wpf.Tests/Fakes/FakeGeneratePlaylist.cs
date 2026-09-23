using MpcplBuilder.Application.Playlists;

namespace MpcplBuilder.Wpf.Tests.Fakes;

internal sealed class FakeGeneratePlaylist : IGeneratePlaylist
{
    public Task<PlaylistGenerationResult> Response { get; set; } = Task.FromResult(
        new PlaylistGenerationResult(PlaylistGenerationStatus.Success, "Playlist.mpcpl", [], null));
    public int Calls { get; private set; }
    public CancellationToken LastToken { get; private set; }

    public Task<PlaylistGenerationResult> ExecuteAsync(
        GeneratePlaylistRequest request,
        CancellationToken cancellationToken)
    {
        Calls++;
        LastToken = cancellationToken;
        return Response;
    }
}
