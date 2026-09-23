namespace MpcplBuilder.Domain.Media;

public static class SupportedMedia
{
    private static readonly HashSet<string> VideoExtensions = new(
        [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".m4v", ".webm", ".mpg", ".mpeg", ".ts", ".m2ts", ".flv"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> SubtitleExtensions = new(
        [".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx", ".txt"],
        StringComparer.OrdinalIgnoreCase);

    public static bool IsVideo(string path) => VideoExtensions.Contains(Path.GetExtension(path));

    public static bool IsSubtitle(string path) => SubtitleExtensions.Contains(Path.GetExtension(path));
}
