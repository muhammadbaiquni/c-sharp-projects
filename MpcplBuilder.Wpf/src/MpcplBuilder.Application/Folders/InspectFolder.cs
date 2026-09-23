using MpcplBuilder.Application.Abstractions;

namespace MpcplBuilder.Application.Folders;

public sealed class InspectFolder(
    IMediaFileRepository mediaFiles,
    IPlaylistOutput playlistOutput) : IInspectFolder
{
    public async Task<FolderInspectionResult> ExecuteAsync(
        string rootPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !mediaFiles.DirectoryExists(rootPath))
            return new(rootPath, FolderInspectionStatus.InvalidFolder, false, null, "The selected folder is not valid.");

        try
        {
            var hasVideos = await mediaFiles.HasAnyVideoAsync(rootPath, cancellationToken);
            var metadata = await playlistOutput.GetMetadataAsync(rootPath, cancellationToken);

            return new(
                rootPath,
                hasVideos ? FolderInspectionStatus.Ready : FolderInspectionStatus.NoVideos,
                hasVideos,
                metadata,
                hasVideos ? null : "No supported video files were found.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return new(rootPath, FolderInspectionStatus.AccessFailure, false, null, exception.Message);
        }
    }
}
