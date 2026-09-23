using MpcplBuilder.Application.Explorer;

namespace MpcplBuilder.Application.Tests.Fakes;

internal sealed class FakeExplorerFileSystem : IExplorerFileSystem
{
    public IReadOnlyList<string> Drives { get; set; } = [];
    public Dictionary<string, IReadOnlyList<string>> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> PlaylistFolders { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? ChildAccessWarning { get; set; }
    public Exception? ExceptionToThrow { get; set; }
    public bool IgnoreCancellation { get; set; }
    public Action? OnGetDrives { get; set; }
    public Action? OnGetChildFolders { get; set; }
    public Action? OnHasPlaylist { get; set; }

    public Task<IReadOnlyList<string>> GetDrivesAsync(CancellationToken cancellationToken)
    {
        Check(cancellationToken);
        OnGetDrives?.Invoke();
        return Task.FromResult(Drives);
    }

    public Task<ExplorerChildFoldersResult> GetChildFoldersAsync(string path, CancellationToken cancellationToken)
    {
        Check(cancellationToken);
        OnGetChildFolders?.Invoke();
        return Task.FromResult(new ExplorerChildFoldersResult(
            Children.GetValueOrDefault(path) ?? [], ChildAccessWarning));
    }

    public Task<bool> HasPlaylistAsync(string path, CancellationToken cancellationToken)
    {
        Check(cancellationToken);
        OnHasPlaylist?.Invoke();
        return Task.FromResult(PlaylistFolders.Contains(path));
    }

    private void Check(CancellationToken cancellationToken)
    {
        if (!IgnoreCancellation)
            cancellationToken.ThrowIfCancellationRequested();
        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;
    }
}
