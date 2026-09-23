using MpcplBuilder.Domain.Playlists;

namespace MpcplBuilder.Application.Playlists;

public sealed record GeneratePlaylistRequest(string RootPath, PathMode PathMode);
