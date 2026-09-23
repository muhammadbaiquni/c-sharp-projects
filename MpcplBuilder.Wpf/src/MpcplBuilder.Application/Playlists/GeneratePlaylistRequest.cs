using MpcplBuilder.Domain.Playlists;

namespace MpcplBuilder.Application.Playlists;

public sealed record GeneratePlaylistRequest(string RootPath, PathMode PathMode)
{
    public static GeneratePlaylistRequest Relative(string rootPath) => new(rootPath, PathMode.Relative);
    public static GeneratePlaylistRequest Full(string rootPath) => new(rootPath, PathMode.Full);
    public static GeneratePlaylistRequest Long(string rootPath) => new(rootPath, PathMode.Long);
}
