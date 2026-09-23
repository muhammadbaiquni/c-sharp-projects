namespace MpcplBuilder.Infrastructure.Explorer;

internal sealed class PhysicalExplorerDriveSnapshot(DriveInfo drive) : IExplorerDriveSnapshot
{
    public DriveType Type => drive.DriveType;
    public bool IsReady => drive.IsReady;
    public string RootPath => drive.RootDirectory.FullName;
}
