namespace MpcplBuilder.Application.Explorer;

public interface ILoadExplorerChildren
{
    Task<ExplorerLoadResult> ExecuteAsync(string path, CancellationToken cancellationToken);
}
