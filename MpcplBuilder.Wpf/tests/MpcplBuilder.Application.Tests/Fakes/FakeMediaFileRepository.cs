using MpcplBuilder.Application.Abstractions;

namespace MpcplBuilder.Application.Tests.Fakes;

internal sealed class FakeMediaFileRepository : IMediaFileRepository
{
    public bool RootExists { get; set; } = true;
    public bool HasAnyVideo { get; set; } = true;
    public Exception? ExceptionToThrow { get; set; }
    public IReadOnlyList<string> Videos { get; set; } = [];
    public Dictionary<string, IReadOnlyList<string>> Subtitles { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int HasAnyVideoCalls { get; private set; }

    public bool DirectoryExists(string rootPath) => RootExists;

    public Task<bool> HasAnyVideoAsync(string rootPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfConfigured();
        HasAnyVideoCalls++;
        return Task.FromResult(HasAnyVideo);
    }

    public Task<IReadOnlyList<string>> GetVideosAsync(string rootPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfConfigured();
        return Task.FromResult(Videos);
    }

    public Task<IReadOnlyList<string>> GetMatchingSubtitlesAsync(string videoPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfConfigured();
        return Task.FromResult(Subtitles.GetValueOrDefault(videoPath) ?? []);
    }

    private void ThrowIfConfigured()
    {
        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;
    }
}
