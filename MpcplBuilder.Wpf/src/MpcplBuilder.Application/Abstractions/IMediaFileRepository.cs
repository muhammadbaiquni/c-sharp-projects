namespace MpcplBuilder.Application.Abstractions;

public interface IMediaFileRepository
{
    bool DirectoryExists(string rootPath);
    Task<bool> HasAnyVideoAsync(string rootPath, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetVideosAsync(string rootPath, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetMatchingSubtitlesAsync(string videoPath, CancellationToken cancellationToken);
}
