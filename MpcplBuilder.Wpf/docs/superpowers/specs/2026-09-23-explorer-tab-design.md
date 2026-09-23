# Explorer Tab Design

## Objective

Add an Explorer tab to MPC Playlist Builder while preserving the existing workflow in a separate Default tab. Explorer lets the user navigate folders from This PC, see whether each visible folder directly contains `Playlist.mpcpl`, select one active folder, and generate a playlist for that folder using the same recursive behavior as the Default workflow.

The header tab order is `Explorer | Default`. Default remains the initially selected tab so the startup experience does not change for existing users.

## User Experience

### Default Tab

The Default tab contains the current interface and retains its existing behavior. Its root folder, path mode, preview, operation state, and commands are independent from Explorer state.

### Explorer Tab

The Explorer toolbar contains:

- Relative Path, Full Path, and Long Path choices.
- Relative Path selected by default.
- A Generate button.
- A Refresh button.

The main area contains a folder-only TreeView rooted at This PC. All available drives appear below This PC. A drive or folder loads its immediate child folders only when expanded so opening the tab does not recursively enumerate the machine.

Only one drive or folder can be active at a time. The standard TreeView selection indicates the active folder. Generate is enabled when an available drive or folder is selected and no conflicting operation is running.

Each folder or drive uses a Windows-style folder icon whose color communicates direct playlist status:

- Green: `Playlist.mpcpl` exists directly in that folder.
- Yellow: `Playlist.mpcpl` does not exist directly in that folder.

The color is informational and never prevents selection. A playlist in a descendant folder does not make an ancestor green.

## Generation Behavior

Generate treats the active folder as the playlist root. It recursively discovers supported videos below that folder, matches subtitles using the existing policy, applies the selected path mode, and writes `Playlist.mpcpl` directly in the active folder through the existing `IGeneratePlaylist` workflow.

The application checks the destination immediately before writing. If a playlist exists, the same overwrite confirmation used by Default is required. Declining leaves the existing file unchanged.

After a successful generation:

- The active node remains selected.
- Expanded nodes and the current TreeView position remain intact.
- The active node refreshes immediately and becomes green.
- Status and error presentation follow the existing application conventions.

## Architecture

`MainWindowViewModel` becomes a lightweight shell that exposes two independent tab ViewModels:

- `ExplorerViewModel` owns Explorer navigation, selection, path mode, refresh, and generation state.
- `DefaultViewModel` owns the existing single-folder workflow currently implemented by `MainWindowViewModel`.

The WPF layer contains `ExplorerView`, `DefaultView`, and `FolderNodeViewModel`. `FolderNodeViewModel` represents a drive or folder, its direct playlist status, expansion state, availability, child-loading state, and child nodes. It requests children lazily and does not access the filesystem directly.

Application owns the Explorer contracts and use cases needed to:

- list available drives;
- list immediate child folders;
- determine whether a folder directly contains `Playlist.mpcpl`;
- refresh a node's direct playlist status.

Infrastructure implements these ports with physical drive and directory APIs. It returns only folders and keeps filesystem exceptions outside WPF. Existing Domain playlist policies and `IGeneratePlaylist` remain the single source of generation behavior.

The dependency direction remains unchanged:

```text
WPF -> Application <- Infrastructure
          |
          v
        Domain
```

No Windows Shell dependency or new dependency injection container is introduced.

## State and Concurrency

Drive discovery, child loading, refresh, and generation accept `CancellationToken`.

- Expanding a node that is already loading does not start a duplicate request.
- Collapsing a node does not discard its loaded children.
- A stale load or refresh result cannot replace newer node state.
- Changing the active folder cannot allow a previous operation to publish state for the new selection.
- Generate remains busy until its operation has completed or acknowledged cancellation.

Refresh reloads the drive list, visible expanded branches, and direct playlist status while preserving the active selection and expanded paths when those paths still exist. A removed drive or missing folder is marked unavailable or removed during refresh without crashing the entire tree.

The Explorer tab and Default tab keep independent cancellation sources and observable state. Work in one tab does not replace the other tab's selection, preview, or status.

## Error Handling

- An inaccessible child folder does not stop siblings from loading.
- Selecting or expanding an inaccessible folder produces an actionable access status without collapsing the entire tree.
- A removed drive or deleted folder is handled as unavailable and can be reconciled through Refresh.
- Generation errors use the existing dialog service and leave the node's playlist status unchanged.
- Cancellation is presented as cancellation, not failure.
- A node becomes green only after the output write completes successfully or a refresh confirms the file exists.

## Testing Strategy

All new behavior is developed test-first with xUnit, FluentAssertions, and handwritten fakes.

### Application Tests

- Lists available drives.
- Lists immediate child folders only.
- Excludes files from Explorer results.
- Detects `Playlist.mpcpl` directly in a folder.
- Distinguishes inaccessible, missing, and cancelled operations.
- Refreshes playlist status after generation.

### Infrastructure Tests

- Enumerates real temporary child folders without returning files.
- Detects direct playlist presence without treating descendant playlists as direct matches.
- Handles inaccessible children while surfacing a root access failure.
- Honors cancellation during enumeration.
- Maps available drives through an injectable boundary so tests remain deterministic.

### WPF Tests

- Default is the initially selected tab and the visual order is Explorer then Default.
- Explorer path mode defaults to Relative.
- Drive and folder child nodes load lazily and only once per active load.
- Folder icons map direct playlist state to green or yellow.
- Only one folder is active.
- Generate command state follows selection, availability, overwrite approval, and busy state.
- Generation sends the active folder and selected path mode to `IGeneratePlaylist`.
- Successful generation changes the active node to green without losing selection or expanded nodes.
- Refresh preserves valid selection and expansion state.
- Cancellation and stale results do not publish obsolete state.
- Binding smoke tests cover both tabs.
- Existing Default tests remain green as regression coverage.

## Implementation Sequence

1. Add Application Explorer models, ports, and use cases with tests.
2. Add physical drive and folder adapters with integration tests.
3. Extract the current interface and ViewModel into the Default tab without changing behavior.
4. Add the shell tab structure with Default initially active.
5. Add folder node and Explorer ViewModels test-first, including lazy loading and concurrency rules.
6. Add the Explorer view, toolbar, TreeView templates, and accessible bindings.
7. Connect generation, overwrite confirmation, refresh, and post-generation status updates.
8. Run binding, regression, full Release, and dependency-direction verification.
9. Update the English README with the new Explorer workflow.

## Out of Scope

- Displaying files in the TreeView.
- Selecting or generating for multiple folders at once.
- File operations such as copy, move, rename, or delete.
- Windows Shell integration, context menus, drag and drop, or filesystem watching.
- Persisting the selected tab, expanded folders, or last active folder across application restarts.
- Changing supported media formats, subtitle matching, or MPCPL serialization.

## Completion Criteria

- The header displays `Explorer | Default`, with Default initially active.
- Explorer starts at This PC and lists available drives and folder-only descendants lazily.
- Every visible folder communicates direct `Playlist.mpcpl` presence with the agreed green or yellow folder icon.
- Any available folder can be selected, including one that already has a playlist.
- Generate processes only the active folder as root while recursively including its descendants.
- Relative Path is the Explorer default, with Full and Long choices available.
- Overwrite confirmation and error behavior match Default.
- Successful generation updates the selected node to green without resetting the tree.
- Default behavior remains unchanged.
- All automated tests and the Release build pass without warnings.
