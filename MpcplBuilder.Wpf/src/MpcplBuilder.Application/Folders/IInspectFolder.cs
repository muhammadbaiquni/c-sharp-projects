namespace MpcplBuilder.Application.Folders;

public interface IInspectFolder
{
    Task<FolderInspectionResult> ExecuteAsync(string rootPath, CancellationToken cancellationToken);
}
