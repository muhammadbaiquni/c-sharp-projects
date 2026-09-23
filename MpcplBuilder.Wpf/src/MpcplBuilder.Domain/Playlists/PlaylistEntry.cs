namespace MpcplBuilder.Domain.Playlists;

public sealed record PlaylistEntry(
    string VideoPath,
    IReadOnlyList<string> SubtitlePaths);
