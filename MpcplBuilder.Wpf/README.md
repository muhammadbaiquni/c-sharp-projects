# MPC Playlist Builder

WPF application to create MPC-HC playlists (.mpcpl) from a video folder recursively, with automatic subtitle matching support.

## Fitur

### 1. Relative Path (Default - Portable)
- The playlist uses paths relative to the selected root folder.
- Advantage: The playlist remains functional when the folder is moved to another location or drive.
- Example: `Subfolder\Movie.mkv` instead of `D:\Videos\Subfolder\Movie.mkv`.
- Recommendation: Use this mode for maximum portability.

### 2. Full Path (Absolute)
- The playlist uses full absolute paths.
- Advantage: Paths remain valid even if the .mpcpl file is moved.
- Disadvantage: Not portable — paths will break if the video folder is moved.
- Example: `D:\Videos\Subfolder\Movie.mkv`.

### 3. **Long Path (\\?\)**
- Playlist menggunakan format Windows Long Path dengan prefix `\\?\`
- **Keuntungan**: Mendukung path yang sangat panjang (>260 karakter)
- **Contoh**: `\\?\D:\Videos\Very\Long\Path\Subfolder\Movie.mkv`
- **Catatan**: Hanya untuk Windows

### 4. Detect Existing Playlist
- Automatically detects if a `Playlist.mpcpl` file already exists in the folder.
- Displays information: creation/last-write date and file size.
- Asks for confirmation before overwriting an existing playlist to avoid accidental duplicates.

## Format Video yang Didukung
`.mp4`, `.mkv`, `.avi`, `.mov`, `.wmv`, `.m4v`, `.webm`, `.mpg`, `.mpeg`, `.ts`, `.m2ts`, `.flv`

## Format Subtitle yang Didukung
`.srt`, `.ass`, `.ssa`, `.vtt`, `.sub`, `.idx`, `.txt`

## How to use

1. Select the root folder: Click the "Browse..." button and pick the folder that contains your videos.
   - If a playlist already exists in the selected folder, a warning with file info will be shown.
2. Choose Path Type:
   - Relative Path (default): Portable, recommended if you plan to move the folder.
   - Full Path: Use absolute paths if you will not move the folder.
   - Long Path: Use when some files have very long paths.
3. Generate: Click the "Generate" button to build the playlist.
   - If a playlist already exists, a confirmation dialog appears.
   - Choose "Yes" to overwrite, or "No" to cancel.
4. Clear: Click the "Clear" button to clear the preview (this does not delete the playlist file).
5. Output: The `Playlist.mpcpl` file will be created in the selected root folder.

## Tombol & Fungsi

| Tombol | Fungsi |
|--------|--------|
| **Browse...** | Select the root folder to scan for videos. Detects existing playlist files. |
| **Generate** | Create the .mpcpl playlist and show a preview (asks for overwrite confirmation if needed). |
| **Clear** | Clear the preview without deleting any playlist files. |

## Indikator Status

### Output Info
- `(will be saved as "Playlist.mpcpl" in: ...)` - New folder, no playlist exists yet
- `⚠️ Playlist already exists! (created: ..., size: ...)` - Warning: playlist already exists
- `✅ Playlist created successfully! (...)` - Success: playlist created/updated

### Status Bar
- `Ready` - Ready to use
- `Scanning...` - Scanning the folder
- `Done` - Operation completed
- `Preview cleared` - The preview has been cleared
- `Warning: Existing playlist detected` - An existing playlist was detected
- `Cancelled` - User cancelled the overwrite

## Matching Subtitle

Subtitles are automatically matched when the subtitle filename corresponds to the video:
- `Movie.mkv` -> `Movie.srt`
- `Movie.mkv` -> `Movie.en.srt`
- `Movie.mkv` -> `Movie.id.ass`

## Technical Details

### Path Mode Comparison

| Mode | Portabilitas | Path Length | Contoh |
|------|--------------|-------------|--------|
| **Relative** | ? Tinggi | Normal | `Videos\Movie.mkv` |
| **Full Path** | ? Rendah | Normal | `D:\Media\Videos\Movie.mkv` |
| **Long Path** | ? Rendah | >260 chars | `\\?\D:\Very\Long\...\Movie.mkv` |

### Path Conversion Logic

```csharp
// Relative Path
Root: C:\Videos
File: C:\Videos\Movies\Action\Movie.mkv
Output: Movies\Action\Movie.mkv  ? Portable!

// Full Path
Output: C:\Videos\Movies\Action\Movie.mkv  ? Not portable

// Long Path
Output: \\?\C:\Videos\Movies\Action\Movie.mkv  ? Supports >260 chars
```

### Playlist Overwrite Protection

```
1. User clicks Browse ? Check if Playlist.mpcpl exists
   ?
2. If exists ? Show warning with file info (date + size)
   ?
3. User clicks Generate ? Show confirmation dialog
   ?
4. User selects Yes ? Overwrite file
   User selects No ? Cancel operation
```

## Requirements
- Windows (net8.0-windows)
- .NET 8.0 Runtime
- MPC-HC or MPC-BE to play the playlist

## Build
```bash
dotnet build
dotnet run
