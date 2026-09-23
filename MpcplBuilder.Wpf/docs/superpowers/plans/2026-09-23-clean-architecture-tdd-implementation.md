# MPC Playlist Builder Clean Architecture and TDD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the existing single-project WPF playlist builder into the approved four-layer Clean Architecture solution with xUnit coverage and the familiar UI layout preserved.

**Architecture:** Domain contains immutable playlist concepts and deterministic policies; Application owns use cases and external-effect ports; Infrastructure implements physical filesystem and MPCPL output; WPF composes dependencies and owns UI state and dialogs. Migration proceeds in buildable, test-first slices until the legacy static builder and command harness can be removed.

**Tech Stack:** .NET 8, WPF, CommunityToolkit.Mvvm 8.4.0, xUnit, FluentAssertions 7.2.0

**Spec:** `docs/superpowers/specs/2026-09-23-clean-architecture-tdd-design.md`

## Global Constraints

- Preserve the current single-window workflow and recognizable control layout.
- `MpcplBuilder.Domain`, `MpcplBuilder.Application`, and `MpcplBuilder.Infrastructure` target `net8.0`; `MpcplBuilder.Wpf` targets `net8.0-windows`.
- Domain has no project references; Application references Domain; Infrastructure references Application and Domain; WPF references Application and Infrastructure.
- Domain and Application contain no WPF, Windows Forms, MessageBox, or physical filesystem APIs.
- Use xUnit and FluentAssertions; use handwritten fakes rather than a mocking framework.
- Use `CancellationToken` through inspection, traversal, matching, serialization, and writing.
- Keep MPCPL compatibility: UTF-8 BOM, CRLF, `MPCPLAYLIST` header, one-based numbering, and subtitle rows.
- Do not add a wizard, dashboard, dark theme, DI container, MediatR, persistence, telemetry, playback, or unrelated features.

## Review Focus

- A folder is selected twice quickly: only the second inspection may update ViewModel state; covered by Task 6 stale-result test.
- A subtitle begins with the video name but has no dot separator (`MovieTrailer.srt`): it must not match `Movie.mkv`; covered by Task 3 matching tests.
- An existing playlist is found and overwrite is declined: Generate remains disabled and no output write occurs; covered by Task 6 command-state test.
- A UNC path is requested in Long mode: output must use `\\?\UNC\server\share\...`; covered by Task 3 path tests.
- A child directory enumeration throws `UnauthorizedAccessException`: discovery skips that child and returns accessible media; covered by Task 5's injected traversal test.

---

### Task 1: Establish the `src` and `tests` Solution Layout

**Files:**
- Move: existing WPF source and assets into `src/MpcplBuilder.Wpf/`
- Move: `MpcplBuilder.Wpf.Tests/` into `tests/MpcplBuilder.Wpf.Tests/`
- Modify: `MpcplBuilder.Wpf.sln`
- Modify: `src/MpcplBuilder.Wpf/MpcplBuilder.Wpf.csproj`
- Modify: `tests/MpcplBuilder.Wpf.Tests/MpcplBuilder.Wpf.Tests.csproj`

**Interfaces:**
- Consumes: the current application and executable regression harness.
- Produces: a buildable solution with conventional `src` and `tests` roots while behavior remains unchanged.

- [ ] **Step 1: Record the green baseline**

Run:

```powershell
dotnet build MpcplBuilder.Wpf.sln -t:Rebuild
dotnet run --project MpcplBuilder.Wpf.Tests/MpcplBuilder.Wpf.Tests.csproj --no-build
```

Expected: build succeeds; both existing regression checks print `PASS`.

- [ ] **Step 2: Move files without changing namespaces or behavior**

Use `git mv` for tracked files. The production project becomes `src/MpcplBuilder.Wpf/MpcplBuilder.Wpf.csproj`; the current test harness becomes `tests/MpcplBuilder.Wpf.Tests/MpcplBuilder.Wpf.Tests.csproj`. Update project references to:

```xml
<ProjectReference Include="..\..\src\MpcplBuilder.Wpf\MpcplBuilder.Wpf.csproj" />
```

Update solution project paths and remove the obsolete `Compile Remove` item because tests no longer live below the WPF project directory.

- [ ] **Step 3: Verify the structural move**

Run:

```powershell
dotnet build MpcplBuilder.Wpf.sln -t:Rebuild
dotnet run --project tests/MpcplBuilder.Wpf.Tests/MpcplBuilder.Wpf.Tests.csproj --no-build
```

Expected: the same baseline succeeds from the new paths.

- [ ] **Step 4: Commit the layout**

```powershell
git add MpcplBuilder.Wpf.sln src tests
git commit -m "refactor: organize playlist builder solution"
```

### Task 2: Replace the Console Harness with Characterization Tests

**Files:**
- Modify: `tests/MpcplBuilder.Wpf.Tests/MpcplBuilder.Wpf.Tests.csproj`
- Delete: `tests/MpcplBuilder.Wpf.Tests/Program.cs`
- Create: `tests/MpcplBuilder.Wpf.Tests/PlaylistBuilderCharacterizationTests.cs`
- Create: `tests/MpcplBuilder.Wpf.Tests/MainWindowViewModelCharacterizationTests.cs`
- Create: `tests/MpcplBuilder.Wpf.Tests/TemporaryDirectory.cs`

**Interfaces:**
- Consumes: `PlaylistBuilder.BuildEntries`, `PlaylistBuilder.WriteMpcpl`, and the current ViewModel behavior.
- Produces: repeatable xUnit characterization coverage that guards the migration.

- [ ] **Step 1: Convert the test project to xUnit**

Set `IsTestProject` and add exact packages:

```xml
<PropertyGroup>
  <TargetFramework>net8.0-windows</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <UseWPF>true</UseWPF>
  <IsTestProject>true</IsTestProject>
  <IsPackable>false</IsPackable>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="FluentAssertions" Version="7.2.0" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
  <PackageReference Include="xunit" Version="2.9.3" />
  <PackageReference Include="xunit.runner.visualstudio" Version="3.0.2">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

- [ ] **Step 2: Write characterization tests before moving logic**

Use real temporary files. The core output test must pin exact bytes and rows:

```csharp
[Fact]
public void WriteMpcpl_WritesCompatibleHeaderNumberingAndSubtitle()
{
    using var temp = new TemporaryDirectory();
    var video = temp.CreateFile("Movie.mkv");
    var subtitle = temp.CreateFile("Movie.en.srt");
    var output = temp.PathFor("Playlist.mpcpl");

    PlaylistBuilder.WriteMpcpl(output,
        [new PlaylistBuilder.PlaylistEntry
        {
            VideoPath = video,
            SubtitlePaths = [subtitle]
        }], temp.Path, PlaylistBuilder.PathMode.Relative);

    File.ReadAllText(output).Should().Be(
        "MPCPLAYLIST\r\n1,type,0\r\n1,filename,Movie.mkv\r\n1,subtitle,Movie.en.srt\r\n");
    File.ReadAllBytes(output).Should().StartWith([0xEF, 0xBB, 0xBF]);
}
```

Add tests for recursive discovery, exact/language subtitle matching, and one scan per folder selection.

- [ ] **Step 3: Run tests and confirm they pass against legacy code**

Run: `dotnet test MpcplBuilder.Wpf.sln --no-restore`

Expected: all characterization tests pass.

- [ ] **Step 4: Commit the characterization suite**

```powershell
git add tests/MpcplBuilder.Wpf.Tests
git commit -m "test: characterize playlist builder behavior"
```

### Task 3: Introduce Domain Models and Policies Test-First

**Files:**
- Create: `src/MpcplBuilder.Domain/MpcplBuilder.Domain.csproj`
- Create: `src/MpcplBuilder.Domain/Playlists/PlaylistEntry.cs`
- Create: `src/MpcplBuilder.Domain/Playlists/PathMode.cs`
- Create: `src/MpcplBuilder.Domain/Media/SupportedMedia.cs`
- Create: `src/MpcplBuilder.Domain/Media/SubtitleMatchingPolicy.cs`
- Create: `src/MpcplBuilder.Domain/Playlists/PlaylistPathFormatter.cs`
- Create: `tests/MpcplBuilder.Domain.Tests/MpcplBuilder.Domain.Tests.csproj`
- Create: `tests/MpcplBuilder.Domain.Tests/SupportedMediaTests.cs`
- Create: `tests/MpcplBuilder.Domain.Tests/SubtitleMatchingPolicyTests.cs`
- Create: `tests/MpcplBuilder.Domain.Tests/PlaylistPathFormatterTests.cs`
- Modify: `MpcplBuilder.Wpf.sln`

**Interfaces:**
- Produces: `PlaylistEntry(string VideoPath, IReadOnlyList<string> SubtitlePaths)`, `PathMode`, `SupportedMedia.IsVideo/IsSubtitle`, `SubtitleMatchingPolicy.IsMatch`, and `PlaylistPathFormatter.Format`.

- [ ] **Step 1: Scaffold Domain and its test project**

```powershell
dotnet new classlib -n MpcplBuilder.Domain -o src/MpcplBuilder.Domain -f net8.0
dotnet new xunit -n MpcplBuilder.Domain.Tests -o tests/MpcplBuilder.Domain.Tests -f net8.0
dotnet add tests/MpcplBuilder.Domain.Tests reference src/MpcplBuilder.Domain
dotnet add tests/MpcplBuilder.Domain.Tests package FluentAssertions --version 7.2.0
dotnet sln MpcplBuilder.Wpf.sln add src/MpcplBuilder.Domain tests/MpcplBuilder.Domain.Tests
```

- [ ] **Step 2: Write failing media and subtitle policy tests**

```csharp
[Theory]
[InlineData("Movie.srt", "Movie.mkv", true)]
[InlineData("Movie.en.srt", "Movie.mkv", true)]
[InlineData("MovieTrailer.srt", "Movie.mkv", false)]
public void IsMatch_UsesExactNameOrDotSuffix(string subtitle, string video, bool expected)
{
    SubtitleMatchingPolicy.IsMatch(video, subtitle).Should().Be(expected);
}
```

Run: `dotnet test tests/MpcplBuilder.Domain.Tests --no-restore`

Expected: FAIL because Domain policies do not exist.

- [ ] **Step 3: Implement immutable models and media policies**

```csharp
public sealed record PlaylistEntry(string VideoPath, IReadOnlyList<string> SubtitlePaths);

public enum PathMode { Relative, Full, Long }

public static class SubtitleMatchingPolicy
{
    public static bool IsMatch(string videoPath, string subtitlePath)
    {
        var videoName = Path.GetFileNameWithoutExtension(videoPath);
        var subtitleName = Path.GetFileNameWithoutExtension(subtitlePath);
        return subtitleName.Equals(videoName, StringComparison.OrdinalIgnoreCase)
            || subtitleName.StartsWith(videoName + ".", StringComparison.OrdinalIgnoreCase);
    }
}
```

Implement extension sets as private case-insensitive `HashSet<string>` values exposed through predicate methods.

- [ ] **Step 4: Write failing path-formatting tests**

Cover relative paths under root, full paths, files outside root, long local paths, and UNC conversion:

```csharp
[Fact]
public void Format_LongUncPath_UsesWindowsUncPrefix()
{
    PlaylistPathFormatter.Format(
        @"\\server\share\Movies\Movie.mkv",
        @"\\server\share\Movies",
        PathMode.Long,
        isWindows: true)
        .Should().Be(@"\\?\UNC\server\share\Movies\Movie.mkv");
}
```

Run the Domain tests and confirm the new path tests fail.

- [ ] **Step 5: Implement `PlaylistPathFormatter` and make Domain tests green**

Signature:

```csharp
public static string Format(string filePath, string rootPath, PathMode mode, bool isWindows)
```

Run: `dotnet test tests/MpcplBuilder.Domain.Tests --no-restore`

Expected: all Domain tests pass.

- [ ] **Step 6: Commit Domain**

```powershell
git add src/MpcplBuilder.Domain tests/MpcplBuilder.Domain.Tests MpcplBuilder.Wpf.sln
git commit -m "feat: add playlist domain policies"
```

### Task 4: Add Application Ports and Use Cases Test-First

**Files:**
- Create: `src/MpcplBuilder.Application/MpcplBuilder.Application.csproj`
- Create: `src/MpcplBuilder.Application/Abstractions/IMediaFileRepository.cs`
- Create: `src/MpcplBuilder.Application/Abstractions/IPlaylistOutput.cs`
- Create: `src/MpcplBuilder.Application/Folders/InspectFolder.cs`
- Create: `src/MpcplBuilder.Application/Folders/IInspectFolder.cs`
- Create: `src/MpcplBuilder.Application/Folders/FolderInspectionResult.cs`
- Create: `src/MpcplBuilder.Application/Playlists/GeneratePlaylist.cs`
- Create: `src/MpcplBuilder.Application/Playlists/IGeneratePlaylist.cs`
- Create: `src/MpcplBuilder.Application/Playlists/GeneratePlaylistRequest.cs`
- Create: `src/MpcplBuilder.Application/Playlists/PlaylistGenerationResult.cs`
- Create: `tests/MpcplBuilder.Application.Tests/Fakes/FakeMediaFileRepository.cs`
- Create: `tests/MpcplBuilder.Application.Tests/Fakes/FakePlaylistOutput.cs`
- Create: `tests/MpcplBuilder.Application.Tests/InspectFolderTests.cs`
- Create: `tests/MpcplBuilder.Application.Tests/GeneratePlaylistTests.cs`

**Interfaces:**
- Consumes: Domain `PlaylistEntry`, `PathMode`, and matching policies.
- Produces: the following exact contracts:

```csharp
public interface IMediaFileRepository
{
    bool DirectoryExists(string rootPath);
    Task<bool> HasAnyVideoAsync(string rootPath, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetVideosAsync(string rootPath, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetMatchingSubtitlesAsync(string videoPath, CancellationToken cancellationToken);
}

public sealed record PlaylistOutputMetadata(DateTime LastWriteTime, long Length);

public interface IPlaylistOutput
{
    Task<PlaylistOutputMetadata?> GetMetadataAsync(string rootPath, CancellationToken cancellationToken);
    Task<string> WriteAsync(string rootPath, IReadOnlyList<PlaylistEntry> entries, PathMode pathMode, CancellationToken cancellationToken);
}

public interface IInspectFolder
{
    Task<FolderInspectionResult> ExecuteAsync(string rootPath, CancellationToken cancellationToken);
}

public interface IGeneratePlaylist
{
    Task<PlaylistGenerationResult> ExecuteAsync(GeneratePlaylistRequest request, CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Scaffold Application and test projects with correct references**

Create both projects targeting `net8.0`; Application references Domain, and Application.Tests references Application and Domain. Add them to the solution and add FluentAssertions 7.2.0 to tests.

- [ ] **Step 2: Write failing folder inspection tests**

Required cases: empty root, missing root, valid root with no video, valid root with video, existing playlist metadata, access failure, and cancellation.

```csharp
[Fact]
public async Task ExecuteAsync_WhenVideoExists_ReturnsReady()
{
    var media = new FakeMediaFileRepository { RootExists = true, HasAnyVideo = true };
    var output = new FakePlaylistOutput();
    var useCase = new InspectFolder(media, output);

    var result = await useCase.ExecuteAsync(@"D:\Media", CancellationToken.None);

    result.Status.Should().Be(FolderInspectionStatus.Ready);
    result.HasVideos.Should().BeTrue();
}
```

Run Application tests and confirm failure because ports/use case are absent.

- [ ] **Step 3: Implement inspection ports, result, and use case minimally**

Use these explicit status enums and immutable records:

```csharp
public enum FolderInspectionStatus { InvalidFolder, NoVideos, Ready, AccessFailure }

public sealed record FolderInspectionResult(
    string RootPath,
    FolderInspectionStatus Status,
    bool HasVideos,
    PlaylistOutputMetadata? ExistingOutput,
    string? ErrorMessage);

public enum PlaylistGenerationStatus { Success, NoVideos, AccessFailure, OutputFailure }

public sealed record PlaylistGenerationResult(
    PlaylistGenerationStatus Status,
    string? OutputPath,
    IReadOnlyList<PlaylistEntry> Entries,
    string? ErrorMessage);
```

Do not catch `OperationCanceledException`; allow cancellation to propagate. Map `UnauthorizedAccessException` and `IOException` into the defined failure results with actionable messages.

- [ ] **Step 4: Write failing generation tests**

Cover sorted entries, subtitle matching, selected `PathMode`, preview result, cancellation, and writer failure. Verify output is never called when no video entries exist.

- [ ] **Step 5: Implement `GeneratePlaylist` minimally**

```csharp
public sealed record GeneratePlaylistRequest(string RootPath, PathMode PathMode);

public sealed class GeneratePlaylist
{
    public Task<PlaylistGenerationResult> ExecuteAsync(
        GeneratePlaylistRequest request,
        CancellationToken cancellationToken);
}
```

Sort video paths and subtitle paths with `StringComparer.OrdinalIgnoreCase` before creating the output request.

- [ ] **Step 6: Run Application and Domain suites**

Run: `dotnet test MpcplBuilder.Wpf.sln --no-restore`

Expected: Domain, Application, and characterization tests pass.

- [ ] **Step 7: Commit Application**

```powershell
git add src/MpcplBuilder.Application tests/MpcplBuilder.Application.Tests MpcplBuilder.Wpf.sln
git commit -m "feat: add playlist application use cases"
```

### Task 5: Implement Filesystem Infrastructure with Integration Tests

**Files:**
- Create: `src/MpcplBuilder.Infrastructure/MpcplBuilder.Infrastructure.csproj`
- Create: `src/MpcplBuilder.Infrastructure/Files/PhysicalMediaFileRepository.cs`
- Create: `src/MpcplBuilder.Infrastructure/Files/DirectoryTraversal.cs`
- Create: `src/MpcplBuilder.Infrastructure/Properties/AssemblyInfo.cs`
- Create: `src/MpcplBuilder.Infrastructure/Playlists/MpcplPlaylistOutput.cs`
- Create: `tests/MpcplBuilder.Infrastructure.Tests/PhysicalMediaFileRepositoryTests.cs`
- Create: `tests/MpcplBuilder.Infrastructure.Tests/MpcplPlaylistOutputTests.cs`
- Create: `tests/MpcplBuilder.Infrastructure.Tests/TemporaryDirectory.cs`

**Interfaces:**
- Consumes: `IMediaFileRepository`, `IPlaylistOutput`, Domain policies and models.
- Produces: physical adapters used by WPF composition.

- [ ] **Step 1: Scaffold projects and references**

Infrastructure references Application and Domain. Infrastructure.Tests references Infrastructure, Application, and Domain. Add FluentAssertions and add both projects to the solution.

- [ ] **Step 2: Write failing recursive repository integration tests**

Create real nested video/subtitle/unrelated files. Assert case-insensitive extension handling, matching subtitles, deterministic results, and cancellation. Give `DirectoryTraversal` an internal constructor accepting directory/file enumeration delegates; use it to make one child throw `UnauthorizedAccessException` and assert accessible sibling media is still returned. Expose internals only to `MpcplBuilder.Infrastructure.Tests` with `InternalsVisibleTo`.

- [ ] **Step 3: Implement safe iterative traversal**

Use a stack, catch `UnauthorizedAccessException`, `DirectoryNotFoundException`, and `IOException` per child enumeration, and check cancellation before each directory and file. Do not use a broad empty catch.

- [ ] **Step 4: Write failing MPCPL output integration tests**

Assert exact text, BOM bytes, CRLF-only newlines, numbering for multiple videos, path modes, metadata lookup, and surfaced write failure.

- [ ] **Step 5: Implement `MpcplPlaylistOutput`**

Open one owned `FileStream`, write with `new UTF8Encoding(true)`, set `StreamWriter.NewLine = "\r\n"`, and format paths through Domain `PlaylistPathFormatter`.

- [ ] **Step 6: Run the full suite**

Run: `dotnet test MpcplBuilder.Wpf.sln --no-restore`

Expected: all tests pass, including legacy characterization tests.

- [ ] **Step 7: Commit Infrastructure**

```powershell
git add src/MpcplBuilder.Infrastructure tests/MpcplBuilder.Infrastructure.Tests MpcplBuilder.Wpf.sln
git commit -m "feat: add filesystem playlist infrastructure"
```

### Task 6: Migrate the WPF ViewModel to CommunityToolkit.Mvvm

**Files:**
- Modify: `src/MpcplBuilder.Wpf/MpcplBuilder.Wpf.csproj`
- Create: `src/MpcplBuilder.Wpf/Services/IFolderPicker.cs`
- Create: `src/MpcplBuilder.Wpf/Services/IUserDialogService.cs`
- Create: `src/MpcplBuilder.Wpf/Services/WindowsFolderPicker.cs`
- Create: `src/MpcplBuilder.Wpf/Services/WpfUserDialogService.cs`
- Rewrite: `src/MpcplBuilder.Wpf/MainWindowViewModel.cs`
- Modify: `src/MpcplBuilder.Wpf/App.xaml.cs`
- Modify: `src/MpcplBuilder.Wpf/MainWindow.xaml.cs`
- Create: `tests/MpcplBuilder.Wpf.Tests/Fakes/FakeFolderPicker.cs`
- Create: `tests/MpcplBuilder.Wpf.Tests/Fakes/FakeUserDialogService.cs`
- Create: `tests/MpcplBuilder.Wpf.Tests/Fakes/FakeInspectFolder.cs`
- Create: `tests/MpcplBuilder.Wpf.Tests/Fakes/FakeGeneratePlaylist.cs`
- Create: `tests/MpcplBuilder.Wpf.Tests/MainWindowViewModelTests.cs`

**Interfaces:**
- Consumes: `InspectFolder`, `GeneratePlaylist`, Infrastructure adapters.
- Produces: observable UI state and commands with no direct filesystem or static builder calls.

- [ ] **Step 1: Add CommunityToolkit.Mvvm**

Run:

```powershell
dotnet add src/MpcplBuilder.Wpf package CommunityToolkit.Mvvm --version 8.4.0
```

- [ ] **Step 2: Write failing ViewModel command/state tests**

Cover initial disabled Generate, enabled Generate after a ready inspection, disabled for no video, active work, or unconfirmed output; browse calls inspection once; clear; cancel; dialog confirmation; and error display.

`FakeInspectFolder` exposes a queue of `Task<FolderInspectionResult>` responses so tests can control completion order. For stale operation protection, use two controllable inspection tasks:

```csharp
[Fact]
public async Task BrowseAsync_WhenOlderInspectionFinishesLast_DoesNotReplaceLatestState()
{
    var first = new TaskCompletionSource<FolderInspectionResult>();
    var second = new TaskCompletionSource<FolderInspectionResult>();
    var inspectFolder = new FakeInspectFolder(first.Task, second.Task);
    var vm = new MainWindowViewModel(
        inspectFolder,
        new FakeGeneratePlaylist(),
        new FakeFolderPicker(),
        new FakeUserDialogService());

    var firstRun = vm.SelectFolderAsync(@"D:\First");
    var secondRun = vm.SelectFolderAsync(@"D:\Second");
    second.SetResult(FolderInspectionResult.Ready(@"D:\Second", hasVideos: true));
    await secondRun;
    first.SetResult(FolderInspectionResult.NoVideos(@"D:\First"));
    await firstRun;

    vm.RootPath.Should().Be(@"D:\Second");
    vm.HasVideos.Should().BeTrue();
}
```

Run WPF tests and confirm they fail against the legacy ViewModel API.

- [ ] **Step 3: Implement UI service interfaces and adapters**

Keep `FolderBrowserDialog` and `MessageBox` inside adapter classes only. ViewModel methods receive folder paths and confirmation results through interfaces.

- [ ] **Step 4: Rewrite ViewModel with generated properties and commands**

Derive from `ObservableObject`. Use `[ObservableProperty]`, `[RelayCommand]`, and `[NotifyCanExecuteChangedFor]`. Store separate cancellation sources for inspection and generation. Increment an inspection version before each selection and verify it before applying results.

- [ ] **Step 5: Compose dependencies in `App.xaml.cs`**

Construct `PhysicalMediaFileRepository`, `MpcplPlaylistOutput`, both use cases, dialog adapters, and `MainWindowViewModel`; assign the ViewModel to `MainWindow.DataContext` before showing the window.

- [ ] **Step 6: Run WPF and full suites**

Run:

```powershell
dotnet test MpcplBuilder.Wpf.sln --no-restore
dotnet build MpcplBuilder.Wpf.sln --no-restore
```

Expected: command/state tests and all earlier suites pass.

- [ ] **Step 7: Commit the WPF migration**

```powershell
git add src/MpcplBuilder.Wpf tests/MpcplBuilder.Wpf.Tests
git commit -m "refactor: migrate WPF workflow to application use cases"
```

### Task 7: Clean Up the Existing XAML Layout Without Redesigning It

**Files:**
- Create: `src/MpcplBuilder.Wpf/Styles/Controls.xaml`
- Modify: `src/MpcplBuilder.Wpf/App.xaml`
- Modify: `src/MpcplBuilder.Wpf/MainWindow.xaml`
- Create: `tests/MpcplBuilder.Wpf.Tests/MainWindowBindingTests.cs`

**Interfaces:**
- Consumes: the Task 6 ViewModel properties and commands.
- Produces: the familiar layout with consistent resources, accessibility metadata, and valid bindings.

- [ ] **Step 1: Write a failing binding smoke test**

Create the window on an STA test thread, assign a real ViewModel with fakes, call `ApplyTemplate`, and assert no binding error is emitted for root path, path mode, output info, preview, progress, status, and commands. Also assert named primary controls expose `AutomationProperties.Name`.

- [ ] **Step 2: Extract shared styles and clean spacing**

Create resources for spacing, label text, primary/secondary buttons, inputs, group boxes, focus visuals, and status colors. Preserve the five existing vertical sections and all existing actions.

- [ ] **Step 3: Improve accessibility and binding clarity**

Add explicit labels/automation names, tab order, `Mode=TwoWay` only where input is intended, and `UpdateSourceTrigger=PropertyChanged` for root input. Do not introduce new navigation or visual concepts.

- [ ] **Step 4: Run WPF tests and build**

Run:

```powershell
dotnet test tests/MpcplBuilder.Wpf.Tests --no-restore
dotnet build MpcplBuilder.Wpf.sln --no-restore
```

Expected: binding smoke test passes and build has no new warnings.

- [ ] **Step 5: Commit XAML cleanup**

```powershell
git add src/MpcplBuilder.Wpf/Styles src/MpcplBuilder.Wpf/App.xaml src/MpcplBuilder.Wpf/MainWindow.xaml tests/MpcplBuilder.Wpf.Tests
git commit -m "style: clean up playlist builder window"
```

### Task 8: Remove Legacy Code, Update Documentation, and Verify the Migration

**Files:**
- Delete: `src/MpcplBuilder.Wpf/PlaylistBuilder.cs`
- Delete: `src/MpcplBuilder.Wpf/AsyncRelayCommand.cs`
- Delete: obsolete characterization tests that only target removed private structure
- Modify: `README.md`
- Modify: `MpcplBuilder.Wpf.sln`

**Interfaces:**
- Consumes: all migrated layers and tests.
- Produces: the final clean solution with no duplicate implementation.

- [ ] **Step 1: Prove legacy code is no longer referenced**

Run:

```powershell
rg -n "PlaylistBuilder|AsyncRelayCommand" src tests
```

Expected: code references exist only in `PlaylistBuilder.cs`, `AsyncRelayCommand.cs`, and characterization tests explicitly scheduled for deletion.

- [ ] **Step 2: Delete legacy implementation and obsolete structural tests**

Remove the static builder and custom command only after equivalent Domain, Application, Infrastructure, and ViewModel tests are green.

- [ ] **Step 3: Update README**

Document the `src`/`tests` structure and exact commands:

```powershell
dotnet restore MpcplBuilder.Wpf.sln
dotnet build MpcplBuilder.Wpf.sln --no-restore
dotnet test MpcplBuilder.Wpf.sln --no-build
dotnet run --project src/MpcplBuilder.Wpf/MpcplBuilder.Wpf.csproj
```

- [ ] **Step 4: Run final verification**

```powershell
dotnet restore MpcplBuilder.Wpf.sln
dotnet test MpcplBuilder.Wpf.sln -c Release --no-restore
dotnet build MpcplBuilder.Wpf.sln -c Release --no-restore
```

Expected: all test projects pass; Release build succeeds; no old project path or legacy implementation remains.

- [ ] **Step 5: Review dependency direction**

Run:

```powershell
dotnet list src/MpcplBuilder.Domain reference
dotnet list src/MpcplBuilder.Application reference
dotnet list src/MpcplBuilder.Infrastructure reference
dotnet list src/MpcplBuilder.Wpf reference
```

Expected: references exactly match the dependency rules in the spec.

- [ ] **Step 6: Commit completion**

```powershell
git add MpcplBuilder.Wpf.sln src tests README.md
git commit -m "docs: finalize clean architecture migration"
```
