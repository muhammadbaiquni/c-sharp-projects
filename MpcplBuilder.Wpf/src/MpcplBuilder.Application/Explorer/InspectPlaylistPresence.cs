namespace MpcplBuilder.Application.Explorer;

public sealed class InspectPlaylistPresence(IExplorerFileSystem fileSystem) : IInspectPlaylistPresence
{
    public async Task<bool> ExecuteAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hasPlaylist = await fileSystem.HasPlaylistAsync(path, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return hasPlaylist;
    }
}
