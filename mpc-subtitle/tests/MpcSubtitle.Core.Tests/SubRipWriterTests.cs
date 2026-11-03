using MpcSubtitle.Core;
using Xunit;

namespace MpcSubtitle.Core.Tests;

public class SubRipWriterTests
{
    [Fact]
    public void FormatTimestamp_PadsAllFields()
    {
        Assert.Equal("00:00:00,000", SubRipWriter.FormatTimestamp(TimeSpan.Zero));
        Assert.Equal("00:01:02,345",
            SubRipWriter.FormatTimestamp(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(2) + TimeSpan.FromMilliseconds(345)));
        Assert.Equal("01:01:01,120",
            SubRipWriter.FormatTimestamp(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1) + TimeSpan.FromMilliseconds(120)));
    }

    [Fact]
    public void Write_ProducesWellFormedCues()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".srt");
        try
        {
            SubRipWriter.Write(
                path,
                new[]
                {
                    new SubtitleSegment(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), "Hello"),
                    new SubtitleSegment(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(6), "World\nsecond line"),
                });

            var lines = File.ReadAllText(path).Split('\n');

            Assert.Equal("1", lines[0]);
            Assert.Equal("00:00:01,000 --> 00:00:03,000", lines[1]);
            Assert.Equal("Hello", lines[2]);
            Assert.Equal("", lines[3]);
            Assert.Equal("2", lines[4]);
            Assert.Equal("00:00:04,000 --> 00:00:06,000", lines[5]);
            Assert.Equal("World", lines[6]);
            Assert.Equal("second line", lines[7]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_SkipsEmptyAndWhitespaceSegments()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".srt");
        try
        {
            SubRipWriter.Write(
                path,
                new[]
                {
                    new SubtitleSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), ""),
                    new SubtitleSegment(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "  \n"),
                    new SubtitleSegment(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), "Real text"),
                });

            var text = File.ReadAllText(path);
            Assert.Equal(1, text.Split("-->").Length - 1); // exactly one cue arrow
            Assert.Contains("Real text", text);
            Assert.DoesNotContain("00:00:00,000 -->", text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_RepairsInvertedTimestamps()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".srt");
        try
        {
            SubRipWriter.Write(
                path,
                new[] { new SubtitleSegment(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), "Backwards") });

            var text = File.ReadAllText(path);
            Assert.Contains("00:00:02,000 --> 00:00:05,000", text);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
