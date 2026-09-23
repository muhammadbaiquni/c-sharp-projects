namespace MpcplBuilder.Domain.Playlists;

public static class PlaylistPathFormatter
{
    private const string LongPathPrefix = @"\\?\";

    public static string Format(string filePath, string rootPath, PathMode mode, bool isWindows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        if (mode == PathMode.Long && !isWindows)
            return filePath;

        var fullPath = Path.GetFullPath(filePath);
        var fullRoot = Path.GetFullPath(rootPath);

        return mode switch
        {
            PathMode.Relative => FormatRelative(fullPath, fullRoot),
            PathMode.Long when isWindows => FormatLongWindowsPath(fullPath),
            _ => fullPath
        };
    }

    private static string FormatRelative(string fullPath, string fullRoot)
    {
        var rootWithSeparator = Path.EndsInDirectorySeparator(fullRoot)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            ? fullPath[rootWithSeparator.Length..]
            : fullPath;
    }

    private static string FormatLongWindowsPath(string fullPath)
    {
        if (fullPath.StartsWith(LongPathPrefix, StringComparison.Ordinal))
            return fullPath;

        return fullPath.StartsWith(@"\\", StringComparison.Ordinal)
            ? LongPathPrefix + @"UNC\" + fullPath.TrimStart('\\')
            : LongPathPrefix + fullPath;
    }
}
