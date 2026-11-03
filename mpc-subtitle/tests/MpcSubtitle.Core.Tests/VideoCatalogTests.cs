using MpcSubtitle.Core;
using Xunit;

namespace MpcSubtitle.Core.Tests;

public class VideoCatalogTests
{
    [Theory]
    [InlineData("a.mp4", true)]
    [InlineData("a.MKV", true)]
    [InlineData("a.webm", true)]
    [InlineData("a.divx", true)]
    [InlineData("a.srt", false)]
    [InlineData("a.txt", false)]
    [InlineData("a.mp4.bak", false)]
    [InlineData("a", false)]
    public void IsVideoFile_MatchesExtensionSet(string name, bool expected)
    {
        Assert.Equal(expected, VideoCatalog.IsVideoFile(name));
    }
}
