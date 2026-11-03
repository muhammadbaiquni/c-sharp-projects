namespace MpcSubtitle.Core;

/// <summary>Recognized video/audio container extensions.</summary>
public static class VideoCatalog
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v",
        ".mpg", ".mpeg", ".m2ts", ".mts", ".ts", ".3gp", ".ogv", ".ogm",
        ".rm", ".rmvb", ".asf", ".vob", ".divx",
    };

    public static bool IsVideoFile(string path)
        => Extensions.Contains(Path.GetExtension(path));
}
