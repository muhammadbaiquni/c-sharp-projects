using MpcplBuilder.Domain.Media;

namespace MpcplBuilder.Domain.Tests;

public sealed class SubtitleMatchingPolicyTests
{
    [Theory]
    [InlineData("Movie.srt", "Movie.mkv", true)]
    [InlineData("Movie.en.srt", "Movie.mkv", true)]
    [InlineData("movie.ID.ass", "Movie.mkv", true)]
    [InlineData("MovieTrailer.srt", "Movie.mkv", false)]
    [InlineData("Other.Movie.srt", "Movie.mkv", false)]
    public void IsMatch_UsesExactNameOrDotSuffix(string subtitle, string video, bool expected)
    {
        SubtitleMatchingPolicy.IsMatch(video, subtitle).Should().Be(expected);
    }
}
