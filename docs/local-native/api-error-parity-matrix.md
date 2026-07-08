# Local Native Post-v1 #116 API/Error Parity Matrix

Date: 2026-07-08

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/116

## Decision

Decision:
`BOUNDED_NATIVE_UI_HEADLESS_ERROR_MATRIX_FIRST_NO_BROWSER_HTTP_COMPAT_LAYER`.

Meaning:

- #116 is a post-v1 hardening matrix, not a requirement to recreate the
  browser HTTP API inside the native app.
- Native parity means equivalent user-visible native UI behavior, native
  headless verifier behavior, or an explicit defer target with owner and
  evidence requirement.
- This slice does not modify `src/**`, does not start enhancement workers, does
  not delete cache/state assets, does not deploy, and does not touch H000033.
- Broad implementation remains split into the existing post-v1 issues #97-#118.

## Evidence Read

- `PROJECT.md`, `DESIGN.md`, `project.toml`, `START_HERE.md`
- `docs/operations-log.md`
- `local-native/README.md`
- `docs/local-native/native-intent-source.md`
- `docs/local-native/m19-verification.md`
- `docs/local-native/m20-verification.md`
- `tasks/local-native-m20/task.md`
- `tasks/local-native-m5/browser-regression-matrix.md`
- GitHub issue #116, PR #119, milestones #25/#26, and issues #97-#118
- Browser API routes under `src/app/api/**` read-only
- Native entrypoints under `local-native/PhotoViewer.Native/**` read-only

## Live Starting State

- `origin/main`: `6493f5bdd80fe8f213fa4201b603ee713c630311`
- PR #119: merged at `6493f5bdd80fe8f213fa4201b603ee713c630311`
- Milestone #25: closed
- Milestone #26: open, 22 open issues (#97-#118)
- SQLite job #233: `dispatched` for `Post-v1 #116`
- Agmsg trace `h25-post-v1-triage-20260708`: three `required_reply=none`
  pointers to `grok_consult`, `claude_ui`, and `cursor_impl`

## Status Legend

- `COVERED`: existing native UI/headless behavior is sufficient for this
  post-v1 matrix row.
- `PARTIAL`: native behavior exists, but the error equivalent or verifier is
  incomplete.
- `DEFER`: the behavior belongs to a separate post-v1 issue or future
  product/UI decision.
- `N/A_NATIVE`: browser HTTP semantics do not map directly to the native app;
  use native UI/headless evidence instead.

## Matrix

| Browser surface | Browser error / edge behavior | Native equivalent target | Current native evidence | #116 status | Next action |
| --- | --- | --- | --- | --- | --- |
| `POST /api/browse` | Folder picker helper failures return HTTP 500 with details; cancel returns empty path set. | Native `FolderBrowserDialog` plus folder-set textbox, `Add Folder`, recent set, and no-folder status. | `MainForm` has native browse/add-folder/recent-set paths; M11 folder-set smoke covers persisted multi-root behavior. | `PARTIAL` | No HTTP compatibility layer. If picker failure UX matters, add a native manual/UI check later. |
| `GET /api/scan` | Missing `dir` returns 400; root scan failures stream progress and final SSE error if all roots fail. | Native scan should refuse missing folders and keep UI usable. | Direct `dotnet run --no-build ... --headless-scan .\.cache\native-missing-folder-for-116` emitted `native-scan error=folder-not-found` and returned non-zero. UI scan shows `Folder not found: ...`. | `PARTIAL` | Add a future multi-root partial-failure headless check if needed. |
| `GET /api/search` | Invalid page/size are clamped; malformed `hiddenFolders` JSON is ignored. | Native search/filter UI should be deterministic and not crash on empty/no-result filters. | Indexed search, no-results UI, favorites/date/enhanced filter smokes exist. Missing folder direct headless search returns non-zero with `native-search error=folder-not-found`. | `COVERED` | No browser query-string clone needed. Search chips/tag UI remain #110. |
| `GET /api/folders` | Missing `dir` returns an empty folder list. | Native folder buckets should tolerate no folder set and expose show/hide controls after scan. | UI status covers `No folder set selected.`; M10/M11 cover folder bucket controls and multi-root buckets. | `COVERED` | Folder range selection remains #102. |
| `GET /api/tags` | Returns indexed tags; no explicit HTTP error surface. | Native tag/search action parity, if adopted. | Native search indexes filename/folder/path. Prompt/tag actions are not implemented. | `DEFER` | Keep tag actions/search chips in #109/#110. |
| `GET/PUT /api/favorites` | Missing/malformed favorites file normalizes to `{}`; levels are clamped. | Native should import favorites safely and clamp favorite levels. | `NativeStateBridge.LoadFavorites` catches malformed files and returns empty; native favorite levels clamp 0-5. | `COVERED` | Malformed-state user recovery copy belongs to #115. |
| `GET/PUT /api/settings` | Malformed settings fall back to defaults; PUT merges keybindings. | Native settings import should be read-only unless keybinding recorder is reopened. | M9 stores read-only settings/keybinding metadata; `browser_settings_found` is shown. | `PARTIAL` | Editable recorder remains #114; malformed recovery UI remains #115. |
| `GET /api/legacy-state` | Browser currently returns empty recent state. | Explicit browser state export import, no Chrome profile reads. | Native imports explicit `.cache/native/browser-localstorage-export.json`, records `pvu_*`, seen state, recent folder set. | `PARTIAL` | Complete `pvu_*` migration remains #117. |
| `GET /api/image` | Missing path 400; missing file 404; unsupported type 415; display/thumb cache failure falls back to source image; warm-only can return 204. | Native should avoid HTTP image serving and load directly from filesystem with clear preview/status failures. | Native scan supports the same extensions as browser (`png`, `jpg`, `jpeg`, `webp`, `avif`, `gif`). Direct preview uses native decode/ring buffer; cache compatibility measures missing/incompatible browser caches without deleting them. | `PARTIAL` | Add a native decode-failure/unsupported-decoder fixture only if it affects normal use; do not add HTTP image routes. |
| `POST /api/thumbs/warm` | Explicit paths require `dir`; invalid path fragments are ignored; warmup is bounded. | Native cache scheduler and preview warmup should stay bounded and passive. | Native cache scheduler prioritizes preview and neighbor decode; cache compatibility and performance smokes pass. | `DEFER` | Native thumbnail warmup UI/detail remains post-v1 optimization, not #116 closeout. |
| `POST /api/open` | Missing path 400; missing file 404; unsupported type 415; OS open failure 500. | Native open action should not crash the UI if ShellExecute fails. | Native selected-file/open-folder actions exist, but `OpenExternalPath` does not currently catch `Process.Start` errors. | `PARTIAL` | Small `local-native/**` fix candidate: catch external open failures and set status. Not implemented in this matrix slice. |
| `DELETE /api/delete` | Missing path 400; project path and non-indexed image 403; missing file 404; unsupported type 415; recycle failure 500. | Native delete should be selected-image only, Recycle Bin only, and never hard-delete on failure. | `DeleteSelectedImage` sends selected image to Recycle Bin and catches exceptions with `Recycle failed; file was not hard-deleted`. | `PARTIAL` | Confirmation/do-not-ask remains #106; bulk recycle remains #105; disposable destructive smoke required before implementation. |
| `GET /api/enhance/presets` | Returns browser enhancement presets. | Native explicit enhancement UI, if adopted later. | Native M19 only reads succeeded jobs for enhanced-only filtering. | `DEFER` | Keep enhancement queue/settings in #97. |
| `GET/POST /api/enhance/jobs` | Invalid JSON, missing/invalid fields, unknown adapter, missing backend, source not indexed, large-job guard, and queue conflicts return 400/404/409/503. | Native must not start enhancement workers automatically; explicit native queue work needs its own guarded milestone. | Native M19 reads `.cache/enhance/jobs.json` read-only and verifies enhancement state unchanged. | `DEFER` | #97 owns explicit-action-only native queue operations and error smokes. |
| `/api/enhance/jobs/:id` | Missing job 404; cancel can include interrupt warning; retry guards source missing/changed and status; output delete guards status. | Native queue job management, if adopted later. | Not implemented in native. Existing native read-only filter intentionally avoids job mutation. | `DEFER` | #97 owns cancel/retry/status; #98 owns output/toggle comparison. |
| `GET /api/enhance/output` | Missing jobId 400; missing output 404; output outside managed cache 403; missing file 404. | Native output discovery/open/delete/toggle, if adopted. | Native enhanced-only filter requires succeeded job with non-empty `outputPath`, but does not open/delete/toggle outputs. | `DEFER` | #97/#98. |
| `GET /api/enhance/isolation` | Reports passive enhancement metrics and queue running state. | Native passive browsing should not mutate enhancement state or start workers. | M7-M20 smokes repeatedly verify `enhancementStateUnchanged=true`; M19 reads jobs read-only. | `COVERED` | Keep this in regression smokes when enhancement UI work starts. |
| Native wrapper `scripts/start-local-native.ps1` | Not a browser API, but used as the documented verifier route. | Headless errors should propagate a non-zero process result through the wrapper. | Direct `dotnet run --no-build` returns non-zero for missing folder. The wrapper currently prints the native error but returns 0 because it does not exit with `$LASTEXITCODE`. | `PARTIAL` | Do not edit `scripts/**` in this issue without approval. Record as verifier-routing gap. |

## #116 Outcome

This issue should close as a matrix/triage slice when these are done:

1. The matrix is committed under `docs/local-native/**`.
2. The task pack is committed under `tasks/**`.
3. Verification proves no `src/**` diff.
4. GitHub issue #116 receives the matrix outcome.
5. SQLite job #233 is updated.
6. Agmsg pointers are sent with advice classification.
7. The next actual Codex thread is created or handed off if more native work
   remains.

## Follow-up Classification

Adopt now:

- `ADOPT`: Use this matrix as the post-v1 #116 result.
- `ADOPT`: Keep #116 scoped to native UI/headless error equivalents, not an
  HTTP compatibility server.

Defer:

- `DEFER`: Native enhancement queue errors to #97/#98.
- `DEFER`: Folder range selection to #102.
- `DEFER`: Bulk destructive/confirmation flows to #105/#106.
- `DEFER`: Malformed import recovery UI to #115.
- `DEFER`: Complete `pvu_*` persistence to #117.
- `DEFER`: UI screenshot/polish to #118.
- `DEFER`: `scripts/start-local-native.ps1` exit propagation until a scripts
  edit is explicitly approved or included in a verifier-focused issue.

Reject:

- `REJECT`: Adding a browser HTTP compatibility layer to satisfy native parity.
- `REJECT`: Treating browser E2E/API behavior as native acceptance by itself.

