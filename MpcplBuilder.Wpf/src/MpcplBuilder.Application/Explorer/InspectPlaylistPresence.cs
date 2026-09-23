namespace MpcplBuilder.Application.Explorer;

public sealed class InspectPlaylistPresence(IExplorerFileSystem fileSystem) : IInspectPlaylistPresence
{
    public Task<bool> ExecuteAsync(string path, CancellationToken cancellationToken) =>
        fileSystem.HasPlaylistAsync(path, cancellationToken);
}
