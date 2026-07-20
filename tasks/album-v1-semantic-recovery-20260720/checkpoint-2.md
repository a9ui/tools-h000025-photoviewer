# Checkpoint 2 — Album v1 product surface and Browser/WPF parity

## Provenance and boundary

- Implementation base: local `main` / PR #322 tree-identical baseline
  `8914935f3118f5634d22570be7d207fbc7735340`.
- Prior shared-store checkpoint:
  `65ef662401fa611c872b89bc51a2d0626d33eda8`.
- This checkpoint is committed only to local `main`. PR #322 remains
  **MERGE FROZEN**: no push, merge, or close was performed.
- Claude branch `claude/photoviewer-local-ui-f62c30` remains read-only evidence;
  its untracked `local-native/` was not changed or removed. No Claude commit was
  cherry-picked and rejected viewer commit `b584744` was not adopted.

## Browser implementation

- Added an independent `AlbumContext`; the existing `ImageContext`, Search
  results, Delete, Favorite, Filmstrip, zoom anchor, and Enhancement ownership
  remain in place.
- Added lazy library and picker surfaces with create, rename, delete, pin, cover,
  recent, one/bulk add, remove, and create-then-add operation calls.
- Added derived current/outside/missing availability and guarded Album source
  sessions. No unrestricted raw-path image URL is exposed.
- Gallery, Right Preview, tabs, Modal and Filmstrip consume the active Album
  source order without overwriting Search result arrays.
- Membership removal and source Recycle are separate actions. Membership cleanup
  runs only after a PhotoViewer-owned source Recycle succeeds; external absence
  remains a tombstone.
- Added collision-aware `addToAlbum` shortcut migration. B is preferred; an
  existing Filmstrip B binding causes a free fallback such as G.

## WPF implementation

- Enabled the header Album button and added a basic native library for create,
  rename, delete, pin, cover, add selection, remove, open, and catalog return.
- Active Album filtering follows durable member order. Modal navigation uses the
  filtered Album source rather than the old catalog/search order.
- Current members are navigable. Outside and missing members are counted and
  explicitly unavailable; WPF v1 does not silently open an arbitrary absolute
  path outside the current catalog.
- Source Recycle success triggers shared membership cleanup. Cleanup failure is
  surfaced while preserving a recoverable tombstone.
- Added the same collision-aware `addToAlbum` key action and migration semantics.
- WinForms was not changed.

## Storage, failure, and scale

- Browser and WPF accept the common shared formats PNG/JPEG/WebP/AVIF/GIF.
- Existing target-scoped create-new lock, latest-on-disk mutation, expected
  revision, flushed same-directory temp, atomic publish, malformed/future refusal,
  and unknown-field preservation remain the sole write contract.
- A barrier test overlaps 16 Browser and 16 WPF creates on the same document:
  final revision 32, 32 Albums, lost update 0, lock/temp residue 0.
- Album storage test creates 100,000 members and verifies read, atomic update,
  member identity, and unknown-field preservation.

## Final local gates

| Gate | Result |
| --- | --- |
| Browser full unit | 68 files passed, 3 skipped; 614 tests passed, 3 skipped |
| Browser typecheck / ESLint / production build | all green; Next.js 16.2.10 lists all Album routes |
| Browser Album runtime | isolated Chromium, port 3131, 1/1 passed; source remove remained separate from Recycle |
| WPF Release + Album focused | 0 warnings / 0 errors; 5 files, 24 tests; UI/source/shortcut/concurrency all true |
| WPF aggregate + reload | 56/56 checks green in 311,665 ms; reload 24/24, stale completion 0, CTS 73/73 |
| WPF exact scale | 100,000/100,000 catalog and filtered; silent truncate 0; Grid/List realized 15/9; tail 99,999; warm hits 100,000/misses 0; anchor drift 0 |
| Source/passive boundaries | 100k cleanup succeeded; Enhancement reads/candidates 0; no user state/cache deletion |

## Deliberate non-goals and remaining boundaries

- Explicit moved-member relink remains a future operation; v1 never guesses a
  move by filename.
- WPF outside member viewing requires adding its folder to the active catalog;
  the explicit unavailable contract is intentional.
- Image context menu and Ctrl+C bitmap/file copy are separate pending product
  lanes and were not changed by Album v1.
- GitHub #322 promotion is not authorized. Because local main is ahead of its
  head, #322 must not be described as mergeable until its head is updated to the
  selected descendant and the required tree/provenance proofs are all rerun.
