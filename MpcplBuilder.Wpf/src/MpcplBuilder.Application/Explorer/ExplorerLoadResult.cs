namespace MpcplBuilder.Application.Explorer;

public enum ExplorerLoadStatus
{
    Success,
    Missing,
    AccessFailure
}

public sealed record ExplorerLoadResult(
    ExplorerLoadStatus Status,
    IReadOnlyList<ExplorerFolder> Folders,
    string? ErrorMessage);
