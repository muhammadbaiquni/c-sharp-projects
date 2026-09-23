namespace MpcplBuilder.Application.Explorer;

public sealed class LoadExplorerRoots(IExplorerFileSystem fileSystem) : ILoadExplorerRoots
{
    public async Task<ExplorerLoadResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var paths = await fileSystem.GetDrivesAsync(cancellationToken);
            var folders = new List<ExplorerFolder>(paths.Count);
            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                folders.Add(new ExplorerFolder(
                    path,
                    path,
                    await fileSystem.HasPlaylistAsync(path, cancellationToken),
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
