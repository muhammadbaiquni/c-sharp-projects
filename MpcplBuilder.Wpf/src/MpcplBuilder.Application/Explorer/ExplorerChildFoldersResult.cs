namespace MpcplBuilder.Application.Explorer;

public sealed record ExplorerChildFoldersResult(
    IReadOnlyList<string> Paths,
    string? AccessWarning);
