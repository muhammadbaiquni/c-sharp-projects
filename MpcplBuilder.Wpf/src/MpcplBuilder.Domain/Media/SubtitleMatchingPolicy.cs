namespace MpcplBuilder.Domain.Media;

public static class SubtitleMatchingPolicy
{
    public static bool IsMatch(string videoPath, string subtitlePath)
    {
        var videoName = Path.GetFileNameWithoutExtension(videoPath);
        var subtitleName = Path.GetFileNameWithoutExtension(subtitlePath);

        return subtitleName.Equals(videoName, StringComparison.OrdinalIgnoreCase)
            || subtitleName.StartsWith(videoName + ".", StringComparison.OrdinalIgnoreCase);
    }
}
