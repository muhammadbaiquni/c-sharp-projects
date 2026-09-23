namespace MpcplBuilder.Infrastructure.Explorer;

internal sealed class DriveCatalog(Func<IEnumerable<IExplorerDriveSnapshot>> getDrives)
{
    public IReadOnlyList<string> GetReadyRoots(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var roots = new List<string>();

        foreach (var drive in getDrives())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (drive.Type is not (DriveType.Fixed or DriveType.Removable or DriveType.Network or DriveType.Ram) ||
                    !drive.IsReady)
                    continue;

                roots.Add(Path.GetFullPath(drive.RootPath));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DriveNotFoundException)
            {
                // A drive can disappear between discovery and inspection.
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return roots;
    }
}
