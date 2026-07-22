<!-- pvu-browser-enhance-companion-contract -->

# Browser enhancement companion contract

GitHub authority: [Issue #332](https://github.com/a9ui/tools-h000025-photoviewer/issues/332)

## Problem

At Browser `main` `0d8ce6d23e79cd5827d1d5282ef76953f5a870b6`, an explicit
`POST /api/enhance/jobs` can create a job only when `sourceId` is already in
the active or fallback Browser index. A local companion that starts from a
user-selected absolute image therefore receives `404 Source image is not in
the active index` unless Browser scanned the folder first.

The create lookup is case-insensitive, but `GET /api/enhance/jobs?sourceId=`
uses strict string equality. A Windows casing difference can consequently
hide a persisted job during polling or after restart. Output serving and
deletion also use lexical containment only, so a junction below `outputs/`
can resolve outside the managed root.

## Scope

- Keep `POST /api/enhance/jobs` as the sole explicit user-action boundary.
- Keep the API loopback-only through the existing local API guard.
- If no active-index entry matches, accept exactly one absolute source path;
  resolve it canonically, require an existing regular supported image, and
  reject one-shot sources inside the managed enhancement root.
- Do not add the source to the Browser index and do not scan its folder.
- Use one platform-aware canonical source-path identity for active-index
  lookup and `GET ...?sourceId=` filtering; Windows identity is
  case-insensitive.
- Canonicalize existing managed outputs before serving or deleting them and
  reject junction/symlink escapes outside the canonical `outputs/` root.
- Preserve enhancement store version 1 and its existing job fields. Do not
  introduce a shared-state schema ahead of the external contract owner.
- Reuse the existing `PVU_ENHANCE_ROOT` override and production launcher
  `--port` contract. An explicit busy port must continue to fail without
  taking over or selecting another process.

## Acceptance

- A TEMP image absent from every Browser index creates a job through the
  guarded POST without folder scanning, and a fresh store reload sees it.
- A Windows case-only `sourceId` variant returns that persisted job.
- Relative, missing, directory, unsupported, and managed-root sources fail
  without enqueueing work.
- A canonical outside-root output reached through a TEMP junction is neither
  served nor deleted.
- The source image SHA-256 is identical before and after create, poll, output
  rejection, and delete rejection.
- All fixture, state, output, and junction writes stay under a unique TEMP
  root; no user image, cache, state, or history is touched.
- Ordinary Browser browsing remains passive and never registers a source or
  starts enhancement work.

## Non-goals

- No WPF/runtime implementation change.
- No enhancement job schema/version change.
- No deployment, user-state migration, or existing checkout cleanup.
- No change to PR #331; its GitHub spending-limit failure remains a blocked
  harness condition rather than a code failure.
