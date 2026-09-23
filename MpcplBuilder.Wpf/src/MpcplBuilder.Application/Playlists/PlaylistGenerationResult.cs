using MpcplBuilder.Domain.Playlists;

namespace MpcplBuilder.Application.Playlists;

public enum PlaylistGenerationStatus
{
    Success,
    NoVideos,
    AccessFailure,
    OutputFailure
}

public sealed record PlaylistGenerationResult(
    PlaylistGenerationStatus Status,
    string? OutputPath,
    IReadOnlyList<PlaylistEntry> Entries,
    string? ErrorMessage);
