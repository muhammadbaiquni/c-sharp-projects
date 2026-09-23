using FluentAssertions;
using MpcplBuilder.Application.Abstractions;
using MpcplBuilder.Application.Folders;
using MpcplBuilder.Application.Playlists;
using MpcplBuilder.Domain.Playlists;
using MpcplBuilder.Wpf.Tests.Fakes;
using MpcplBuilder.Wpf.ViewModels;

namespace MpcplBuilder.Wpf.Tests;

public sealed class DefaultViewModelTests
{
    [Fact]
    public void Initially_GenerateIsDisabled()
    {
        var vm = CreateViewModel();

        vm.GenerateCommand.CanExecute(null).Should().BeFalse();
        vm.Status.Should().Be("Ready");
    }

    [Fact]
    public async Task SelectFolderAsync_Ready_EnablesGenerateAndInspectsOnce()
    {
        var inspect = new FakeInspectFolder(Ready(@"D:\Media"));
        var vm = CreateViewModel(inspect: inspect);

        await vm.SelectFolderAsync(@"D:\Media");

        inspect.Calls.Should().Be(1);
        vm.HasVideos.Should().BeTrue();
        vm.GenerateCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task SelectFolderAsync_NoVideos_KeepsGenerateDisabled()
    {
        var result = new FolderInspectionResult(@"D:\Empty", FolderInspectionStatus.NoVideos, false, null, null);
        var vm = CreateViewModel(inspect: new FakeInspectFolder(Task.FromResult(result)));

        await vm.SelectFolderAsync(@"D:\Empty");

        vm.GenerateCommand.CanExecute(null).Should().BeFalse();
        vm.Status.Should().Contain("No video");
    }

    [Fact]
    public async Task SelectFolderAsync_InvalidTypedPathUsesInlineStatusWithoutDialog()
    {
        var dialogs = new FakeUserDialogService();
        var result = new FolderInspectionResult("x", FolderInspectionStatus.InvalidFolder, false, null, "invalid");
        var vm = CreateViewModel(new FakeInspectFolder(Task.FromResult(result)), dialogs: dialogs);

        await vm.SelectFolderAsync("x");

        vm.Status.Should().Be("Invalid folder");
        dialogs.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancel_DuringInspectionCancelsTokenAndWaitsForCompletion()
    {
        var pending = new TaskCompletionSource<FolderInspectionResult>();
        var inspect = new FakeInspectFolder(pending.Task);
        var vm = CreateViewModel(inspect);
        var inspection = vm.SelectFolderAsync(@"D:\Media");

        vm.IsBusy.Should().BeTrue();
        vm.CancelCommand.Execute(null);
        inspect.Tokens.Single().IsCancellationRequested.Should().BeTrue();
        vm.IsBusy.Should().BeTrue();
        pending.SetCanceled(inspect.Tokens.Single());
        await inspection;
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task SelectFolderAsync_ExistingOutputRequiresConfirmation()
    {
        var metadata = new PlaylistOutputMetadata(DateTime.Now, 42);
        var result = new FolderInspectionResult(@"D:\Media", FolderInspectionStatus.Ready, true, metadata, null);
        var dialogs = new FakeUserDialogService { ConfirmOverwriteResult = false };
        var vm = CreateViewModel(new FakeInspectFolder(Task.FromResult(result)), dialogs: dialogs);

        await vm.SelectFolderAsync(@"D:\Media");

        dialogs.ConfirmOverwriteCalls.Should().Be(1);
        vm.GenerateCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task BrowseAsync_UsesPickerAndInspectsExactlyOnce()
    {
        var inspect = new FakeInspectFolder(Ready(@"D:\Picked"));
        var picker = new FakeFolderPicker { SelectedPath = @"D:\Picked" };
        var vm = CreateViewModel(inspect, picker: picker);

        await vm.BrowseCommand.ExecuteAsync(null);

        picker.Calls.Should().Be(1);
        inspect.Calls.Should().Be(1);
    }

    [Fact]
    public async Task SelectFolderAsync_WhenOlderInspectionFinishesLast_DoesNotReplaceLatestState()
    {
        var first = new TaskCompletionSource<FolderInspectionResult>();
        var second = new TaskCompletionSource<FolderInspectionResult>();
        var vm = CreateViewModel(new FakeInspectFolder(first.Task, second.Task));

        var firstRun = vm.SelectFolderAsync(@"D:\First");
        var secondRun = vm.SelectFolderAsync(@"D:\Second");
        second.SetResult((await Ready(@"D:\Second")));
        await secondRun;
        first.SetResult(new FolderInspectionResult(@"D:\First", FolderInspectionStatus.NoVideos, false, null, null));
        await firstRun;

        vm.RootPath.Should().Be(@"D:\Second");
        vm.HasVideos.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_SuccessPopulatesPreviewAndClearRemovesIt()
    {
        var entry = new PlaylistEntry(@"D:\Media\Movie.mkv", [@"D:\Media\Movie.srt"]);
        var generator = new FakeGeneratePlaylist
        {
            Response = Task.FromResult(new PlaylistGenerationResult(
                PlaylistGenerationStatus.Success, @"D:\Media\Playlist.mpcpl", [entry], null))
        };
        var vm = CreateViewModel(generate: generator);
        await vm.SelectFolderAsync(@"D:\Media");

        await vm.GenerateCommand.ExecuteAsync(null);
        vm.PreviewItems.Should().HaveCount(2);

        vm.ClearCommand.Execute(null);
        vm.PreviewItems.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancel_DuringGenerationCancelsTokenAndRestoresIdleState()
    {
        var pending = new TaskCompletionSource<PlaylistGenerationResult>();
        var generator = new FakeGeneratePlaylist { Response = pending.Task };
        var vm = CreateViewModel(generate: generator);
        await vm.SelectFolderAsync(@"D:\Media");

        var generation = vm.GenerateCommand.ExecuteAsync(null);
        vm.CancelCommand.Execute(null);

        generator.LastToken.IsCancellationRequested.Should().BeTrue();
        vm.IsBusy.Should().BeTrue();
        pending.SetCanceled(generator.LastToken);
        await generation;
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_FailureShowsError()
    {
        var dialogs = new FakeUserDialogService();
        var generator = new FakeGeneratePlaylist
        {
            Response = Task.FromResult(new PlaylistGenerationResult(
                PlaylistGenerationStatus.OutputFailure, null, [], "Disk full"))
        };
        var vm = CreateViewModel(generate: generator, dialogs: dialogs);
        await vm.SelectFolderAsync(@"D:\Media");

        await vm.GenerateCommand.ExecuteAsync(null);

        dialogs.Errors.Should().ContainSingle().Which.Should().Be("Disk full");
        vm.Status.Should().Be("Error");
    }

    [Fact]
    public async Task SelectingAnotherFolderWhileGenerationFinishes_DiscardsOldResult()
    {
        var pending = new TaskCompletionSource<PlaylistGenerationResult>();
        var generator = new FakeGeneratePlaylist { Response = pending.Task };
        var vm = CreateViewModel(generate: generator);
        await vm.SelectFolderAsync(@"D:\First");
        var generation = vm.GenerateCommand.ExecuteAsync(null);

        await vm.SelectFolderAsync(@"D:\Second");
        pending.SetResult(new PlaylistGenerationResult(
            PlaylistGenerationStatus.Success, @"D:\First\Playlist.mpcpl",
            [new PlaylistEntry(@"D:\First\Movie.mkv", [])], null));
        await generation;

        vm.RootPath.Should().Be(@"D:\Second");
        vm.PreviewItems.Should().BeEmpty();
    }

    private static Task<FolderInspectionResult> Ready(string path) => Task.FromResult(
        new FolderInspectionResult(path, FolderInspectionStatus.Ready, true, null, null));

    private static DefaultViewModel CreateViewModel(
        FakeInspectFolder? inspect = null,
        FakeGeneratePlaylist? generate = null,
        FakeFolderPicker? picker = null,
        FakeUserDialogService? dialogs = null) => new(
            inspect ?? new FakeInspectFolder(Ready(@"D:\Media")),
            generate ?? new FakeGeneratePlaylist(),
            picker ?? new FakeFolderPicker(),
            dialogs ?? new FakeUserDialogService());
}
