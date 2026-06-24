# PhotoViewer Performance Baseline Policy

This policy records the PRO-reviewed measurement rules for H000025. Any
optimization PR must either create the baseline evidence first or compare
against an existing baseline captured with the same rules.

## Required Environment Record

- Commit SHA.
- Windows version.
- CPU, RAM, storage type, and power mode.
- Node and pnpm versions.
- Build mode and command.
- Cache state.
- Fixture manifest: file count, total bytes, dimensions, formats, directory
  depth, malformed files, and locked files.
- Scenario steps.
- Correctness evidence such as counts or hashes.

## Accepted Metrics

- Launch: cold and warm process start to shell interactive, first visible image,
  and viewport ready.
- Scan: first correct result, progress-update gaps, full completion, result
  count, cancellation behavior, and UI responsiveness.
- Thumbnails: first visible thumbnail, viewport fill, max queued work, max
  in-flight work, stale cancellation, cache state, and eventual completion.
- Modal: cached input-to-displayed-image p95, uncached input-to-loading-state,
  and uncached input-to-correct-image.
- Local APIs: route p50, p95, max diagnostic, error count, result count, and
  payload size.
- Heavy work boundary: ordinary browsing must produce 0 enhancement enqueues and
  0 worker starts.
- Runtime lightness: CPU time, peak working set, repeated-navigation memory,
  disk activity, and UI long tasks.

## Sample Rules

- Launch: at least 10 cold and 10 warm observations.
- Scan: at least 5 observations.
- Modal, thumbnails, and APIs: at least 30 observations.
- Do not use p95 for tiny samples.
- A relative improvement counts only when it is at least 10% and at least twice
  the observed noise.
- If variance exceeds 10%, fix the harness before claiming an optimization.
- Compare base and candidate with the same machine, fixture, build mode, and
  cache state. Prefer interleaved base/head runs where practical.

## Initial Gates

- Cached modal p95 input-to-displayed-image: <= 100ms.
- Uncached modal input-to-loading-state: <= 100ms.
- Scan progress gaps: <= 500ms while scanning.
- Ordinary browsing enhancement enqueues: 0.
- Ordinary browsing enhancement worker starts: 0.
- Critical API, CPU, peak memory, and disk regressions: <= 10%.

## Guardrails

- Faster first result must not hide worse completion time or resource use.
- Cold and warm cache states must be labeled and enforced.
- Measurements must use production builds, not dev or HMR.
- Rapid navigation must not show stale or wrong images.
- Thumbnail cache invalidation must survive delete and file changes.
- Visible-first scheduling must eventually drain background work.
- Higher concurrency must not freeze input or saturate disk.
- API semantics must preserve status codes, order, counts, paths, and errors.
- Windows path cases must include case-insensitive paths, Unicode, long paths,
  locked files, and UNC where relevant.
- Date/time tests must pin timezone or use timezone-stable inputs.
- Heavy work must be tested at central enqueue/start boundaries.
- Feature preservation must cover search, tags, favorites, date sections,
  delete, open, settings, preview, and modal workflows.
