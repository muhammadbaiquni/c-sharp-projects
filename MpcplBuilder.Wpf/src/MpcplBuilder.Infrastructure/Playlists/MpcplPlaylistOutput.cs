using System.Text;
using MpcplBuilder.Application.Abstractions;
using MpcplBuilder.Domain.Playlists;

namespace MpcplBuilder.Infrastructure.Playlists;

public sealed class MpcplPlaylistOutput : IPlaylistOutput
{
    private const string OutputFileName = "Playlist.mpcpl";

    public Task<PlaylistOutputMetadata?> GetMetadataAsync(
        string rootPath,
        CancellationToken cancellationToken) =>
        Task.Run<PlaylistOutputMetadata?>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outputPath = GetOutputPath(rootPath);
            if (!File.Exists(outputPath))
                return null;

            var file = new FileInfo(outputPath);
            return new PlaylistOutputMetadata(file.LastWriteTime, file.Length);
        }, cancellationToken);

    public Task<string> WriteAsync(
        string rootPath,
        IReadOnlyList<PlaylistEntry> entries,
        PathMode pathMode,
        CancellationToken cancellationToken) =>
        Task.Run(() => Write(rootPath, entries, pathMode, cancellationToken), cancellationToken);

    private static string Write(
        string rootPath,
        IReadOnlyList<PlaylistEntry> entries,
        PathMode pathMode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(rootPath);
        var outputPath = GetOutputPath(rootPath);

        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
        {
            NewLine = "\r\n"
        };

        writer.WriteLine("MPCPLAYLIST");
        for (var index = 0; index < entries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var number = index + 1;
            var entry = entries[index];
            writer.WriteLine($"{number},type,0");
            writer.WriteLine($"{number},filename,{Format(entry.VideoPath, rootPath, pathMode)}");

            foreach (var subtitlePath in entry.SubtitlePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.WriteLine($"{number},subtitle,{Format(subtitlePath, rootPath, pathMode)}");
            }
        }

        return outputPath;
    }

    private static string Format(string path, string rootPath, PathMode pathMode) =>
        PlaylistPathFormatter.Format(path, rootPath, pathMode, OperatingSystem.IsWindows());

    private static string GetOutputPath(string rootPath) => Path.Combine(rootPath, OutputFileName);
}
