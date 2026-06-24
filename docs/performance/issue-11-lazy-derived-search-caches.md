# Issue 11 Lazy Derived Search Caches

Recorded: 2026-06-24

## Scope

This note records the local before/after probe for GitHub Issue #11.

The change reduces first search/filter work in `src/lib/indexer.ts`:

- Sort arrays are now created lazily for only the requested `sortBy`.
- Search text is now created only when the query contains terms.

## Probe

Command:

```powershell
pnpm exec vitest run src/lib/indexer.perf.test.ts --reporter=verbose
```

Probe shape:

- 50,000 synthetic `ImageFile` records.
- 7 observations per scenario.
- `setIndex(images)` before every observation to force first-search derived cache work.
- Timed only the `searchIndex(...)` call.
- The temporary probe file was removed after recording these numbers.

## Results

| Scenario | Before median | After median | Reduction | Speedup |
| --- | ---: | ---: | ---: | ---: |
| Blank query, `sortBy=name` | 562.35 ms | 161.99 ms | 71.2% | 3.47x |
| Query `marker-49999`, `sortBy=name` | 545.97 ms | 177.06 ms | 67.6% | 3.08x |

## Interpretation

The main user-visible win is first filter/search responsiveness after a new
index is loaded. Blank browsing and folder/date filtering no longer pay the
cost of building unused search text, and a first request no longer builds four
unused sort arrays.
