using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;

namespace MpcplBuilder.Wpf;

public static class PlaylistBuilder
{
    // Publicly exposed video extensions list so other components can reuse it
    public static readonly string[] VideoExtensions = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".m4v", ".webm", ".mpg", ".mpeg", ".ts", ".m2ts", ".flv" };

    private static readonly string[] SubExt = new[] { ".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx", ".txt" };

    private const string LongPathPrefix = @"\\?\";

    public enum PathMode
    {
        Relative, // Default - portable, works when folder is moved
        FullPath, // Absolute path
        LongPath  // \\?\ prefix for very long paths
    }

    public sealed class PlaylistEntry
    {
        public string VideoPath { get; set; } = default!;
        public List<string> SubtitlePaths { get; set; } = new();
    }

    public static List<PlaylistEntry> BuildEntries(string rootDir, System.Threading.CancellationToken cancellationToken = default)
    {
        var videos = EnumerateFilesSafe(rootDir, VideoExtensions, cancellationToken).ToList();
        var entries = new List<PlaylistEntry>(videos.Count);

        foreach (var v in videos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var subs = FindMatchingSubtitles(v, cancellationToken);
            entries.Add(new PlaylistEntry { VideoPath = v, SubtitlePaths = subs });
        }

        return entries;
    }

    public static void WriteMpcpl(string outFile, List<PlaylistEntry> entries, string rootDir, PathMode pathMode, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);

        using var fs = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
        {
            NewLine = "\r\n"
        };

        writer.WriteLine("MPCPLAYLIST");

        int index = 1;
        foreach (var e in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writer.WriteLine($"{index},type,0");
            writer.WriteLine($"{index},filename,{NormalizeForOutput(e.VideoPath, rootDir, pathMode)}");

            foreach (var s in e.SubtitlePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.WriteLine($"{index},subtitle,{NormalizeForOutput(s, rootDir, pathMode)}");
            }
            index++;
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root, string[] allowedExt, System.Threading.CancellationToken cancellationToken = default)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();
            IEnumerable<string> subDirs = Array.Empty<string>();
            IEnumerable<string> files = Array.Empty<string>();

            try { subDirs = Directory.EnumerateDirectories(current); } catch { }
            try { files = Directory.EnumerateFiles(current); } catch { }

            foreach (var f in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var ext = Path.GetExtension(f);
                if (allowedExt.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    yield return f;
            }
            foreach (var d in subDirs) stack.Push(d);
        }
    }

    private static List<string> FindMatchingSubtitles(string videoPath, System.Threading.CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(videoPath)!;
        var baseName = Path.GetFileNameWithoutExtension(videoPath);

        var subs = new List<string>();
        IEnumerable<string> files = Array.Empty<string>();

        try { files = Directory.EnumerateFiles(dir); } catch { }

        foreach (var f in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ext = Path.GetExtension(f);
            if (!SubExt.Contains(ext, StringComparer.OrdinalIgnoreCase))
                continue;

            var nameNoExt = Path.GetFileNameWithoutExtension(f);
            bool match =
                  nameNoExt.Equals(baseName, StringComparison.OrdinalIgnoreCase) ||
         nameNoExt.StartsWith(baseName + ".", StringComparison.OrdinalIgnoreCase);

            if (match) subs.Add(f);
        }

        subs.Sort(StringComparer.OrdinalIgnoreCase);
        return subs;
    }

    private static string NormalizeForOutput(string filePath, string rootDir, PathMode pathMode)
    {
        var fullPath = Path.GetFullPath(filePath);
        var fullRoot = Path.GetFullPath(rootDir);

        // Ensure root path ends with directory separator
        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
            fullRoot += Path.DirectorySeparatorChar;

        switch (pathMode)
        {
            case PathMode.Relative:
                // Convert to relative path if file is under root
                if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                {
                    var relativePath = fullPath.Substring(fullRoot.Length);
                    return relativePath;
                }
                // If file is outside root, use full path as fallback
                return fullPath;

            case PathMode.LongPath:
                // Add \\?\ prefix for long path support on Windows
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (!fullPath.StartsWith(LongPathPrefix, StringComparison.Ordinal))
                    {
                        if (fullPath.StartsWith(@"\\"))
                            return LongPathPrefix + @"UNC\" + fullPath.TrimStart('\\');
                        return LongPathPrefix + fullPath;
                    }
                }
                return fullPath;

            case PathMode.FullPath:
            default:
                // Return full absolute path
                return fullPath;
        }
    }
}
