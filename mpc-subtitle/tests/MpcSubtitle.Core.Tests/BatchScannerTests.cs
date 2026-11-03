using MpcSubtitle.Core;
using Xunit;

namespace MpcSubtitle.Core.Tests;

public class BatchScannerTests
{
    private static string NewTempDir()
        => Path.Combine(Path.GetTempPath(), "mpcscantest-" + Guid.NewGuid().ToString("N"));

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static BatchScanner RecordingScanner(List<string> processed)
        => new((video, srt, _) =>
        {
            processed.Add(video);
            File.WriteAllText(srt, "DONE");
            return Task.CompletedTask;
        });

    [Fact]
    public async Task Scan_GeneratesForMissingVideos_Recursively()
    {
        var root = NewTempDir();
        try
        {
            WriteFile(Path.Combine(root, "a.mp4"), "x");
            WriteFile(Path.Combine(root, "b.mkv"), "x");
            WriteFile(Path.Combine(root, "sub", "c.mp4"), "x");
            WriteFile(Path.Combine(root, "sub", "d.txt"), "not video");
            WriteFile(Path.Combine(root, "readme.txt"), "not video");

            var processed = new List<string>();
            var stats = await RecordingScanner(processed).ScanAsync(
                new[] { root },
                new BatchScanOptions { SplitFirstLevel = false },
                (_, _) => ConflictDecision.Replace,
                cancellationToken: CancellationToken.None);

            Assert.Equal(3, stats.Total);
            Assert.Equal(3, stats.Created);
            Assert.Equal(3, stats.Done);
            Assert.Equal(0, stats.Replaced);
            Assert.Equal(0, stats.Skipped);
            Assert.Equal(0, stats.Failed);
            Assert.Equal(3, processed.Count);
            Assert.All(processed, video => Assert.Equal("DONE", File.ReadAllText(Path.ChangeExtension(video, ".srt"))));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_SkipThenReplace_HonorsDecisions()
    {
        var root = NewTempDir();
        try
        {
            var aVideo = Path.Combine(root, "a.mp4");
            var aSrt = Path.ChangeExtension(aVideo, ".srt");
            var bVideo = Path.Combine(root, "b.mp4");
            var bSrt = Path.ChangeExtension(bVideo, ".srt");
            WriteFile(aVideo, "x");
            WriteFile(aSrt, "ORIGINAL");
            WriteFile(bVideo, "x");
            WriteFile(bSrt, "ORIGINAL");

            var calls = 0;
            ConflictDecision Decide(string video, string _)
            {
                calls++;
                return video.EndsWith("a.mp4", StringComparison.OrdinalIgnoreCase)
                    ? ConflictDecision.Skip
                    : ConflictDecision.Replace;
            }

            var stats = await RecordingScanner(new List<string>()).ScanAsync(
                new[] { root },
                new BatchScanOptions(),
                Decide,
                cancellationToken: CancellationToken.None);

            Assert.Equal(2, calls);
            Assert.Equal(1, stats.Skipped);
            Assert.Equal(1, stats.Replaced);
            Assert.Equal(0, stats.Created);
            Assert.Equal("ORIGINAL", File.ReadAllText(aSrt));
            Assert.Equal("DONE", File.ReadAllText(bSrt));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_SkipAll_SkipsRemainingWithoutFurtherPrompts()
    {
        var root = NewTempDir();
        try
        {
            foreach (var name in new[] { "a", "b", "c" })
            {
                WriteFile(Path.Combine(root, name + ".mp4"), "x");
                WriteFile(Path.Combine(root, name + ".srt"), "ORIGINAL");
            }

            var calls = 0;
            var processed = new List<string>();
            var scanner = new BatchScanner((video, srt, _) =>
            {
                processed.Add(video);
                File.WriteAllText(srt, "DONE");
                return Task.CompletedTask;
            });

            var stats = await scanner.ScanAsync(
                new[] { root },
                new BatchScanOptions(),
                (_, _) => { calls++; return ConflictDecision.SkipAll; },
                cancellationToken: CancellationToken.None);

            Assert.Equal(1, calls);
            Assert.Equal(3, stats.Skipped);
            Assert.Equal(0, stats.Created);
            Assert.Equal(0, stats.Replaced);
            Assert.Empty(processed);
            Assert.All(new[] { "a", "b", "c" }, n => Assert.Equal("ORIGINAL", File.ReadAllText(Path.Combine(root, n + ".srt"))));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveRoots_SplitFirstLevel_YieldsImmediateChildren()
    {
        var root = NewTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "one"));
            Directory.CreateDirectory(Path.Combine(root, "two"));
            Directory.CreateDirectory(Path.Combine(root, "three"));
            WriteFile(Path.Combine(root, "loose.mp4"), "x");

            var resolved = BatchScanner.ResolveRoots(new[] { root }, splitFirstLevel: true);

            Assert.Equal(3, resolved.Count);
            Assert.DoesNotContain(Path.GetFullPath(root), resolved);

            // Files directly under the parent are ignored when splitting.
            var videos = BatchScanner.EnumerateVideos(resolved, CancellationToken.None);
            Assert.DoesNotContain(videos, v => v.Video == Path.Combine(root, "loose.mp4"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveRoots_PrunesNestedRoots()
    {
        var root = NewTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "sub"));

            var resolved = BatchScanner.ResolveRoots(
                new[] { root, Path.Combine(root, "sub") },
                splitFirstLevel: false);

            Assert.Single(resolved);
            Assert.Equal(Path.GetFullPath(root), resolved[0]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
