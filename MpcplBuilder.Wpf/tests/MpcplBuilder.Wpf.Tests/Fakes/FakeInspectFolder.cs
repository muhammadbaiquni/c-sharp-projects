using MpcplBuilder.Application.Folders;

namespace MpcplBuilder.Wpf.Tests.Fakes;

internal sealed class FakeInspectFolder(params Task<FolderInspectionResult>[] responses) : IInspectFolder
{
    private readonly Queue<Task<FolderInspectionResult>> _responses = new(responses);
    public int Calls { get; private set; }
    public List<CancellationToken> Tokens { get; } = [];

    public Task<FolderInspectionResult> ExecuteAsync(string rootPath, CancellationToken cancellationToken)
    {
        Calls++;
        Tokens.Add(cancellationToken);
        return _responses.Count > 0
            ? _responses.Dequeue()
            : Task.FromResult(new FolderInspectionResult(rootPath, FolderInspectionStatus.Ready, true, null, null));
    }
}
