namespace MpcplBuilder.Application.Explorer;

public sealed record ExplorerFolder(string Path, string DisplayName, bool HasPlaylist, bool IsAvailable);
