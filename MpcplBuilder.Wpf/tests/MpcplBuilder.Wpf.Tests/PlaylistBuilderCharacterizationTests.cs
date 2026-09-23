using FluentAssertions;

namespace MpcplBuilder.Wpf.Tests;

public sealed class PlaylistBuilderCharacterizationTests
{
    [Fact]
    public void WriteMpcpl_WritesCompatibleHeaderNumberingAndSubtitle()
    {
        using var temp = new TemporaryDirectory();
        var video = temp.CreateFile("Movie.mkv");
        var subtitle = temp.CreateFile("Movie.en.srt");
        var output = temp.PathFor("Playlist.mpcpl");

        PlaylistBuilder.WriteMpcpl(
            output,
            [new PlaylistBuilder.PlaylistEntry
            {
                VideoPath = video,
                SubtitlePaths = [subtitle]
            }],
            temp.Path,
            PlaylistBuilder.PathMode.Relative);

        File.ReadAllText(output).Should().Be(
            "MPCPLAYLIST\r\n1,type,0\r\n1,filename,Movie.mkv\r\n1,subtitle,Movie.en.srt\r\n");
        File.ReadAllBytes(output).Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
    }

    [Fact]
    public void BuildEntries_RecursesAndMatchesExactAndLanguageSubtitles()
    {
        using var temp = new TemporaryDirectory();
        temp.CreateFile("nested/Movie.MKV");
        temp.CreateFile("nested/Movie.srt");
        temp.CreateFile("nested/Movie.en.ass");
        temp.CreateFile("nested/MovieTrailer.srt");
        temp.CreateFile("nested/readme.md");

        var entry = PlaylistBuilder.BuildEntries(temp.Path).Should().ContainSingle().Subject;

        System.IO.Path.GetFileName(entry.VideoPath).Should().Be("Movie.MKV");
        entry.SubtitlePaths.Select(System.IO.Path.GetFileName)
            .Should().Equal("Movie.en.ass", "Movie.srt");
    }
}
