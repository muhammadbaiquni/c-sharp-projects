namespace MpcplBuilder.Infrastructure.Explorer;

internal sealed class DriveCatalog(Func<DriveInfo[]> getDrives)
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
                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable or DriveType.Network or DriveType.Ram) ||
                    !drive.IsReady)
                    continue;

                roots.Add(Path.GetFullPath(drive.RootDirectory.FullName));
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
