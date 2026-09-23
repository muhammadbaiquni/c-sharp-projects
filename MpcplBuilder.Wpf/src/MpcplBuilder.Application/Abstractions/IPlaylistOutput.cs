using MpcplBuilder.Domain.Playlists;

namespace MpcplBuilder.Application.Abstractions;

public sealed record PlaylistOutputMetadata(DateTime LastWriteTime, long Length);

public interface IPlaylistOutput
{
    Task<PlaylistOutputMetadata?> GetMetadataAsync(string rootPath, CancellationToken cancellationToken);

    Task<string> WriteAsync(
        string rootPath,
        IReadOnlyList<PlaylistEntry> entries,
        PathMode pathMode,
        bool overwriteExisting,
        CancellationToken cancellationToken);
}
