# WPF persistent prompt metadata index closeout

Date: 2026-07-19
Tracking: GitHub issue #321 / SQLite improvement item #43
Final revision: the commit containing this recap

## Outcome

WPF now opens the complete lightweight catalog first and continues Prompt /
dimension indexing in the background, while retaining the last complete result
in a safe WPF-owned persistent index. Reopening the same 100,000-image folder
set no longer rereads all source metadata.

The Browser remains authoritative for shared Favorite, Seen, Recent, thumbnail
cache, and Enhancement state. The new index owns none of those stores and does
not modify source images.

## Durable index contract

- One folder-set-specific `.pvmi` file under the WPF state owner's
  `metadata-index-v1` directory; tests can override only the directory through
  `PHOTOVIEWER_WPF_METADATA_INDEX_DIRECTORY`.
- Normalized/sorted absolute folder-set identity is SHA-256 keyed.
- Entries contain absolute source path, length, last-write/creation UTC identity,
  dimensions, and searchable Prompt text.
- Header contains magic, schema version, bounded entry/payload lengths, and a
  SHA-256 payload checksum. Strict UTF-8 and bounded path/prompt sizes fail safe.
- Truncation, malformed lengths, checksum mismatch, or invalid data becomes an
  Invalid load and falls back to source reads without escaping into the UI.
- Future schema is protected both on load and again while holding the writer
  lock immediately before commit.
- Writes use a folder-set writer lock, same-directory unique temp file,
  flush-to-disk, and atomic replacement. Stale temp cleanup is scoped to the
  same target and lock.
- Only a complete current-generation snapshot can replace the durable file.
  Decode failure, catalog mutation, cancellation, supersession, and close keep
  the previous complete bytes.
- Available-root deletions prune stale entries. A temporarily unavailable
  selected root retains its previous derived entries for reconnect.
- Exact restart all-hits preserve index bytes and mtime and skip the 100k
  snapshot-rebuild allocation path.

## Viewer progress

The Viewer exposes monotonic Prompt metadata progress after the catalog is
already usable. The status is visible in the Folder sidebar and in a bounded
footer surface when space/sidebar state would otherwise hide it. Final status
distinguishes ready/reused, refreshed, incomplete, future-version protected,
catalog superseded, and save failure. Only one polite live region is published.

## Performance evidence

Same TEMP-only 100,000-image / 100-folder shape:

| Metric | Fresh cold baseline | Cold after | Restart warm |
| --- | ---: | ---: | ---: |
| Catalog ready | 3,721 ms | 3,809 ms | 3,396 ms |
| Background metadata | 27,176 ms | 26,659 ms | 213 ms |
| Full load | 31,269 ms | 30,850 ms | 3,928 ms |
| Metadata index read/write | n/a | 0 / commit | 65 / 0 ms |
| Index hits/misses | 0 / 100,000 | 0 / 100,000 | 100,000 / 0 |

Warm metadata is 99.2% shorter and warm full load is 87.4% shorter than the
fresh cold baseline. Catalog-ready time did not regress. Both phases retained:

- exact source/catalog/filtered count 100,000; silent truncation 0
- Grid/List realization 15/9 and far-tail index 99,999
- zoom-anchor drift 0px
- overall dispatcher max gap 424ms and external WM_NULL max streak 387ms,
  below the 750ms gate
- Enhancement reads/candidates 0/0
- source/state isolation and successful TEMP cleanup

Bounded details are in `baseline.md` and `after.md`.

## Verification evidence

- Release build: 0 warnings / 0 errors.
- `verify-wpf-metadata-index.ps1`: cold, separate-window warm, one-file partial
  stale, checksum corruption/rebuild, future schema, commit-time future guard,
  checksum-valid malformed length, decode-failure preservation, stale deletion
  prune, background cancellation, source/store/environment isolation, residue
  cleanup, and separate-process cold→warm restart: PASS.
- `verify-wpf-catalog-stress.ps1 -Count 100000 -FolderCount 100`: cold→separate
  `MainWindow` warm, full catalog/virtualization/tail/zoom/dispatcher/WM_NULL:
  PASS.
- `verify-wpf-product.ps1 -IncludeReloadSoak`: 50/50 checks PASS, including the
  20,000-image cold→warm aggregate stress and 24-cycle same-process reload soak.
- Cross-runtime shared Favorite/Seen: 20 iterations, 40/40 disjoint paths,
  valid JSON, no lock/temp residue, no real port/user cache: PASS.
- Cross-runtime Recent: 20 iterations, three owner sets, unknown-field/latest
  owner policy preserved, no lock/temp residue: PASS.
- Browser project verifier: 55 test files passed / 2 skipped; 475 tests passed /
  2 skipped; ESLint, TypeScript, and production Next build PASS.
- 1280x820 and 1024x700 Viewer/Folders visual evidence shows the final metadata
  status without crop/overflow.
- `git diff --check`: PASS.

## Source Delete safety addendum

The user restated that Browser and WPF source-image Delete must move files to
the Windows Recycle Bin. Live code already met that contract:

- Browser production backend uses Microsoft.VisualBasic `DeleteFile` with
  `SendToRecycleBin`; failure has no hard-delete fallback.
- WPF production backend uses `RecycleOption.SendToRecycleBin`; single/bulk
  failure retains the source.
- Browser focused Delete tests (5 files / 113 tests) and WPF correctness/bulk/
  race fake-backend gates pass.
- `verify-ui-regression-guard.ps1` now statically rejects replacement with
  `DeletePermanently`, `File.Delete`, `fs.rm`, or `fs.unlink` in either source
  Delete workflow.

Explicit deletion of managed AI-generated output and cleanup of derived
thumbnail/temp/lock files remain separate ownership domains and never serve as
a fallback for deleting the source image.

## Preserved boundaries

- Browser port 3000 was not started, stopped, or probed.
- User Favorite/Seen/Recent/state/cache and source images were not deleted or
  reset.
- Browser UI/runtime, WinForms local-native, deployment, and passive
  Enhancement ownership were not changed.
- GitHub Actions and external consultations were not used as gates.
