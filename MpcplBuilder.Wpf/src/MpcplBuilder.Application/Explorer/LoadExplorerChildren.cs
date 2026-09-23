namespace MpcplBuilder.Application.Explorer;

public sealed class LoadExplorerChildren(IExplorerFileSystem fileSystem) : ILoadExplorerChildren
{
    public async Task<ExplorerLoadResult> ExecuteAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var paths = await fileSystem.GetChildFoldersAsync(path, cancellationToken);
            var folders = new List<ExplorerFolder>(paths.Count);
            foreach (var childPath in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                folders.Add(new ExplorerFolder(
                    childPath,
                    Path.GetFileName(Path.TrimEndingDirectorySeparator(childPath)),
                    await fileSystem.HasPlaylistAsync(childPath, cancellationToken),
                    true));
            }

            return new(
                ExplorerLoadStatus.Success,
                folders.OrderBy(folder => folder.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                null);
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
