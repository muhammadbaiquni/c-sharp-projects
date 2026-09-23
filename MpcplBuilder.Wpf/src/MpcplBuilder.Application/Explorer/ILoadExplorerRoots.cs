namespace MpcplBuilder.Application.Explorer;

public interface ILoadExplorerRoots
{
    Task<ExplorerLoadResult> ExecuteAsync(CancellationToken cancellationToken);
}
