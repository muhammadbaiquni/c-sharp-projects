namespace MpcplBuilder.Application.Explorer;

public interface IInspectPlaylistPresence
{
    Task<bool> ExecuteAsync(string path, CancellationToken cancellationToken);
}
