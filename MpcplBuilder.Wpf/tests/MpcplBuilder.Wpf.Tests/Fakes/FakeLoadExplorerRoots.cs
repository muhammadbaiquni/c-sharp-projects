using MpcplBuilder.Application.Explorer;

namespace MpcplBuilder.Wpf.Tests.Fakes;

internal sealed class FakeLoadExplorerRoots : ILoadExplorerRoots
{
    public Func<CancellationToken, Task<ExplorerLoadResult>> Handler { get; set; } =
        _ => Task.FromResult(new ExplorerLoadResult(ExplorerLoadStatus.Success, [], null));
    public List<CancellationToken> Tokens { get; } = [];
    public Task<ExplorerLoadResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        Tokens.Add(cancellationToken);
        return Handler(cancellationToken);
    }
}
