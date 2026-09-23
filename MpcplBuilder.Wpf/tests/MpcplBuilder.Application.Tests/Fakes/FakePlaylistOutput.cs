using MpcplBuilder.Application.Abstractions;
using MpcplBuilder.Domain.Playlists;

namespace MpcplBuilder.Application.Tests.Fakes;

internal sealed class FakePlaylistOutput : IPlaylistOutput
{
    public PlaylistOutputMetadata? Metadata { get; set; }
    public Exception? ExceptionToThrow { get; set; }
    public int WriteCalls { get; private set; }
    public IReadOnlyList<PlaylistEntry>? WrittenEntries { get; private set; }
    public PathMode? WrittenPathMode { get; private set; }
    public string OutputPath { get; set; } = @"D:\Media\Playlist.mpcpl";

    public Task<PlaylistOutputMetadata?> GetMetadataAsync(string rootPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;
        return Task.FromResult(Metadata);
    }

    public Task<string> WriteAsync(
        string rootPath,
        IReadOnlyList<PlaylistEntry> entries,
        PathMode pathMode,
        bool overwriteExisting,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;
        WriteCalls++;
        WrittenEntries = entries;
        WrittenPathMode = pathMode;
        return Task.FromResult(OutputPath);
    }
}
