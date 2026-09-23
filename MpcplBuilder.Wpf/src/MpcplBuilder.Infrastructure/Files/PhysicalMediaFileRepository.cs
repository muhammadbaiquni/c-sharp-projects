using MpcplBuilder.Application.Abstractions;
using MpcplBuilder.Domain.Media;

namespace MpcplBuilder.Infrastructure.Files;

public sealed class PhysicalMediaFileRepository : IMediaFileRepository
{
    private readonly DirectoryTraversal _traversal;

    public PhysicalMediaFileRepository()
        : this(new DirectoryTraversal())
    {
    }

    internal PhysicalMediaFileRepository(DirectoryTraversal traversal)
    {
        _traversal = traversal;
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public Task<bool> HasAnyVideoAsync(string rootPath, CancellationToken cancellationToken) =>
        Task.Run(
            () => _traversal.AnyFile(rootPath, SupportedMedia.IsVideo, cancellationToken),
            cancellationToken);

    public Task<IReadOnlyList<string>> GetVideosAsync(
        string rootPath,
        CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<string>>(
            () => _traversal.EnumerateFiles(rootPath, cancellationToken)
                .Where(SupportedMedia.IsVideo)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            cancellationToken);

    public Task<IReadOnlyList<string>> GetMatchingSubtitlesAsync(
        string videoPath,
        CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<string>>(
            () => _traversal.EnumerateFilesInDirectory(
                    Path.GetDirectoryName(videoPath) ?? string.Empty,
                    cancellationToken)
                .Where(SupportedMedia.IsSubtitle)
                .Where(path => SubtitleMatchingPolicy.IsMatch(videoPath, path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            cancellationToken);
}
