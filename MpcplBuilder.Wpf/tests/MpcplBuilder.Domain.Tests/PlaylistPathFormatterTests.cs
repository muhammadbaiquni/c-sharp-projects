using MpcplBuilder.Domain.Playlists;

namespace MpcplBuilder.Domain.Tests;

public sealed class PlaylistPathFormatterTests
{
    [Fact]
    public void Format_RelativeFileUnderRoot_ReturnsRelativePath()
    {
        PlaylistPathFormatter.Format(
                @"C:\Videos\Movies\Movie.mkv",
                @"C:\Videos",
                PathMode.Relative,
                isWindows: true)
            .Should().Be(@"Movies\Movie.mkv");
    }

    [Fact]
    public void Format_RelativeFileOutsideRoot_FallsBackToFullPath()
    {
        PlaylistPathFormatter.Format(
                @"D:\Movies\Movie.mkv",
                @"C:\Videos",
                PathMode.Relative,
                isWindows: true)
            .Should().Be(@"D:\Movies\Movie.mkv");
    }

    [Fact]
    public void Format_FullPath_ReturnsFullPath()
    {
        PlaylistPathFormatter.Format(
                @"C:\Videos\Movie.mkv",
                @"C:\Videos",
                PathMode.Full,
                isWindows: true)
            .Should().Be(@"C:\Videos\Movie.mkv");
    }

    [Fact]
    public void Format_LongLocalPath_UsesWindowsPrefix()
    {
        PlaylistPathFormatter.Format(
                @"C:\Videos\Movie.mkv",
                @"C:\Videos",
                PathMode.Long,
                isWindows: true)
            .Should().Be(@"\\?\C:\Videos\Movie.mkv");
    }

    [Fact]
    public void Format_LongUncPath_UsesWindowsUncPrefix()
    {
        PlaylistPathFormatter.Format(
                @"\\server\share\Movies\Movie.mkv",
                @"\\server\share\Movies",
                PathMode.Long,
                isWindows: true)
            .Should().Be(@"\\?\UNC\server\share\Movies\Movie.mkv");
    }

    [Fact]
    public void Format_LongPathOnNonWindows_ReturnsFullPathWithoutPrefix()
    {
        PlaylistPathFormatter.Format(
                "/videos/Movie.mkv",
                "/videos",
                PathMode.Long,
                isWindows: false)
            .Should().Be("/videos/Movie.mkv");
    }
}
