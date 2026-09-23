# MPC Playlist Builder Clean Architecture and TDD Design

## Objective

Restructure MPC Playlist Builder into a Clean Architecture solution that separates business rules, application workflows, filesystem concerns, and WPF presentation. Preserve the existing single-window workflow and familiar visual layout while improving spacing, consistency, accessibility, state handling, cancellation, and testability.

The migration must preserve these user-visible capabilities:

- Select or type a root folder.
- Recursively detect supported video files.
- Match supported subtitle files to videos.
- Generate `Playlist.mpcpl` using relative, full, or Windows long paths.
- Detect an existing playlist and require overwrite confirmation.
- Show status, progress, output information, counts, and preview items.
- Clear the preview and cancel active work.

## Solution Structure

```text
MpcplBuilder.Wpf.sln
├── src
│   ├── MpcplBuilder.Domain
│   ├── MpcplBuilder.Application
│   ├── MpcplBuilder.Infrastructure
│   └── MpcplBuilder.Wpf
└── tests
    ├── MpcplBuilder.Domain.Tests
    ├── MpcplBuilder.Application.Tests
    ├── MpcplBuilder.Infrastructure.Tests
    └── MpcplBuilder.Wpf.Tests
```

All projects target .NET 8. The WPF project targets `net8.0-windows`; all non-UI projects target `net8.0`.

## Dependency Rules

- `MpcplBuilder.Domain` has no project references and no UI or filesystem dependencies.
- `MpcplBuilder.Application` references Domain and owns the ports used by its use cases.
- `MpcplBuilder.Infrastructure` references Application and Domain, and implements Application ports.
- `MpcplBuilder.Wpf` references Application and Infrastructure, composes dependencies, and contains all WPF-specific behavior.
- Domain and Application never reference WPF, Windows Forms, MessageBox, or physical filesystem APIs.
- Infrastructure never controls UI state or displays dialogs.

Runtime dependency flow:

```text
User action
  -> ViewModel command
  -> Application use case
  -> Application port implemented by Infrastructure
  -> Application result
  -> ViewModel state and View binding
```

## Domain Layer

Domain contains immutable models and deterministic playlist rules:

- `PlaylistEntry`: video path plus ordered subtitle paths.
- `PathMode`: `Relative`, `Full`, and `Long`.
- Supported video and subtitle extension policies.
- Subtitle matching policy, including exact basename and language suffix matching.
- Playlist path formatting rules that can be evaluated without reading or writing files.

Domain APIs accept data and return data. They do not enumerate directories, inspect metadata, write files, log, prompt users, or mutate UI state.

## Application Layer

Application coordinates the workflows and owns interfaces for external effects.

Primary use cases:

- `InspectFolder`: validate a selected folder, determine whether it contains a supported video, and return existing playlist metadata when present.
- `GeneratePlaylist`: enumerate media, match subtitles, order entries, serialize through a writer port, and return preview/count/output information.

Primary ports:

- `IMediaFileRepository`: enumerate supported video and subtitle candidates safely.
- `IPlaylistOutput`: inspect output metadata and write the playlist.

Use cases accept `CancellationToken`. They return explicit result records that distinguish success, invalid input, no supported videos, access failure, output failure, and cancellation. Expected operational outcomes are not represented by swallowed exceptions.

The Application layer owns no overwrite dialog. It reports that output exists, and the WPF layer decides whether to request confirmation before invoking generation.

## Infrastructure Layer

Infrastructure provides physical adapters:

- Safe recursive directory enumeration.
- File metadata lookup.
- MPCPL serialization with UTF-8 BOM and CRLF line endings.
- Atomic-enough output behavior for the current desktop scope: write the complete file through one owned stream and surface write failures; no database or recovery subsystem is introduced.

Unreadable child directories are skipped during recursive discovery so one inaccessible folder does not abort a scan. An invalid root folder or failure to create/write the requested output is returned as an error. Cancellation is checked during directory traversal, subtitle matching, serialization, and file writing.

Integration tests use unique temporary directories, real files, and deterministic fixtures. They clean up only directories created by the test.

## WPF Presentation Layer

The existing visual composition remains recognizable:

1. Root folder field and Browse button.
2. Path type radio buttons.
3. Output information.
4. Preview list and counts.
5. Generate, Clear, Cancel, progress, and status controls.

The XAML will be cleaned up with shared resources for spacing, typography, button styles, focus states, and status colors. Controls receive accessible labels, sensible tab order, and keyboard focus behavior. The redesign does not introduce a wizard, dashboard, navigation shell, or dark theme.

`MainWindowViewModel` uses `CommunityToolkit.Mvvm`:

- `ObservableObject` and generated observable properties.
- Async relay commands for browse-result processing and generation.
- Synchronous relay commands for clear and cancel where appropriate.
- Command state derived from root validity, video availability, active operation, output existence, and overwrite confirmation.

WPF-specific services isolate dialogs:

- `IFolderPicker` selects a folder.
- `IUserDialogService` requests overwrite confirmation and displays actionable errors.
- A composition root in `App.xaml.cs` constructs Infrastructure adapters, Application use cases, services, and the ViewModel.

Selecting a new folder cancels the previous inspection. Each inspection has an operation identity; only the latest operation may update ViewModel state. Generation disables incompatible commands and always restores state after success, cancellation, or failure.

## Error and State Handling

- Empty or missing roots produce an invalid-folder state and keep Generate disabled.
- A valid folder with no supported video produces a clear no-video state and keeps Generate disabled.
- An existing output enables Generate only after overwrite is confirmed.
- Access failures and output failures display a user-facing message and retain enough status for another attempt.
- Cancellation is shown as cancelled rather than error.
- Exceptions are caught at the Application/WPF boundary, mapped to defined outcomes, and never silently converted to `Ready`.
- ViewModel workflow code does not start unobserved tasks; async work is owned and observed by async commands.

## Testing Strategy

Tests use xUnit and FluentAssertions. Application and ViewModel tests use small handwritten fakes; no mocking framework is included in this migration.

### Domain Tests

- Supported extension matching is case-insensitive.
- Subtitle exact-basename and language-suffix matching.
- Rejection of unrelated similarly prefixed subtitles.
- Relative, full, long, and UNC path formatting.
- Stable ordering where ordering is part of the output contract.

### Application Tests

- Valid, invalid, empty, inaccessible, and cancelled folder inspection.
- Existing output metadata propagation.
- Generation orchestration and result mapping.
- Cancellation propagation.
- Output failure mapping.
- No duplicate scan for one folder selection.

### Infrastructure Tests

- Recursive discovery with mixed extensions.
- Subtitle discovery from real temporary files.
- MPCPL header, entry numbering, subtitle rows, BOM, and CRLF output.
- Skipping inaccessible children where the operating system permits a reliable fixture.
- Cancellation during traversal or writing.

### WPF Tests

- Generate command enablement after a successful inspection.
- Generate remains disabled for invalid roots, no videos, an active operation, or unconfirmed overwrite.
- Folder selection starts one inspection.
- A stale inspection cannot overwrite the latest selection state.
- Browse, overwrite, error, cancel, and clear interactions through UI service fakes.
- Property and command notifications needed by WPF bindings.

Every migrated behavior follows red-green-refactor. Characterization tests capture current behavior before a legacy implementation is moved. New behavior begins with a failing test. The full solution test suite and build must pass at each migration boundary.

## Migration Sequence

1. Move the existing projects under `src` and `tests` without changing behavior, and keep the solution buildable.
2. Replace the executable console-style test harness with xUnit test projects and characterization tests.
3. Introduce Domain models and policies, then migrate matching and path rules test-first.
4. Introduce Application ports, result models, and folder inspection/generation use cases test-first.
5. Introduce Infrastructure adapters and migrate recursive scanning and MPCPL writing behind the ports.
6. Add CommunityToolkit.Mvvm, UI service interfaces, and the composition root; migrate ViewModel behavior test-first.
7. Clean up XAML resources, spacing, focus/accessibility, and state presentation while preserving the current layout.
8. Delete the legacy static builder and obsolete command/test harness after equivalent tests pass.
9. Update README with the final project structure, build, test, and run commands.

## Out of Scope

- Changing playlist format or supported extensions unless required to preserve existing behavior.
- Adding media playback, drag-and-drop, settings persistence, localization, themes, telemetry, database storage, or online services.
- Replacing WPF with another UI framework.
- Introducing MediatR, a dependency injection container, or additional abstraction layers without an implementation-driven need.

## Completion Criteria

- The solution has the approved four production projects and four test projects.
- Project references obey the dependency rules.
- Existing user workflows and playlist output remain compatible.
- The original single-window layout remains recognizable and receives the agreed cleanup.
- Folder inspection is single-shot per selection, race-safe, cancellable, and updates command state correctly.
- All automated tests and the full solution build pass.
- README documents the architecture and developer commands.
