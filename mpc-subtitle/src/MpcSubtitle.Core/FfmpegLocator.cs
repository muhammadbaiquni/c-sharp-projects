namespace MpcSubtitle.Core;

/// <summary>Locates ffmpeg from an explicit path or the process PATH.</summary>
public static class FfmpegLocator
{
    /// <summary>Returns the ffmpeg executable path, or null when not found.</summary>
    public static string? Locate(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var full = Path.GetFullPath(explicitPath);
            return File.Exists(full) ? full : null;
        }

        return FindOnPath("ffmpeg.exe") ?? FindOnPath("ffmpeg");
    }

    public static string? FindOnPath(string name)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        var hasExtension = Path.GetExtension(name).Length > 0;
        string[] extensions = hasExtension ? new[] { string.Empty } : new[] { ".exe", ".com", ".bat", ".cmd" };

        foreach (var rawDir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var dir = rawDir.Trim().Trim('"');
            if (dir.Length == 0)
            {
                continue;
            }

            try
            {
                foreach (var ext in extensions)
                {
                    var full = Path.Combine(dir, name + ext);
                    if (File.Exists(full))
                    {
                        return Path.GetFullPath(full);
                    }
                }
            }
            catch
            {
                // Skip unreadable PATH entries.
            }
        }

        return null;
    }
}
