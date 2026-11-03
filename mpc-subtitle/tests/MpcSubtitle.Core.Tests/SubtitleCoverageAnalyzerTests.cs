using MpcSubtitle.Core;
using Xunit;

namespace MpcSubtitle.Core.Tests;

public class SubtitleCoverageAnalyzerTests
{
    private static string NewTempDir()
        => Path.Combine(Path.GetTempPath(), "mpccov-" + Guid.NewGuid().ToString("N"));

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void Compute_AllVideosHaveSubtitle_ReturnsAll()
    {
        var root = NewTempDir();
        try
        {
            Write(Path.Combine(root, "a.mp4"), "x");
            Write(Path.Combine(root, "a.srt"), "s");
            Write(Path.Combine(root, "b.mkv"), "x");
            Write(Path.Combine(root, "b.srt"), "s");

            Assert.Equal(SubtitleCoverage.All, SubtitleCoverageAnalyzer.Compute(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Compute_SomeVideosHaveSubtitle_ReturnsPartial()
    {
        var root = NewTempDir();
        try
        {
            Write(Path.Combine(root, "a.mp4"), "x");
            Write(Path.Combine(root, "a.srt"), "s");
            Write(Path.Combine(root, "b.mp4"), "x"); // no subtitle

            Assert.Equal(SubtitleCoverage.Partial, SubtitleCoverageAnalyzer.Compute(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Compute_NoSubtitles_ReturnsNone()
    {
        var root = NewTempDir();
        try
        {
            Write(Path.Combine(root, "a.mp4"), "x");
            Write(Path.Combine(root, "b.mp4"), "x");

            Assert.Equal(SubtitleCoverage.None, SubtitleCoverageAnalyzer.Compute(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Compute_NoVideos_ReturnsNone()
    {
        var root = NewTempDir();
        try
        {
            Write(Path.Combine(root, "readme.txt"), "x");

            Assert.Equal(SubtitleCoverage.None, SubtitleCoverageAnalyzer.Compute(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Compute_IgnoresNonVideoFiles()
    {
        var root = NewTempDir();
        try
        {
            Write(Path.Combine(root, "movie.mp4"), "x");
            Write(Path.Combine(root, "movie.srt"), "s");
            Write(Path.Combine(root, "image.png"), "x");
            Write(Path.Combine(root, "subtitles.ass"), "x"); // not counted as a video

            Assert.Equal(SubtitleCoverage.All, SubtitleCoverageAnalyzer.Compute(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Compute_MissingDirectory_ReturnsNone()
    {
        var missing = Path.Combine(Path.GetTempPath(), "mpccov-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(SubtitleCoverage.None, SubtitleCoverageAnalyzer.Compute(missing));
    }
}
