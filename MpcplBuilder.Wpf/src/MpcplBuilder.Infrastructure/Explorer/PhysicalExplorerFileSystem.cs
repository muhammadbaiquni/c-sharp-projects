using MpcplBuilder.Application.Explorer;

namespace MpcplBuilder.Infrastructure.Explorer;

public sealed class PhysicalExplorerFileSystem : IExplorerFileSystem
{
    private readonly DriveCatalog _driveCatalog;
    private readonly Func<string, IEnumerable<string>> _enumerateDirectories;

    public PhysicalExplorerFileSystem()
        : this(DriveInfo.GetDrives, Directory.EnumerateDirectories)
    {
    }

    internal PhysicalExplorerFileSystem(
        Func<DriveInfo[]> getDrives,
        Func<string, IEnumerable<string>> enumerateDirectories)
    {
        _driveCatalog = new DriveCatalog(getDrives);
        _enumerateDirectories = enumerateDirectories;
    }

    public Task<IReadOnlyList<string>> GetDrivesAsync(CancellationToken cancellationToken) =>
        Task.Run(() => _driveCatalog.GetReadyRoots(cancellationToken), cancellationToken);

    public Task<IReadOnlyList<string>> GetChildFoldersAsync(string path, CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<string>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folders = new List<string>();
            foreach (var folder in _enumerateDirectories(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                folders.Add(folder);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return folders;
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
