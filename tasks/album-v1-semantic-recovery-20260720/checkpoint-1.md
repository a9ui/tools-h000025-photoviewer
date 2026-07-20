# Checkpoint 1 — shared store and operation semantics

## Scope

This checkpoint is a descendant of local main / PR #322 baseline `8914935`.
It adds the Album v1 durable core without changing current Browser UI,
ImageContext, Grid, Modal, Sidebar, Delete, Favorite, Filmstrip, Search History,
zoom anchor, Enhancement, or WinForms.

## Implemented

- Browser `albums.json` v1 reader/mutator and operation-only API routes.
- WPF reader/mutator using the same schema, revision and lock protocol.
- opaque Album/member ids; create/update/delete/add/remove/recent/cleanup paths.
- canonical path membership identity and idempotent bulk add.
- latest-on-disk mutation, expected-revision conflict, create-new shared lock,
  flushed temporary file, atomic publish, malformed/future refusal and unknown
  root/Album/member field preservation.
- task contract, Claude commit disposition and #322 operational freeze evidence.

## Verification

- focused Album core/shared-root: 20 tests green across four files;
- cross-runtime: Browser revision 0→2, WPF create/add/update/cleanup 2→6,
  Browser create 6→7; stale write rejected; unknown fields preserved; lock/temp
  residue 0;
- full Browser unit: 66 files passed, 3 skipped; 607 tests passed, 3 skipped;
- Browser typecheck, ESLint and production build green; build lists all four
  Album API route groups;
- WPF Release build: 0 warnings, 0 errors;
- `verify-wpf-product.ps1 -SkipStress`: exit 0 including the new Album verifier
  and the existing Delete/Favorite/Filmstrip/Search History/zoom/Enhancement
  regression matrix.

## Explicitly not complete

Browser/WPF Album library/picker, availability resolution, Album Gallery/Modal
source, Filmstrip navigation, source-session guard, shortcut migration, dialog
focus, Recycle cleanup wiring, 100k Album scale and isolated Browser/WPF UI
runtime evidence remain for later checkpoints. PR #322 push/merge/close remains
outside this lane and requires explicit user approval.
