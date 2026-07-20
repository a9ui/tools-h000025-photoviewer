# PhotoViewer Project Charter

## Goal

Keep PhotoViewer's existing local viewer workflows intact while making large
Windows image libraries fast, predictable, and safe to browse, search, compare,
organize, and optionally enhance.

The repository may be public. The application runtime remains local-only.
Public source does not imply deployment, cloud storage, account support, remote
file access, or a hosted PhotoViewer service.

## Success criteria

PhotoViewer is successful when a large local image library can be opened,
scanned, browsed, filtered, previewed, and navigated with low waiting time and
without accidental data loss or hidden heavy work.

- The first usable screen appears quickly.
- Scan and metadata work report progress and keep controls responsive.
- Thumbnail and decode work are bounded and prioritized around visible items.
- Modal navigation remains responsive and preserves the complete current order.
- Search, Favorites, Seen state, Albums, Delete, Open, and Enhancement retain
  their documented semantics.
- Optional Enhancement starts only from an explicit action.
- Source deletion uses the Windows Recycle Bin and never falls back to permanent
  deletion.
- Browser and WPF share supported state without lost updates.
- Security-sensitive inputs, allocations, locks, retries, and processes have
  explicit bounds and regression tests.

## Users

The primary user is a Windows operator browsing a large local illustration or
photo library. PhotoViewer is a focused viewer and organization tool rather than
a cloud gallery manager or image-generation service.

## Product scope

- Preserve the Next.js Browser viewer and native .NET 8 WPF viewer.
- Keep the Browser runtime on `127.0.0.1`.
- Measure startup, scan, thumbnail, modal, API, and shared-state costs.
- Reduce redundant rendering, filesystem work, decode work, and state churn.
- Maintain virtualization and full-catalog reachability.
- Keep GitHub as the source of truth for code, issues, pull requests, CI, and
  public milestone state.
- Keep WinForms frozen except for critical break/fix maintenance.

## Non-goals

- Hosting PhotoViewer on the public Internet.
- Binding the local API to a LAN or wildcard interface.
- Vercel, Cloudflare Tunnel, reverse-proxy, account, or cloud-sync deployment.
- Renaming the product as an upscaler.
- Removing user-facing viewer functions for speed.
- Starting AI or GPU work automatically.
- Deleting user cache or state to simplify migrations.

## Safety constraints

- No secrets, personal media, real user state, or unredacted home-directory
  paths in commits, issues, pull requests, logs, or screenshots.
- Source images remain read-only except for an explicit Recycle Bin operation.
- File and process actions revalidate active-session membership, path identity,
  existence, type, canonical ownership, and project-root exclusion.
- Shared state uses bounded locking, latest-on-disk mutation, malformed/newer
  version protection, and atomic publication.
- Browser and WPF parity changes must preserve product results on both surfaces;
  presentation may remain native to each platform.
- Deployment, billing, and repository visibility changes require explicit owner
  approval.

## Current roadmap

### Product correctness

Maintain the authoritative Browser/WPF contract, close regressions with focused
source-backed tests, and keep source/state ownership explicit.

### Performance

Measure before and after changes. Require scale tests when a change touches
virtualization, catalog materialization, metadata indexing, or the stress gates;
do not make large stress fixtures a ritual for unrelated changes.

### Security and public repository readiness

- Bound untrusted image and backend inputs.
- Maintain loopback Host/Origin/DNS-rebinding protection.
- Harden public fork CI and GitHub repository settings.
- Complete full-history secret and privacy review.
- Select a repository license and record third-party obligations.
- Rename the repository to `H000025-PhotoViewer` while it is still private.
- Change visibility only after the final gate and explicit human approval.

The detailed publication process is defined in
`docs/public-repository-policy.md` and `docs/publication-runbook.md`.
