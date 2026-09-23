using MpcplBuilder.Domain.Media;

namespace MpcplBuilder.Domain.Tests;

public sealed class SupportedMediaTests
{
    [Theory]
    [InlineData("Movie.mp4")]
    [InlineData("Movie.MKV")]
    [InlineData("Movie.m2ts")]
    public void IsVideo_SupportedExtension_ReturnsTrue(string path)
    {
        SupportedMedia.IsVideo(path).Should().BeTrue();
    }

    [Theory]
    [InlineData("Movie.txt")]
    [InlineData("Movie")]
    [InlineData("Movie.mp4.tmp")]
    public void IsVideo_UnsupportedExtension_ReturnsFalse(string path)
    {
        SupportedMedia.IsVideo(path).Should().BeFalse();
    }

    [Theory]
    [InlineData("Movie.srt")]
    [InlineData("Movie.EN.ASS")]
    [InlineData("Movie.idx")]
    public void IsSubtitle_SupportedExtension_ReturnsTrue(string path)
    {
        SupportedMedia.IsSubtitle(path).Should().BeTrue();
    }
}
