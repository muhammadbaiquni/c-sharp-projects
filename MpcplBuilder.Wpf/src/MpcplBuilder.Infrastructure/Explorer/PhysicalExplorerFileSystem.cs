using MpcplBuilder.Application.Explorer;

namespace MpcplBuilder.Infrastructure.Explorer;

public sealed class PhysicalExplorerFileSystem : IExplorerFileSystem
{
    private readonly DriveCatalog _driveCatalog;
    private readonly Func<string, IEnumerable<string>> _enumerateDirectories;

    public PhysicalExplorerFileSystem()
        : this(
            () => DriveInfo.GetDrives().Select(drive => (IExplorerDriveSnapshot)new PhysicalExplorerDriveSnapshot(drive)),
            Directory.EnumerateDirectories)
    {
    }

    internal PhysicalExplorerFileSystem(
        Func<IEnumerable<IExplorerDriveSnapshot>> getDrives,
        Func<string, IEnumerable<string>> enumerateDirectories)
    {
        _driveCatalog = new DriveCatalog(getDrives);
        _enumerateDirectories = enumerateDirectories;
    }

    public Task<IReadOnlyList<string>> GetDrivesAsync(CancellationToken cancellationToken) =>
        Task.Run(() => _driveCatalog.GetReadyRoots(cancellationToken), cancellationToken);

    public Task<ExplorerChildFoldersResult> GetChildFoldersAsync(string path, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folders = new List<string>();
            try
            {
                // Listing a parent yields child paths; expanding a child calls this method again.
                foreach (var folder in _enumerateDirectories(path))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    folders.Add(folder);
                }
            }
            catch (Exception exception) when (
                folders.Count > 0 &&
                exception is UnauthorizedAccessException or IOException &&
                exception is not (DirectoryNotFoundException or DriveNotFoundException))
            {
                // A failed iterator cannot reveal children it has not yielded.
                cancellationToken.ThrowIfCancellationRequested();
                return new ExplorerChildFoldersResult(
                    Array.AsReadOnly(folders.ToArray()),
                    $"Some child folders of '{path}' could not be listed: {exception.Message}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new ExplorerChildFoldersResult(Array.AsReadOnly(folders.ToArray()), null);
        }, cancellationToken);

    public Task<bool> HasPlaylistAsync(string path, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hasPlaylist = File.Exists(Path.Combine(path, "Playlist.mpcpl"));
            cancellationToken.ThrowIfCancellationRequested();
            return hasPlaylist;
        }, cancellationToken);
}
