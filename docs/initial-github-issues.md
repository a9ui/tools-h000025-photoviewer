# Initial GitHub Issues

## M0: Project spine and baseline

Acceptance:

- GitHub repository exists and is private.
- `project.toml` contains the GitHub URL and ChatGPT Project URL.
- GitHub Actions verify runs on PRs and main.
- Local verify result is recorded.
- M1 baseline issues exist.
- M0 setup failures and fixes are recorded in `docs/operations-log.md`.

## M1: Define repeatable PhotoViewer performance baseline

Acceptance:

- Pick representative local test folders.
- Measure launch to first usable screen.
- Measure scan time to first visible result and scan completion.
- Measure visible thumbnail fill time.
- Measure modal previous/next latency.
- Measure local API timings for scan, search, image, thumbnail, settings,
  favorites, tags, delete, and open flows.
- Record the baseline report in `docs/performance/`.

## M0: Ask PRO to review the current plan and success metrics

Acceptance:

- Prepare a compact review packet with current code state, roadmap, constraints,
  local verify result, and proposed performance metrics.
- Send it to the new ChatGPT Project using GPT PRO.
- Record adopted feedback in `PROJECT.md`, `DESIGN.md`, or follow-up issues.
- Record rejected or deferred feedback with a short reason.

## M1: Prove optional heavy jobs are isolated

Acceptance:

- Opening, previewing, and modal navigation do not start optional heavy jobs.
- Queue state remains idle during normal browsing.
- Add or update tests around this isolation if practical.

## M2: Reduce visible browsing jank

Acceptance:

- Identify redundant renders or state updates around grid, sidebar, preview,
  and modal.
- Reduce one bottleneck per PR.
- Include before/after measurement or profiler notes.
- Preserve current user-facing behavior.

## M3: Lighten scan and local API path

Acceptance:

- Identify redundant filesystem or metadata work.
- Improve cache and invalidation behavior where safe.
- Keep API compatibility.
- Include before/after measurement.
