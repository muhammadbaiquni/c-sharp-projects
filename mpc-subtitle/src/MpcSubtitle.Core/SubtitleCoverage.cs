namespace MpcSubtitle.Core;

/// <summary>Subtitle coverage of the videos directly inside one folder.</summary>
public enum SubtitleCoverage
{
    /// <summary>No videos, or no video has a subtitle file.</summary>
    None,

    /// <summary>Some but not all videos have a subtitle file.</summary>
    Partial,

    /// <summary>Every video (and there is at least one) has a subtitle file.</summary>
    All,
}

public static class SubtitleCoverageAnalyzer
{
    /// <summary>
    /// Scans the immediate files of <paramref name="directoryPath"/> and reports how many videos
    /// already have a same-named .srt next to them.
    /// </summary>
    public static SubtitleCoverage Compute(string directoryPath)
    {
        var videos = 0;
        var matched = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(directoryPath))
            {
                if (!VideoCatalog.IsVideoFile(file))
                {
                    continue;
                }

                videos++;
                if (File.Exists(Path.ChangeExtension(file, ".srt")))
                {
                    matched++;
                }
            }
        }
        catch
        {
            return SubtitleCoverage.None; // inaccessible or removed
        }

        if (videos == 0 || matched == 0)
        {
            return SubtitleCoverage.None;
        }

        return matched == videos ? SubtitleCoverage.All : SubtitleCoverage.Partial;
    }
}
