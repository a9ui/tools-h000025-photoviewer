# Issue 4 Heavy Work Isolation

Recorded: 2026-06-25

## Scope

This note records the isolation proof for GitHub Issue #4.

The target is not a speedup number. The target is proving that ordinary
browsing, preview reads, and modal reads do not enqueue enhancement jobs or
start the enhancement worker.

## Instrumentation

The app now records lightweight in-process counters:

- `enhancementEnqueues`
- `enhancementWorkerStarts`

The counters are exposed through:

```text
GET /api/enhance/isolation
```

The endpoint only reads counters and queue state. It does not start enhancement
work.

## Verified Boundary

Heavy enhancement work starts only through explicit enhancement actions:

- `POST /api/enhance/jobs`
- `POST /api/enhance/jobs/[id]/retry`

Ordinary reads remain passive:

- `GET /api/enhance/jobs`
- `GET /api/enhance/output`
- image preview and modal job status polling

## Unit Evidence

`src/lib/enhance/jobStore.test.ts` verifies:

- Listing jobs leaves both counters at `0`.
- Creating an enhancement job increments enqueue count to `1`.
- Starting the enhancement queue increments worker-start count to `1`.

This matches the M1 heavy-work budget:

| Metric | Required | Verified |
| --- | ---: | ---: |
| ordinaryBrowsingEnhancementEnqueues | 0 | 0 |
| ordinaryBrowsingWorkerStarts | 0 | 0 |

## Remaining Notes

This proves the server-side start boundary and exposes a live diagnostic
endpoint. A later browser fixture can call `/api/enhance/isolation` before and
after scripted preview/modal navigation to include the same counters in a full
Playwright browsing artifact.
