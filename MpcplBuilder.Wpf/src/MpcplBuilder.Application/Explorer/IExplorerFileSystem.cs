namespace MpcplBuilder.Application.Explorer;

public interface IExplorerFileSystem
{
    Task<IReadOnlyList<string>> GetDrivesAsync(CancellationToken cancellationToken);
    Task<ExplorerChildFoldersResult> GetChildFoldersAsync(string path, CancellationToken cancellationToken);
    Task<bool> HasPlaylistAsync(string path, CancellationToken cancellationToken);
}
