# Checkpoint 3 — independent-review hardening (working state)

## Snapshot and safety boundary

- Work is integrated directly in the existing local `main` worktree. The
  pre-hardening HEAD is `f64d98d81e016e43177b1e39738f759742d1215b`; the final
  checkpoint SHA is **PENDING FINAL GATE** because this delta is not committed.
- PR #322 remains **MERGE FROZEN**. Its recorded head is `8914935`; this lane
  does not push, merge, close, or otherwise update the PR.
- Claude evidence remains protected at
  `claude/photoviewer-local-ui-f62c30` / `c63df41220087cb323faf81f0c18bf187a4b68b3`.
  Its untracked `local-native/` tree was read only and was not changed, moved,
  cleaned, or deleted.
- No new branch/worktree, cherry-pick, deployment, WinForms change, user
  cache/state deletion, Agmsg, or external consultation was used.
- Tracked `next-env.d.ts` is an unrelated generated worktree change. It is not
  part of this checkpoint and must not be staged, reset, or described as an
  Album/UI implementation change.

## Implemented in the current worktree

### Album and shared-state hardening

- Browser and WPF accept only the unambiguous legacy empty Album document and
  migrate it to v1; non-empty or partially versioned legacy data remains a
  protected refusal.
- Same-document and same-Album interleavings mutate the latest on-disk state
  under the shared create-new lock. Browser and WPF Album stale-lock recovery check a
  structured owner PID and fails closed while that owner is still live.
- PhotoViewer Recycle success remains independent from Album membership cleanup.
  Cleanup is a commutative latest-state operation, exposes a pending status on
  failure, preserves the missing member, and can be retried without reversing
  the successful source Recycle.
- Collision-aware `addToAlbum` migration now also reserves valid chords stored
  under unknown future key-binding actions before selecting a fallback.
- Browser settings use the shared project-root resolver. Favorite, Seen,
  Recent, Search History, Album, thumbnail-border settings, and Enhancement
  job/output meaning remain shared according to their established owners;
  WPF continues to read or delegate Enhancement operations rather than owning
  the worker/store.
- Browser `POST /api/open` launches the validated target with a direct argument
  vector and no command shell/string concatenation.

### Browser interaction hardening

- Modal Filmstrip is a bounded, vertical rail on the left. The manual and
  transient forms use the same source order and vertical keyboard/scroll model.
- Modal right-click opens a product action menu for Favorite level, displayed
  Original/Enhanced selection, guarded external open, Album add, Filmstrip,
  metadata, zoom reset, and confirmed Recycle. Image bitmap/file clipboard copy
  is not implemented by this checkpoint.
- Settings uses one bounded scrolling panel and closes from its backdrop.
  Buttons expose visible hover/focus states, and Enhanced thumbnail borders use
  a solid cyan default instead of the retired rainbow presentation.

### WPF interaction hardening

- Landing has native minimize, maximize/restore, and close controls. App
  Settings is pinned to the lower end of the left sidebar and remains available
  on Landing.
- Modal uses the full window image area with `Uniform` fit, one zoom indicator,
  functional top controls, 28%/44%/28% left-center-right pointer zones, image
  and empty-area chrome toggle, and a bounded vertical Filmstrip on the left.
- A succeeded managed Enhanced output is selected by default; the Original /
  Enhanced control and `E` switch the displayed asset without starting work.
- Modal right-click exposes Favorite, Original/Enhanced, guarded external open,
  Show in folder, Album add, Filmstrip, and confirmed source Recycle. Image
  bitmap/file clipboard copy remains outside this checkpoint.
- Gallery Ctrl+wheel is bound to the active Grid surface and its scrollbar while
  preserving input/overlay/non-gallery isolation; List row geometry remains
  fixed. Middle-click starts native
  gallery auto-scroll and Escape/capture loss stops it.
- Grid and List expose direct Favorite controls. Small cards retain bounded,
  usable geometry with a neutral background; the rainbow card presentation is
  removed and legacy `rainbow` preference is normalized to solid cyan.
- App Settings has a single scroll owner and backdrop close. All shared button
  styles have enabled-only hover/focus feedback and dark non-empty tooltips.
- Prompt display strips emphasis-only wrapper characters such as `((...))`,
  `[...]`, and `{...}` without adding Browser-inconsistent emphasis notation.

## Verified at this working checkpoint

| Gate | Confirmed result |
| --- | --- |
| Browser full unit | 69 files / 635 tests passed |
| Browser typecheck / lint / production build | green |
| WPF focused UI/store verifiers | green: Album store, Modal interaction/context action, gallery zoom/anchor + middle auto-scroll, Settings/backdrop/hover, thumbnail status borders, prompt tag normalization, direct gallery Favorite |
| WPF Release build | 0 warnings / 0 errors |

These results prove the focused contracts only. They do not promote this
working delta to a final green checkpoint.

## Public-readiness review boundary

- The attached public-readiness review was incorporated as a risk packet. It
  found the local Album goal and design direction sound, but did not provide a
  complete independent code review and did not make PR #322 or the repository a
  publication candidate.
- Browser `POST /api/open` now uses the adopted direct argument-vector launch;
  the reviewed command-shell construction must not return.
- `package.json` now binds both `dev` and `start` explicitly to `127.0.0.1`, and
  mutating local API routes are being placed behind the shared Origin/Host
  guard. Those changes are present in the worktree but their focused/full test
  gate is still **PENDING**.
- A repository `LICENSE` decision has not been made. Public repository or
  distribution readiness is therefore **NO-GO**; this remains a private/local
  product lane with deployment prohibited.
- A full Codex Security scan was not run. External consultation is prohibited
  in this lane and that scan requires a separately authorized setup, so its
  absence must not be presented as a security clearance.

## PENDING FINAL GATE

The following evidence is intentionally not reported green or complete yet:

1. focused and normal-scale full Browser/WPF regression gates on the final
   source, including the loopback and mutating-API guard changes;
2. isolated Browser runtime on port 3001 or higher, including console and
   updated Modal/Settings/context-menu behavior;
3. normal WPF launcher/runtime provenance on the final source;
4. final Browser/WPF concurrency/regression checks at normal catalog scale;
5. final local-main commit SHA and post-commit PR/local/origin/tree provenance;
6. GitHub issue comments and Tools SQLite rows using that exact final SHA.

The user explicitly removed 20,000/100,000 catalog-scale runs and aggregate
check-count tracking from this review-hardening gate. Their recorded historical
green results remain valid historical evidence and are not edited or promoted
to evidence for this delta; they also do not require a rerun for this
checkpoint.

Until all applicable gates are green, this checkpoint is **in progress**. PR
#322 stays **MERGE FROZEN**, and no push/merge/close is authorized.
