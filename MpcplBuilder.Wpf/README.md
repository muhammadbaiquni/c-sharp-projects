# MPC Playlist Builder

A WPF application that creates MPC-HC/MPC-BE playlists (`.mpcpl`) with automatic subtitle matching. The **Explorer** tab navigates folders from This PC; the **Default** tab keeps the browse-and-preview workflow. Each tab has its own selection and operation state.

## Features

### Explorer Tab

- Starts at **This PC** and shows drives and folders only. Expand a folder to load its immediate children; expanding does not recursively scan its subtree.
- Select one active folder at a time. Both green and yellow folders can be selected. A **green** folder icon means `Playlist.mpcpl` exists directly in that folder; **yellow** means it does not. A playlist in a descendant does not change its parent's icon.
- Uses **Relative Path** by default. **Full Path** and **Long Path** are also available.
- **Generate** recursively includes supported videos beneath the selected folder and writes `Playlist.mpcpl` in that folder. The icon updates when generation succeeds.
- **Refresh** reloads drives and folder status, retaining the expanded folders and selection when those paths still exist.
- If the playlist already exists, **Generate** asks for overwrite confirmation. Choosing **No** leaves the file unchanged.

### Default Tab

Default is initially active and keeps the browse, inspection, preview, and generation workflow described below.

#### 1. Relative Paths (Default, Portable)

- Stores paths relative to the selected root folder.
- Keeps the playlist working when the entire folder is moved to another location or drive.
- Example: `Subfolder\Movie.mkv` instead of `D:\Videos\Subfolder\Movie.mkv`.
- Recommended for maximum portability.

#### 2. Full Paths (Absolute)

- Stores full absolute paths.
- Keeps media paths valid when only the `.mpcpl` file is moved.
- The playlist will stop working if the video folder itself is moved.
- Example: `D:\Videos\Subfolder\Movie.mkv`.

#### 3. Long Paths (`\\?\`)

- Stores paths using the Windows long-path prefix (`\\?\`).
- Supports paths longer than 260 characters.
- Example: `\\?\D:\Videos\Very\Long\Path\Subfolder\Movie.mkv`.
- Available on Windows only.

#### 4. Existing Playlist Detection

- Automatically detects an existing `Playlist.mpcpl` file in the selected folder.
- Displays its last-write date and file size.
- Requests confirmation before overwriting it.

#### 5. Automatic Subtitle Matching

Subtitles are matched automatically when their filenames correspond to the video filename:

- `Movie.mkv` → `Movie.srt`
- `Movie.mkv` → `Movie.en.srt`
- `Movie.mkv` → `Movie.id.ass`

## Supported Video Formats

`.mp4`, `.mkv`, `.avi`, `.mov`, `.wmv`, `.m4v`, `.webm`, `.mpg`, `.mpeg`, `.ts`, `.m2ts`, `.flv`

## Supported Subtitle Formats

`.srt`, `.ass`, `.ssa`, `.vtt`, `.sub`, `.idx`, `.txt`

## Usage

### Explorer

1. Open **Explorer**, expand **This PC**, then expand drives and folders to find the folder you want. Select that folder; only one folder is active.
2. Choose **Relative Path** (the default), **Full Path**, or **Long Path**.
3. Click **Generate**. The app scans the selected folder recursively and saves `Playlist.mpcpl` there. If the file already exists, confirm or cancel the overwrite prompt.
4. Click **Refresh** to reload drives, folders, and direct playlist status. Existing paths retain their expanded and selected state.

### Default

1. Click **Browse...** and select the root folder that contains your videos.
   - The application scans the folder recursively.
   - If a playlist already exists, the application displays its file information and asks whether overwriting should be enabled.
2. Select a path type:
   - **Relative Path** (default): portable and recommended when the folder may be moved.
   - **Full Path**: uses absolute paths when the video folder will remain in place.
   - **Long Path**: supports files with paths longer than 260 characters.
3. Click **Generate** to create the playlist.
   - If a playlist already exists, a confirmation dialog appears before it is overwritten.
   - Select **Yes** to overwrite it or **No** to cancel.
4. Click **Clear** to clear the preview. This does not delete the generated playlist.
5. The application saves `Playlist.mpcpl` in the selected root folder.

## Default Tab Buttons

| Button | Function |
| --- | --- |
| **Browse...** | Selects the root folder, scans for supported videos, and detects an existing playlist. |
| **Generate** | Creates the `.mpcpl` playlist and displays a preview. |
| **Clear** | Clears the preview without deleting the playlist file. |
| **Cancel** | Cancels the active scan or playlist generation operation. |

## Explorer Tab Buttons

| Button | Function |
| --- | --- |
| **Generate** | Creates `Playlist.mpcpl` for the selected folder and its descendant videos. |
| **Refresh** | Reloads drives, folders, and direct playlist status. |

## Default Tab Status Indicators

### Output Information

- `(will be saved as "Playlist.mpcpl" in: ...)` — no playlist currently exists in the selected folder.
- `⚠️ Playlist already exists! (created: ..., size: ...)` — an existing playlist was detected.
- `✅ Playlist created successfully! (...)` — the playlist was created or updated successfully.

### Status Bar

- `Ready` — the application is ready.
- `Scanning...` — the folder is being scanned.
- `Done` — playlist generation completed.
- `Preview cleared` — the preview was cleared.
- `Warning: Existing playlist detected` — an existing playlist was found.
- `Cancelled` — the operation was cancelled.

## Technical Details

### Solution Structure

The solution follows Clean Architecture so business rules can be tested independently of WPF and the physical filesystem:

```text
src/
├── MpcplBuilder.Domain/          Playlist models and deterministic media/path policies
├── MpcplBuilder.Application/     Folder inspection and playlist generation use cases
├── MpcplBuilder.Infrastructure/  Filesystem traversal and MPCPL output adapters
└── MpcplBuilder.Wpf/             WPF composition, ViewModel, dialogs, and views
tests/
├── MpcplBuilder.Domain.Tests/
├── MpcplBuilder.Application.Tests/
├── MpcplBuilder.Infrastructure.Tests/
└── MpcplBuilder.Wpf.Tests/
```

The Domain project has no project dependencies. Application depends only on Domain. Infrastructure implements Application ports and uses Domain policies. WPF composes the application and infrastructure at startup.

The test suite follows a test-first workflow and covers media policies, path formatting, application use cases, real filesystem integration, MPCPL byte compatibility, ViewModel command state, cancellation, stale operation protection, WPF bindings, and startup composition.

### Path Mode Comparison

| Mode | Portability | Path Length | Example |
| --- | --- | --- | --- |
| **Relative** | High | Normal | `Videos\Movie.mkv` |
| **Full Path** | Low | Normal | `D:\Media\Videos\Movie.mkv` |
| **Long Path** | Low | More than 260 characters | `\\?\D:\Very\Long\...\Movie.mkv` |

### Path Conversion Examples

```text
# Relative path
Root:   C:\Videos
File:   C:\Videos\Movies\Action\Movie.mkv
Output: Movies\Action\Movie.mkv

# Full path
Output: C:\Videos\Movies\Action\Movie.mkv

# Long path
Output: \\?\C:\Videos\Movies\Action\Movie.mkv
```

### Playlist Overwrite Protection

```text
1. Select a folder.
   ↓
2. Check whether Playlist.mpcpl exists.
   ↓
3. If it exists, display its file information and request overwrite confirmation.
   ↓
4. Generate the playlist when overwrite is allowed; otherwise, cancel the operation.
```

## Requirements

- Windows
- .NET 8 Desktop Runtime
- MPC-HC or MPC-BE for playlist playback

## Build and Run

```powershell
dotnet restore MpcplBuilder.Wpf.sln
dotnet build MpcplBuilder.Wpf.sln --no-restore
dotnet test MpcplBuilder.Wpf.sln --no-build
dotnet run --project src/MpcplBuilder.Wpf/MpcplBuilder.Wpf.csproj
```
