# MpcSubtitle README Design

## Intent

Create an English, user-first README that helps a Windows user understand, install, and successfully run MpcSubtitle without reading the source code first.

## Goals

- Lead with a concise explanation of the product and an actual screenshot of the WPF application.
- Explain the packaged Windows installation path and the source-build path.
- Document first-run requirements: FFmpeg, internet access for the selected Whisper model, and the model cache location.
- Explain the GUI workflow, the single-file CLI workflow, supported model names, and common failure recovery.
- Keep every command, path, and product claim consistent with the current implementation.

## Non-goals

- Do not create an installer, publish a GitHub release, or change application behavior.
- Do not claim Linux or macOS GUI support.
- Do not document features that are not present in the current source.

## Constraints

- README copy is English.
- The screenshot must come from the actual application, not a mockup.
- Generated build outputs, model files, FFmpeg binaries, and the local release bundle remain untracked.
- Existing unrelated untracked files must not be modified or committed.

## Approaches

1. **User-first (selected):** screenshot and quick installation first, followed by usage, CLI, developer setup, and troubleshooting. This best serves users downloading the Windows package.
2. **Developer-first:** architecture and source build first. Rejected because it delays the path most users need.
3. **Compact:** one screenshot and minimal setup. Rejected because FFmpeg, model download, GUI, and CLI need enough detail to prevent predictable setup failures.

## Deliverables

- `mpc-subtitle/README.md`
- `mpc-subtitle/docs/images/mpc-subtitle-gui.png`
