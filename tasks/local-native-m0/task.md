# Local Native M0

## Goal

Create an isolated local-native PhotoViewer lane inside H000025 while keeping
the existing browser app and H000033 untouched.

## Exit Criteria

- A runnable Windows desktop prototype exists under `local-native/`.
- It scans a folder without starting Next.js, Chrome, or a local HTTP server.
- It displays large result sets through a virtual native list.
- It loads previews directly from local files.
- It reads existing `.cache/favorites.json` favorite levels.
- Current browser app files under `src/` remain untouched.
- Build status for the native prototype is known.

## Current M0 Slice

- Stack: .NET 8 Windows Forms.
- Entry command:

```powershell
dotnet run --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
```

- Parent-folder launcher:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1
```

- Headless verification route:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder "C:\path\to\images"
```

- Existing state bridge:
  - favorites: implemented read path.
  - albums/settings: counted or detected only.
  - browser `localStorage`: export/import planned, not direct profile access.

## Fast Track Plan

### M0 - Separate Local App Exists

Finish the runnable isolated prototype:

- Keep all native code under `local-native/`.
- Keep root-level integration to launchers/docs only.
- Build cleanly with `dotnet build`.
- Prove direct folder scan, virtual list, preview, and favorites read path.

### M1 - Native Store And Import

Move from JSON compatibility reads to a native local store:

- Add SQLite database under `.cache/native/`.
- Import favorites, albums, settings, and recent folders.
- Keep absolute path ids compatible with the browser app.
- Add a one-command import report.

### M2 - Viewer UI Parity

Recreate the working viewer surface without the browser runtime:

- Dense grid/list toggle.
- Favorite levels and filters.
- Folder/date sections.
- Modal-like preview navigation with previous/current/next preload.
- Open and delete through Windows APIs.

### M3 - Native Speed Pass

Use local-only acceleration after parity exists:

- Incremental scan with watcher updates.
- SQLite search and favorite indexes.
- Header-first dimensions.
- Bounded thumbnail/display cache only where measured useful.
- Keyboard navigation p95 checks.

## Routing Rule

Do not move this work to H000033 unless explicitly requested. H000033 can remain
as a separate experiment, but the active local-native lane for this repo lives
here.
