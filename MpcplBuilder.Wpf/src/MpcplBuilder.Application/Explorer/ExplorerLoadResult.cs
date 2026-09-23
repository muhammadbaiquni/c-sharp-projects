namespace MpcplBuilder.Application.Explorer;

public enum ExplorerLoadStatus
{
    Success,
    PartialAccess,
    Missing,
    AccessFailure
}

public sealed record ExplorerLoadResult(
    ExplorerLoadStatus Status,
    IReadOnlyList<ExplorerFolder> Folders,
    string? ErrorMessage);
