namespace MpcplBuilder.Application.Explorer;

public sealed class LoadExplorerChildren(IExplorerFileSystem fileSystem) : ILoadExplorerChildren
{
    public async Task<ExplorerLoadResult> ExecuteAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var childResult = await fileSystem.GetChildFoldersAsync(path, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var folders = new List<ExplorerFolder>(childResult.Paths.Count);
            foreach (var childPath in childResult.Paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var hasPlaylist = await fileSystem.HasPlaylistAsync(childPath, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                folders.Add(new ExplorerFolder(
                    childPath,
                    Path.GetFileName(Path.TrimEndingDirectorySeparator(childPath)),
                    hasPlaylist,
                    true));
            }

            return new(
                childResult.AccessWarning is null ? ExplorerLoadStatus.Success : ExplorerLoadStatus.PartialAccess,
                Array.AsReadOnly(folders.OrderBy(folder => folder.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase)
                    .ToArray()),
                childResult.AccessWarning);
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException or DriveNotFoundException)
        {
            return new(ExplorerLoadStatus.Missing, [], exception.Message);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return new(ExplorerLoadStatus.AccessFailure, [], exception.Message);
        }
    }
}
