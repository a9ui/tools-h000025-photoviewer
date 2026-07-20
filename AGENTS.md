# PhotoViewer Agent and Contributor Guidance

This file contains repository-local guidance for human contributors and coding
agents. It is intentionally self-contained and must not depend on a private
workspace, local database, external chat project, or machine-specific path.

## Read order

1. `README.md`
2. `SECURITY.md`
3. `PROJECT.md`
4. `DESIGN.md`
5. `START_HERE.md`
6. `docs/photoviewer-authoritative-spec.md`
7. the source and tests directly related to the task

When documentation conflicts with current source or tests, do not silently pick
one. Determine the intended product invariant, update code and documentation in
the same change when appropriate, and record any unresolved conflict.

## Repository workflow

- GitHub issues, pull requests, Actions, and the Git history are the durable
  public project record.
- Keep changes narrow, reviewable, and grounded in current `main`.
- Do not claim a test or runtime check passed unless it was executed for the
  exact candidate revision.
- Use draft pull requests for incomplete security, migration, publication, or
  cross-surface work.
- Do not publish secrets, personal images, local state, private absolute paths,
  or scanner reports containing sensitive values.

## Product invariants

- PhotoViewer is a local-first viewer, not a hosted gallery or upscaler.
- The Browser runtime binds to `127.0.0.1`; LAN and Internet exposure are
  unsupported.
- Normal browsing, scanning, search, preview, modal navigation, and thumbnail
  decode must not enqueue Enhancement jobs or start workers.
- Source images are not rewritten by viewing, indexing, or Enhancement.
- Source deletion uses the Windows Recycle Bin only. Hard-delete fallback is
  prohibited.
- Browser/WPF shared state must preserve latest-on-disk merge, malformed/newer
  version protection, bounded locking, and atomic publication.
- User cache and state must not be deleted as a repair or migration shortcut.
- WinForms is frozen. Do not add features or parity work there.
- Do not restore removed legacy UI or replace the current Grid, Modal, Sidebar,
  or ImageContext with older implementations.

## Security-sensitive work

For local APIs, filesystem access, image parsing, external processes, shared
state, or GitHub workflow changes:

- identify the untrusted input and trust boundary,
- trace the closest relevant control and dangerous sink,
- preserve active-session, path, type, existence, and canonical ownership
  checks,
- use argument arrays with `shell: false` for process execution,
- bound input size, allocation, concurrency, retries, and timeouts,
- use disposable fixtures and add regression tests for the actual failure path.

Repository publication follows `docs/public-repository-policy.md` and
`docs/publication-runbook.md`. Renaming or changing visibility is a separate
administrative operation and requires the final human approval defined there.

## Verification

Run Browser checks sequentially:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1
```

Build WPF in Release and run the focused verifier for the changed surface:

```powershell
dotnet build .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -c Release --nologo
```

Do not use a real image library, shared user state, or the normal user-owned
port 3000 for tests. Do not rebuild the shared `.next` output behind a live
production process.
