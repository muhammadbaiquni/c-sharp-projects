using MpcplBuilder.Application.Explorer;

namespace MpcplBuilder.Application.Tests.Fakes;

internal sealed class FakeExplorerFileSystem : IExplorerFileSystem
{
    public IReadOnlyList<string> Drives { get; set; } = [];
    public Dictionary<string, IReadOnlyList<string>> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> PlaylistFolders { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Exception? ExceptionToThrow { get; set; }

    public Task<IReadOnlyList<string>> GetDrivesAsync(CancellationToken cancellationToken)
    {
        Check(cancellationToken);
        return Task.FromResult(Drives);
    }

    public Task<IReadOnlyList<string>> GetChildFoldersAsync(string path, CancellationToken cancellationToken)
    {
        Check(cancellationToken);
        return Task.FromResult(Children.GetValueOrDefault(path) ?? []);
    }

    public Task<bool> HasPlaylistAsync(string path, CancellationToken cancellationToken)
    {
        Check(cancellationToken);
        return Task.FromResult(PlaylistFolders.Contains(path));
    }

    private void Check(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;
    }
}
