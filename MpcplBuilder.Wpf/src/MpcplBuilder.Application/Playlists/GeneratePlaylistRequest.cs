using MpcplBuilder.Domain.Playlists;

namespace MpcplBuilder.Application.Playlists;

public sealed record GeneratePlaylistRequest(string RootPath, PathMode PathMode, bool OverwriteExisting = false)
{
    public static GeneratePlaylistRequest Relative(string rootPath, bool overwrite = false) => new(rootPath, PathMode.Relative, overwrite);
    public static GeneratePlaylistRequest Full(string rootPath, bool overwrite = false) => new(rootPath, PathMode.Full, overwrite);
    public static GeneratePlaylistRequest Long(string rootPath, bool overwrite = false) => new(rootPath, PathMode.Long, overwrite);
}
