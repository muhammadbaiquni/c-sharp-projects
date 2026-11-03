namespace MpcSubtitle.Core;

/// <summary>Folder explorer plumbing: lists immediate subdirectories safely for lazy tree loading.</summary>
public static class FolderExplorer
{
    public const int DefaultMaxEntries = 2000;

    /// <summary>
    /// Returns immediate subdirectories of <paramref name="path"/>, sorted by name,
    /// skipping reparse points (junctions/symlinks) and unreadable entries.
    /// </summary>
    public static List<string> ListSubdirectories(string path, int maxEntries = DefaultMaxEntries)
    {
        var result = new List<string>();

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(path))
            {
                if (result.Count >= maxEntries)
                {
                    break;
                }

                try
                {
                    // Skip reparse points (junctions/symlinks) to avoid cycles.
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }

                result.Add(directory);
            }
        }
        catch
        {
            // Inaccessible or removed directory.
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }
}
