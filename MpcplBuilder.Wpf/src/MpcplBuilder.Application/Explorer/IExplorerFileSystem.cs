namespace MpcplBuilder.Application.Explorer;

public interface IExplorerFileSystem
{
    Task<IReadOnlyList<string>> GetDrivesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetChildFoldersAsync(string path, CancellationToken cancellationToken);
    Task<bool> HasPlaylistAsync(string path, CancellationToken cancellationToken);
}
