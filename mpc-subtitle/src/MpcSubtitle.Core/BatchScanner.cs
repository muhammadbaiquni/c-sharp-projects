namespace MpcSubtitle.Core;

public enum ConflictDecision
{
    Skip,
    SkipAll,
    Replace,
    ReplaceAll,
}

public sealed record BatchScanOptions
{
    /// <summary>Treat each immediate child directory of every selected root as its own scan root.</summary>
    public bool SplitFirstLevel { get; init; }
}

/// <summary>Running counters for a batch scan, inspected by the UI.</summary>
public sealed class ScanStats
{
    public int Total;
    public int Done;
    public int Created;
    public int Replaced;
    public int Skipped;
    public int Failed;
    public string? Current;
}

/// <summary>
/// Recursively enumerates video files under the given roots and runs a processor
/// (injected) over each, prompting a decision callback when a subtitle already exists.
/// </summary>
public sealed class BatchScanner
{
    private readonly Func<string, string, CancellationToken, Task> _process;

    public BatchScanner(Func<string, string, CancellationToken, Task> process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
    }

    public async Task<ScanStats> ScanAsync(
        IReadOnlyCollection<string> roots,
        BatchScanOptions options,
        Func<string, string, ConflictDecision> onConflict,
        Action<string>? onLog = null,
        Action<ScanStats>? onStats = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(onConflict);

        var stats = new ScanStats();
        void Emit() => onStats?.Invoke(stats);

        var scanRoots = ResolveRoots(roots, options.SplitFirstLevel);
        foreach (var root in scanRoots)
        {
            onLog?.Invoke($"Scan folder: {root}");
        }

        var files = EnumerateVideos(scanRoots, cancellationToken);
        stats.Total = files.Count;
        onLog?.Invoke($"Found {files.Count} video file(s).");
        Emit();

        var skipAll = false;
        var replaceAll = false;

        foreach (var (video, srt) in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            stats.Current = video;
            Emit();
            onLog?.Invoke($"[{stats.Done + 1}/{stats.Total}] {Path.GetFileName(video)}");

            var existed = File.Exists(srt);
            var process = true;

            if (existed && !replaceAll)
            {
                ConflictDecision decision = skipAll ? ConflictDecision.Skip : onConflict(video, srt);
                cancellationToken.ThrowIfCancellationRequested();

                switch (decision)
                {
                    case ConflictDecision.Skip:
                        stats.Skipped++;
                        process = false;
                        onLog?.Invoke("  -> skipped");
                        break;
                    case ConflictDecision.SkipAll:
                        skipAll = true;
                        stats.Skipped++;
                        process = false;
                        onLog?.Invoke("  -> skipped (skip all)");
                        break;
                    case ConflictDecision.Replace:
                        break; // overwrite just this one
                    case ConflictDecision.ReplaceAll:
                        replaceAll = true;
                        break; // overwrite this one and the rest
                }
            }

            if (process)
            {
                try
                {
                    await _process(video, srt, cancellationToken);

                    if (!File.Exists(srt))
                    {
                        throw new InvalidOperationException("Processor did not produce a subtitle file.");
                    }

                    if (existed)
                    {
                        stats.Replaced++;
                    }
                    else
                    {
                        stats.Created++;
                    }

                    onLog?.Invoke($"  -> OK: {Path.GetFileName(srt)}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    stats.Failed++;
                    onLog?.Invoke($"  -> ERROR: {ex.Message}");
                }
            }

            stats.Done++;
            Emit();
        }

        return stats;
    }

    /// <summary>Normalizes selected roots, expanding split-first-level and removing nested duplicates.</summary>
    public static List<string> ResolveRoots(IReadOnlyCollection<string> roots, bool splitFirstLevel)
    {
        var resolved = new List<string>();

        foreach (var raw in roots)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var root = Path.GetFullPath(raw);
            if (!Directory.Exists(root))
            {
                continue;
            }

            if (splitFirstLevel)
            {
                IEnumerable<string> children;
                try
                {
                    children = Directory.EnumerateDirectories(root);
                }
                catch
                {
                    children = Array.Empty<string>();
                }

                resolved.AddRange(children);
            }
            else
            {
                resolved.Add(root);
            }
        }

        return PruneNestedRoots(resolved);
    }

    public static List<string> PruneNestedRoots(List<string> roots)
    {
        var distinct = roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        distinct.Sort((a, b) =>
        {
            var lengthOrder = a.Length.CompareTo(b.Length);
            return lengthOrder != 0 ? lengthOrder : StringComparer.OrdinalIgnoreCase.Compare(a, b);
        });

        var pruned = new List<string>();
        foreach (var root in distinct)
        {
            if (!pruned.Any(already => IsDescendant(root, already)))
            {
                pruned.Add(root);
            }
        }

        return pruned;
    }

    public static List<(string Video, string Srt)> EnumerateVideos(IReadOnlyCollection<string> roots, CancellationToken cancellationToken)
    {
        var list = new List<(string Video, string Srt)>();

        foreach (var root in roots)
        {
            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = stack.Pop();

                IEnumerable<string> files;
                IEnumerable<string> subdirectories;
                try
                {
                    files = Directory.EnumerateFiles(directory);
                    subdirectories = Directory.EnumerateDirectories(directory);
                }
                catch
                {
                    continue; // inaccessible or deleted directory
                }

                foreach (var file in files)
                {
                    if (VideoCatalog.IsVideoFile(file))
                    {
                        list.Add((file, Path.ChangeExtension(file, ".srt")));
                    }
                }

                foreach (var sub in subdirectories)
                {
                    stack.Push(sub);
                }
            }
        }

        list.Sort((a, b) => string.Compare(a.Video, b.Video, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    private static bool IsDescendant(string path, string root)
    {
        var withSeparator = path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return withSeparator.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
               && withSeparator.Length > rootWithSeparator.Length;
    }
}
