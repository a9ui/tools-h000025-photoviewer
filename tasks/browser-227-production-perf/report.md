# Browser #227 Production Performance Record

Issue: #227 Browser performance follow-up from #128

This slice replaces the placeholder-only perf record path with an optional
production browser measurement mode:

```powershell
corepack pnpm perf:record -- --output tasks/browser-227-production-perf/artifacts/browser-prod-perf.json --dir .\.cache\native-fixture --observations 3
```

## Result

- Output artifact: `tasks/browser-227-production-perf/artifacts/browser-prod-perf.json`
- Build mode: production `next build` plus local `next start`
- Fixture: `.cache/native-fixture`, prepared by `scripts/start-local-native.ps1 -PrepareFixture`
- Summary: `overall=measured`, `measuredScenarioCount=6`, `pendingScenarioCount=1`
- Pending scenario: `runtime` only. The new browser record mode does not claim OS
  process CPU, working-set, or disk-byte measurements.

## Key Measurements

- Launch warm shell interactive: 149 ms
- Launch DOMContentLoaded: 38 ms
- Scan first progress: 27.7 ms
- Scan full completion: 41.9 ms
- Search observations: 3 browser fetch samples
- First visible thumbnail: 5.7 ms
- Thumbnail viewport fill proxy: 7.9 ms
- Modal/display proxy cached p95: 2.3 ms
- Modal/display proxy uncached p95: 2.8 ms
- Ordinary browsing enhancement enqueues: 0
- Ordinary browsing worker starts: 0

## Scope Notes

- This is browser-only. No WinForms, WPF, native, deployment, destructive, or
  cache/state deletion work is part of this slice.
- The modal metric is a production browser display-fetch proxy for the modal
  image path, not a visual frame-by-frame modal animation profiler.
- The command still supports the old placeholder mode when `--dir` is omitted.
