namespace MpcplBuilder.Infrastructure.Explorer;

internal interface IExplorerDriveSnapshot
{
    DriveType Type { get; }
    bool IsReady { get; }
    string RootPath { get; }
}
