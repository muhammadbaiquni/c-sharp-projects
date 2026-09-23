using MpcplBuilder.Application.Explorer;

namespace MpcplBuilder.Wpf.Tests.Fakes;

internal sealed class FakeLoadExplorerChildren : ILoadExplorerChildren
{
    public Func<string, CancellationToken, Task<ExplorerLoadResult>> Handler { get; set; } =
        (_, _) => Task.FromResult(new ExplorerLoadResult(ExplorerLoadStatus.Success, [], null));
    public List<(string Path, CancellationToken Token)> Requests { get; } = [];
    public Task<ExplorerLoadResult> ExecuteAsync(string path, CancellationToken cancellationToken)
    {
        Requests.Add((path, cancellationToken));
        return Handler(path, cancellationToken);
    }
}
