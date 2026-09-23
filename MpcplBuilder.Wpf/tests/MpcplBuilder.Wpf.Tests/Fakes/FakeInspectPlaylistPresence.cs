using MpcplBuilder.Application.Explorer;

namespace MpcplBuilder.Wpf.Tests.Fakes;

internal sealed class FakeInspectPlaylistPresence : IInspectPlaylistPresence
{
    public Func<string, CancellationToken, Task<bool>> Handler { get; set; } = (_, _) => Task.FromResult(false);
    public Task<bool> ExecuteAsync(string path, CancellationToken cancellationToken) => Handler(path, cancellationToken);
}
