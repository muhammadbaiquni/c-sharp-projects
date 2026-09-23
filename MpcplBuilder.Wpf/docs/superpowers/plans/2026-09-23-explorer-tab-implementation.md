# Explorer Tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a folder-only, lazy-loaded Explorer tab that generates a playlist for one active folder while preserving the existing workflow in a Default tab.

**Architecture:** Application owns folder-browsing contracts and outcomes, Infrastructure implements them with physical drive and directory APIs, and WPF contains independent Default and Explorer ViewModels beneath a lightweight shell. Explorer reuses `IGeneratePlaylist` and the existing dialog abstraction so generation, overwrite safety, and MPCPL output remain consistent.

**Tech Stack:** .NET 8, WPF, CommunityToolkit.Mvvm 8.4.0, xUnit 2.9.3, FluentAssertions 7.2.0

**Spec:** `docs/superpowers/specs/2026-09-23-explorer-tab-design.md`

## Global Constraints

- Keep the production dependency direction: Domain has no references; Application references Domain; Infrastructure references Application and Domain; WPF references Application and Infrastructure.
- The header tab order is `Explorer | Default`, while Default is initially active.
- Explorer displays folders only, starts at This PC, and permits one active folder.
- Load child folders lazily; never recursively enumerate merely to expand one node.
- A green icon means `Playlist.mpcpl` exists directly in that node; yellow means it does not.
- Both green and yellow folders remain selectable.
- Explorer generation recursively processes the active folder and uses Relative Path by default.
- Keep Default and Explorer state and cancellation independent.
- Preserve the user's current uncommitted `MainWindow.xaml` width adjustment when restructuring the view.
- Do not add Windows Shell integration, a DI container, filesystem watching, multi-selection, file operations, or persistence.
- Every production behavior starts with a failing test and each task ends with a focused commit.

## Review Focus

- A drive disappears between discovery and expansion: the node becomes unavailable without crashing siblings; pinned in Task 2 and Task 4.
- An expanded folder contains an inaccessible child: accessible siblings still appear and the parent reports an actionable partial/access state; pinned in Task 2 and Task 4.
- Refresh runs while an earlier child load completes: the stale result cannot replace refreshed children; pinned in Task 4.
- A playlist is created outside the app after selection but before Generate: generation rechecks and requires overwrite confirmation; pinned in Task 5.
- Two folders share the same display name at different paths: selection and refresh restoration use full paths, not labels; pinned in Task 4.

---

### Task 1: Add Explorer Application Contracts and Use Cases

**Files:**
- Create: `src/MpcplBuilder.Application/Explorer/ExplorerFolder.cs`
- Create: `src/MpcplBuilder.Application/Explorer/ExplorerLoadResult.cs`
- Create: `src/MpcplBuilder.Application/Explorer/IExplorerFileSystem.cs`
- Create: `src/MpcplBuilder.Application/Explorer/ILoadExplorerRoots.cs`
- Create: `src/MpcplBuilder.Application/Explorer/ILoadExplorerChildren.cs`
- Create: `src/MpcplBuilder.Application/Explorer/IInspectPlaylistPresence.cs`
- Create: `src/MpcplBuilder.Application/Explorer/LoadExplorerRoots.cs`
- Create: `src/MpcplBuilder.Application/Explorer/LoadExplorerChildren.cs`
- Create: `src/MpcplBuilder.Application/Explorer/InspectPlaylistPresence.cs`
- Create: `tests/MpcplBuilder.Application.Tests/Fakes/FakeExplorerFileSystem.cs`
- Create: `tests/MpcplBuilder.Application.Tests/Explorer/ExplorerUseCaseTests.cs`

**Interfaces:**
- Consumes: no physical filesystem APIs; Application only uses the port defined here.
- Produces:

```csharp
public sealed record ExplorerFolder(string Path, string DisplayName, bool HasPlaylist, bool IsAvailable);
public enum ExplorerLoadStatus { Success, Missing, AccessFailure }
public sealed record ExplorerLoadResult(ExplorerLoadStatus Status, IReadOnlyList<ExplorerFolder> Folders, string? ErrorMessage);

public interface IExplorerFileSystem
{
    Task<IReadOnlyList<string>> GetDrivesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetChildFoldersAsync(string path, CancellationToken cancellationToken);
    Task<bool> HasPlaylistAsync(string path, CancellationToken cancellationToken);
}

public interface ILoadExplorerRoots
{
    Task<ExplorerLoadResult> ExecuteAsync(CancellationToken cancellationToken);
}

public interface ILoadExplorerChildren
{
    Task<ExplorerLoadResult> ExecuteAsync(string path, CancellationToken cancellationToken);
}

public interface IInspectPlaylistPresence
{
    Task<bool> ExecuteAsync(string path, CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write failing use-case tests**

Cover sorted drive roots, immediate sorted child folders, direct playlist status, missing root, access failure, and cancellation. Include this direct-status assertion:

```csharp
[Fact]
public async Task LoadChildren_MapsDirectPlaylistPresenceForEachFolder()
{
    var fileSystem = new FakeExplorerFileSystem
    {
        Children = { [@"D:\Media"] = [@"D:\Media\A", @"D:\Media\B"] },
        PlaylistFolders = { @"D:\Media\B" }
    };

    var result = await new LoadExplorerChildren(fileSystem)
        .ExecuteAsync(@"D:\Media", CancellationToken.None);

    result.Folders.Select(x => (x.Path, x.HasPlaylist)).Should().Equal(
        (@"D:\Media\A", false), (@"D:\Media\B", true));
}
```

- [ ] **Step 2: Run the Application tests and confirm RED**

Run: `dotnet test tests/MpcplBuilder.Application.Tests --no-restore`

Expected: compilation fails because Explorer contracts and use cases do not exist.

- [ ] **Step 3: Implement immutable results and focused use cases**

Map `DirectoryNotFoundException` and `DriveNotFoundException` to `Missing`, and `UnauthorizedAccessException`/`IOException` to `AccessFailure`. Let `OperationCanceledException` propagate. Sort by display name and then full path with `StringComparer.OrdinalIgnoreCase`.

- [ ] **Step 4: Run Application and full suites**

Run:

```powershell
dotnet test tests/MpcplBuilder.Application.Tests --no-restore
dotnet test MpcplBuilder.Wpf.sln --no-restore
```

Expected: all tests pass.

- [ ] **Step 5: Commit Application Explorer contracts**

```powershell
git add src/MpcplBuilder.Application/Explorer tests/MpcplBuilder.Application.Tests
git commit -m "feat: add explorer application use cases"
```

### Task 2: Implement Physical Explorer Filesystem

**Files:**
- Create: `src/MpcplBuilder.Infrastructure/Explorer/PhysicalExplorerFileSystem.cs`
- Create: `src/MpcplBuilder.Infrastructure/Explorer/DriveCatalog.cs`
- Modify: `src/MpcplBuilder.Infrastructure/Properties/AssemblyInfo.cs`
- Create: `tests/MpcplBuilder.Infrastructure.Tests/Explorer/PhysicalExplorerFileSystemTests.cs`

**Interfaces:**
- Consumes: `IExplorerFileSystem` from Task 1.
- Produces: `PhysicalExplorerFileSystem : IExplorerFileSystem`, with a public production constructor and an internal constructor accepting drive and directory delegates for deterministic tests.

- [ ] **Step 1: Write failing adapter tests**

Use real temporary directories to prove only immediate directories are returned and descendant playlists do not affect a parent. Use injected delegates for removable-drive and inaccessible-child cases:

```csharp
[Fact]
public async Task GetChildFoldersAsync_ReturnsDirectoriesButNotFilesOrGrandchildren()
{
    using var temp = new TemporaryDirectory();
    var child = temp.CreateDirectory("Child");
    temp.CreateDirectory("Child/Grandchild");
    temp.CreateFile("video.mkv");

    var result = await new PhysicalExplorerFileSystem()
        .GetChildFoldersAsync(temp.Path, CancellationToken.None);

    result.Should().Equal(child);
}
```

Add a cancellation-during-enumeration test and a drive delegate test that removes a drive between calls.

- [ ] **Step 2: Run Infrastructure tests and confirm RED**

Run: `dotnet test tests/MpcplBuilder.Infrastructure.Tests --no-restore`

Expected: compilation fails because the adapter does not exist.

- [ ] **Step 3: Implement the adapter**

Use `DriveInfo.GetDrives()` for ready fixed, removable, network, and RAM drives; expose roots as full paths. Use `Directory.EnumerateDirectories(path)` and check cancellation for each result. Determine direct playlist presence only with `File.Exists(Path.Combine(path, "Playlist.mpcpl"))`. Do not recurse in this adapter.

- [ ] **Step 4: Run Infrastructure and full suites**

Run:

```powershell
dotnet test tests/MpcplBuilder.Infrastructure.Tests --no-restore
dotnet test MpcplBuilder.Wpf.sln --no-restore
```

Expected: all tests pass, including disappearing-drive and root-access cases.

- [ ] **Step 5: Commit Infrastructure Explorer adapter**

```powershell
git add src/MpcplBuilder.Infrastructure/Explorer tests/MpcplBuilder.Infrastructure.Tests
git commit -m "feat: add physical folder explorer adapter"
```

### Task 3: Extract the Existing Workflow into the Default Tab

**Files:**
- Rename: `src/MpcplBuilder.Wpf/MainWindowViewModel.cs` to `src/MpcplBuilder.Wpf/ViewModels/DefaultViewModel.cs`
- Create: `src/MpcplBuilder.Wpf/ViewModels/MainWindowViewModel.cs`
- Create: `src/MpcplBuilder.Wpf/Views/DefaultView.xaml`
- Create: `src/MpcplBuilder.Wpf/Views/DefaultView.xaml.cs`
- Modify: `src/MpcplBuilder.Wpf/MainWindow.xaml`
- Modify: `src/MpcplBuilder.Wpf/App.xaml.cs`
- Rename: `tests/MpcplBuilder.Wpf.Tests/MainWindowViewModelTests.cs` to `tests/MpcplBuilder.Wpf.Tests/DefaultViewModelTests.cs`
- Modify: `tests/MpcplBuilder.Wpf.Tests/MainWindowBindingTests.cs`

**Interfaces:**
- Consumes: current `MainWindowViewModel` behavior and constructor dependencies.
- Produces:

```csharp
public sealed class MainWindowViewModel(DefaultViewModel defaultViewModel, ExplorerViewModel explorerViewModel)
{
    public ExplorerViewModel Explorer { get; } = explorerViewModel;
    public DefaultViewModel Default { get; } = defaultViewModel;
    public int SelectedTabIndex { get; set; } = 1;
}
```

During this task, use a minimal placeholder `ExplorerViewModel` and Explorer tab content so the shell compiles; Task 4 replaces it with real behavior.

- [ ] **Step 1: Add failing extraction and shell tests**

Rename existing ViewModel tests to target `DefaultViewModel` without changing their assertions. Add a shell test asserting `SelectedTabIndex == 1`, `Explorer` and `Default` are distinct instances, and the tab headers occur in XAML order `Explorer`, then `Default`.

- [ ] **Step 2: Run WPF tests and confirm RED**

Run: `dotnet test tests/MpcplBuilder.Wpf.Tests --no-restore`

Expected: compilation fails because the extracted types do not exist.

- [ ] **Step 3: Extract without changing Default behavior**

Move the existing grid markup into `DefaultView.xaml`. Preserve every binding, automation name, tab order, style, and the current footer width value from the working tree. Rename the existing ViewModel type and update tests. Create a two-item `TabControl` in `MainWindow.xaml` bound to `SelectedTabIndex`, with Explorer first and Default second.

- [ ] **Step 4: Compose the shell and verify regressions**

Update `App.xaml.cs` to construct `DefaultViewModel`, the temporary Explorer ViewModel, and the shell. Run:

```powershell
dotnet test tests/MpcplBuilder.Wpf.Tests --no-restore
dotnet test MpcplBuilder.Wpf.sln --no-restore
```

Expected: all existing Default behavior remains green and Default starts selected.

- [ ] **Step 5: Commit the tab shell extraction**

```powershell
git add src/MpcplBuilder.Wpf tests/MpcplBuilder.Wpf.Tests
git commit -m "refactor: extract default playlist tab"
```

### Task 4: Build Lazy Folder Node and Explorer ViewModels

**Files:**
- Create: `src/MpcplBuilder.Wpf/ViewModels/FolderNodeViewModel.cs`
- Create: `src/MpcplBuilder.Wpf/ViewModels/ExplorerViewModel.cs`
- Create: `tests/MpcplBuilder.Wpf.Tests/Fakes/FakeLoadExplorerRoots.cs`
- Create: `tests/MpcplBuilder.Wpf.Tests/Fakes/FakeLoadExplorerChildren.cs`
- Create: `tests/MpcplBuilder.Wpf.Tests/Fakes/FakeInspectPlaylistPresence.cs`
- Create: `tests/MpcplBuilder.Wpf.Tests/FolderNodeViewModelTests.cs`
- Create: `tests/MpcplBuilder.Wpf.Tests/ExplorerViewModelTests.cs`

**Interfaces:**
- Consumes: Task 1 use-case interfaces and `IGeneratePlaylist`.
- Produces observable properties `Roots`, `SelectedFolder`, `IsRelativePath`, `IsFullPath`, `IsLongPath`, `IsBusy`, `Status`, plus `InitializeCommand`, `RefreshCommand`, `GenerateCommand`, and `CancelCommand`.

- [ ] **Step 1: Write failing lazy-loading tests**

Pin one request per active load, retry after access failure, preservation after collapse, direct green/yellow status, and cancellation. The duplicate-load test must hold the first task pending and invoke expand twice before completing it.

- [ ] **Step 2: Write failing selection and refresh tests**

Cover Relative as default, Generate disabled without selection, identity by full path when duplicate labels exist, expanded-path restoration, removed drive handling, and stale load completion after refresh.

- [ ] **Step 3: Run WPF tests and confirm RED**

Run: `dotnet test tests/MpcplBuilder.Wpf.Tests --no-restore`

Expected: compilation fails because the real Explorer ViewModels do not exist.

- [ ] **Step 4: Implement `FolderNodeViewModel`**

Use a placeholder child only to display the expansion affordance, replace it on first expansion, and keep real children after collapse. Guard each load with a monotonically increasing version and a node-owned `CancellationTokenSource`. Expose `IconKey` as `GreenFolderIcon` or `YellowFolderIcon`; do not encode status only through color, and expose accessible status text.

- [ ] **Step 5: Implement initialization and refresh**

Represent This PC as a non-generatable root whose children are drive nodes. On refresh, capture selected full path and expanded full paths, reload roots, then reopen matching paths segment by segment. Compare paths with `StringComparer.OrdinalIgnoreCase`.

- [ ] **Step 6: Run focused and full suites**

Run:

```powershell
dotnet test tests/MpcplBuilder.Wpf.Tests --no-restore
dotnet test MpcplBuilder.Wpf.sln --no-restore
```

Expected: all ViewModel concurrency and restoration tests pass.

- [ ] **Step 7: Commit Explorer state management**

```powershell
git add src/MpcplBuilder.Wpf/ViewModels tests/MpcplBuilder.Wpf.Tests
git commit -m "feat: add lazy explorer view models"
```

### Task 5: Connect Explorer Generation and Overwrite Safety

**Files:**
- Modify: `src/MpcplBuilder.Wpf/ViewModels/ExplorerViewModel.cs`
- Modify: `tests/MpcplBuilder.Wpf.Tests/ExplorerViewModelTests.cs`

**Interfaces:**
- Consumes: `IGeneratePlaylist`, `IInspectPlaylistPresence`, and `IUserDialogService`.
- Produces: generation for the selected node with post-success direct-status refresh.

- [ ] **Step 1: Write failing generation tests**

Cover selected path and path mode, recursive generation delegation, already-green selection, output created after selection, overwrite decline, successful green transition, failure staying yellow, cancellation, and selection changing during generation.

```csharp
[Fact]
public async Task Generate_WhenSuccessful_RefreshesNodeToGreenWithoutReplacingSelection()
{
    var node = CreateNode(@"D:\Media", hasPlaylist: false);
    var generator = new FakeGeneratePlaylist { Response = Success(@"D:\Media\Playlist.mpcpl") };
    var presence = new FakeInspectPlaylistPresence(false, true);
    var vm = CreateExplorer(generator: generator, presence: presence, selected: node);

    await vm.GenerateCommand.ExecuteAsync(null);

    vm.SelectedFolder.Should().BeSameAs(node);
    node.HasPlaylist.Should().BeTrue();
    node.IsExpanded.Should().BeFalse();
}
```

- [ ] **Step 2: Run focused tests and confirm RED**

Run: `dotnet test tests/MpcplBuilder.Wpf.Tests --no-restore --filter FullyQualifiedName~ExplorerViewModelTests`

Expected: new generation tests fail.

- [ ] **Step 3: Implement generation orchestration**

Recheck direct playlist presence immediately before generation. Ask through `IUserDialogService` when it exists, pass the selected path mode through `GeneratePlaylistRequest`, and use the existing overwrite flag. After success call `IInspectPlaylistPresence` again and update only the original node if the operation version and selected full path still match.

- [ ] **Step 4: Run WPF and full suites**

Run:

```powershell
dotnet test tests/MpcplBuilder.Wpf.Tests --no-restore
dotnet test MpcplBuilder.Wpf.sln --no-restore
```

Expected: all generation and Default regression tests pass.

- [ ] **Step 5: Commit Explorer generation**

```powershell
git add src/MpcplBuilder.Wpf/ViewModels/ExplorerViewModel.cs tests/MpcplBuilder.Wpf.Tests
git commit -m "feat: generate playlists from explorer"
```

### Task 6: Implement the Explorer WPF View

**Files:**
- Create: `src/MpcplBuilder.Wpf/Views/ExplorerView.xaml`
- Create: `src/MpcplBuilder.Wpf/Views/ExplorerView.xaml.cs`
- Create: `src/MpcplBuilder.Wpf/Converters/FolderStatusToBrushConverter.cs`
- Modify: `src/MpcplBuilder.Wpf/Styles/Controls.xaml`
- Modify: `src/MpcplBuilder.Wpf/App.xaml`
- Modify: `src/MpcplBuilder.Wpf/MainWindow.xaml`
- Modify: `tests/MpcplBuilder.Wpf.Tests/MainWindowBindingTests.cs`

**Interfaces:**
- Consumes: Task 4/5 Explorer ViewModel properties and commands.
- Produces: accessible folder-only TreeView, toolbar, yellow/green icon treatment, and two-tab binding surface.

- [ ] **Step 1: Write failing XAML binding smoke tests**

On an STA thread, assert the shell tab order, initial selected index, bindings for roots/selection/path modes/commands/status, and automation names for TreeView, Generate, Refresh, and path controls. Capture `PresentationTraceSources.DataBindingSource` errors after dispatcher processing and assert the collection is empty.

- [ ] **Step 2: Run the binding test and confirm RED**

Run: `dotnet test tests/MpcplBuilder.Wpf.Tests --no-restore --filter FullyQualifiedName~MainWindowBindingTests`

Expected: failure because Explorer controls/templates are missing.

- [ ] **Step 3: Create the Explorer view**

Use the existing visual language. The toolbar presents the three radio buttons and Generate/Refresh commands. The hierarchical template binds `Children`, `IsExpanded`, and accessible playlist status. Use a folder-shaped geometry or compact text/icon resource with green/yellow brushes; include visible status text or a tooltip so color is not the only signal.

- [ ] **Step 4: Bind selection without code-behind filesystem logic**

Use a small selection behavior or event-to-command adapter whose only job is assigning the selected `FolderNodeViewModel` to `ExplorerViewModel.SelectedFolder`. Code-behind must not enumerate drives, inspect files, or invoke generation directly.

- [ ] **Step 5: Run WPF tests and rebuild**

Run:

```powershell
dotnet test tests/MpcplBuilder.Wpf.Tests --no-restore
dotnet build MpcplBuilder.Wpf.sln -t:Rebuild --no-restore
```

Expected: binding tests pass and build reports zero warnings and errors.

- [ ] **Step 6: Commit the Explorer interface**

```powershell
git add src/MpcplBuilder.Wpf tests/MpcplBuilder.Wpf.Tests
git commit -m "feat: add explorer tab interface"
```

### Task 7: Compose, Document, and Verify the Feature

**Files:**
- Modify: `src/MpcplBuilder.Wpf/App.xaml.cs`
- Modify: `README.md`
- Modify: relevant test fakes only if constructor wiring requires it

**Interfaces:**
- Consumes: all Explorer contracts, adapters, ViewModels, views, and existing playlist services.
- Produces: the complete running application and English developer/user documentation.

- [ ] **Step 1: Add a composition smoke test**

Construct the same object graph as `App.OnStartup` using fake UI services and assert the shell contains independent Explorer and Default ViewModels. This test prevents missing dependencies from being hidden solely in startup code.

- [ ] **Step 2: Wire production dependencies**

Construct one `PhysicalExplorerFileSystem`, the three Explorer use cases, `ExplorerViewModel`, and `DefaultViewModel`. Pass both to the shell. Initialize Explorer when its ViewModel is created or first activated without blocking window startup.

- [ ] **Step 3: Update the English README**

Document Explorer versus Default, direct green/yellow playlist status, This PC navigation, single active folder, Relative default, Refresh, overwrite behavior, and recursive generation. Keep the existing build/test/run commands accurate.

- [ ] **Step 4: Run final verification**

```powershell
dotnet restore MpcplBuilder.Wpf.sln
dotnet test MpcplBuilder.Wpf.sln -c Release --no-restore
dotnet build MpcplBuilder.Wpf.sln -c Release --no-restore
dotnet list src/MpcplBuilder.Domain reference
dotnet list src/MpcplBuilder.Application reference
dotnet list src/MpcplBuilder.Infrastructure reference
dotnet list src/MpcplBuilder.Wpf reference
```

Expected: all tests pass, Release build has zero warnings/errors, and project references still match the Global Constraints.

- [ ] **Step 5: Verify scope and working tree**

Run:

```powershell
rg -n "File\.Exists|Directory\.|DriveInfo|MessageBox" src/MpcplBuilder.Domain src/MpcplBuilder.Application
git diff --check
git status --short
```

Expected: no physical filesystem/UI calls in Domain or Application, no whitespace errors, and only explicitly preserved pre-existing unrelated changes remain outside this feature.

- [ ] **Step 6: Commit feature completion**

```powershell
git add README.md src tests
git commit -m "docs: complete explorer playlist workflow"
```
