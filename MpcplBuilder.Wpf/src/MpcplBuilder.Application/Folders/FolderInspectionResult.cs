using MpcplBuilder.Application.Abstractions;

namespace MpcplBuilder.Application.Folders;

public enum FolderInspectionStatus
{
    InvalidFolder,
    NoVideos,
    Ready,
    AccessFailure
}

public sealed record FolderInspectionResult(
    string RootPath,
    FolderInspectionStatus Status,
    bool HasVideos,
    PlaylistOutputMetadata? ExistingOutput,
    string? ErrorMessage);
