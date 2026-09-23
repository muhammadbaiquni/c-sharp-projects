using MpcplBuilder.Application.Abstractions;
using MpcplBuilder.Application.Folders;
using MpcplBuilder.Application.Tests.Fakes;

namespace MpcplBuilder.Application.Tests;

public sealed class InspectFolderTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_EmptyRoot_ReturnsInvalidFolder(string root)
    {
        var result = await CreateUseCase().ExecuteAsync(root, CancellationToken.None);
        result.Status.Should().Be(FolderInspectionStatus.InvalidFolder);
    }

    [Fact]
    public async Task ExecuteAsync_MissingRoot_ReturnsInvalidFolder()
    {
        var media = new FakeMediaFileRepository { RootExists = false };
        var result = await CreateUseCase(media).ExecuteAsync(@"D:\Missing", CancellationToken.None);
        result.Status.Should().Be(FolderInspectionStatus.InvalidFolder);
    }

    [Fact]
    public async Task ExecuteAsync_NoVideo_ReturnsNoVideos()
    {
        var media = new FakeMediaFileRepository { HasAnyVideo = false };
        var result = await CreateUseCase(media).ExecuteAsync(@"D:\Media", CancellationToken.None);
        result.Status.Should().Be(FolderInspectionStatus.NoVideos);
        result.HasVideos.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_VideoAndExistingOutput_ReturnsReadyWithMetadata()
    {
        var metadata = new PlaylistOutputMetadata(new DateTime(2026, 9, 23, 12, 0, 0), 512);
        var output = new FakePlaylistOutput { Metadata = metadata };

        var result = await CreateUseCase(output: output).ExecuteAsync(@"D:\Media", CancellationToken.None);

        result.Status.Should().Be(FolderInspectionStatus.Ready);
        result.HasVideos.Should().BeTrue();
        result.ExistingOutput.Should().Be(metadata);
    }

    [Fact]
    public async Task ExecuteAsync_Unauthorized_ReturnsAccessFailure()
    {
        var media = new FakeMediaFileRepository { ExceptionToThrow = new UnauthorizedAccessException("denied") };
        var result = await CreateUseCase(media).ExecuteAsync(@"D:\Media", CancellationToken.None);
        result.Status.Should().Be(FolderInspectionStatus.AccessFailure);
        result.ErrorMessage.Should().Contain("denied");
    }

    [Fact]
    public async Task ExecuteAsync_Cancelled_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var act = () => CreateUseCase().ExecuteAsync(@"D:\Media", cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static InspectFolder CreateUseCase(
        FakeMediaFileRepository? media = null,
        FakePlaylistOutput? output = null) =>
        new(media ?? new FakeMediaFileRepository(), output ?? new FakePlaylistOutput());
}
