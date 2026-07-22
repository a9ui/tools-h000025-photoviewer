# Issue #342 Browser critical-path evidence

The harness reuses the existing production build by default. Run `pnpm build`
only after stopping the normal PhotoViewer server, or pass `--build` when that
shared-output mutation is explicitly intended and no normal server is running.
`--skip-build` remains accepted as an explicit no-build marker for retained
commands.

This evidence covers the Browser runtime only. All image, cache, state, and trace
inputs were deterministic synthetic fixtures under a fresh OS TEMP child. The
trace payloads contain numeric timings and counts, not source paths.

## Reproducible slice

- 10,000 PNG files across two roots; 96 are 3072 x 2048 and the remainder are
  96 x 64.
- A: cold process, scan cache, and thumbnail cache.
- B: cold process, warm scan cache, and cold thumbnail cache.
- C: cold process, warm scan cache, and warm first-two-viewport thumbnails.
- Ten runs per condition, alternating A-B-C and C-B-A order.
- User path: landing, folder scan, first grid page, first viewport 90% painted,
  next viewport 90% painted, then focused modal display.

Trace-disabled versus trace-enabled p95 deltas were below the 10% gate: total
-0.4%, CPU +0.3%, peak working set +3.5%, reads -0.9%, writes -0.6%, initial
fill +6.1%, and continued fill -5.3%.

## Diagnosis and rejected probes

Cold scan was confirmed by a 519.6 ms A-versus-B median delta. Its 588.1 ms
route median was dominated by metadata reads (472.1 ms), while warm scan was
about 65 ms. Increasing metadata in-flight work with safe independent buffers
at batch sizes 2, 4, and 8 improved the exploratory median by less than 15%, so
that change was rolled back.

Cold thumbnail generation was also confirmed. With warm scan and cold
thumbnails, continued fill p95 was 416.0 ms, inter-paint gap p95 was 251.6 ms,
Sharp p95 was 202.0 ms, and queue p95 was 187.5 ms. Warm thumbnails reduced
continued fill p95 to 84.0 ms. Changing thumbnail WebP effort from 2 to 0
worsened Sharp p95 to 251.1 ms and the inter-paint gap to 267.3 ms, so that
change was rolled back.

## Adopted change

The thumbnail queue already bounds cross-image work at up to 12 jobs. The old
configuration also gave every image up to 12 libvips threads. A bounded B-only
five-run sweep separated the two pools:

| Sharp threads per image | Sharp p95 | Continued fill p95 | Gap p95 | Modal p95 | CPU p95 |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 158.1 ms | 250.8 ms | 216.5 ms | 438.8 ms | 4,187.5 ms |
| 2 | 220.9 ms | 366.9 ms | 200.0 ms | 508.9 ms | 4,625.0 ms |
| 4 | 224.5 ms | 399.2 ms | 217.5 ms | 489.3 ms | 4,828.1 ms |

The production candidate therefore keeps cross-image queue concurrency but
sets Sharp/libvips to one thread per image.

## Full before/after gate

The corrected B baseline and the fixed-one-thread candidate both use the same
paint observer and ten runs.

| B metric (p95) | Baseline | Candidate | Delta |
| --- | ---: | ---: | ---: |
| Sharp | 202.0 ms | 161.4 ms | -20.1% |
| Queue | 187.5 ms | 1.6 ms | -99.1% |
| Continued thumbnail fill | 416.0 ms | 266.3 ms | -36.0% |
| Maximum inter-paint gap | 251.6 ms | 216.3 ms | -14.0% |
| Focused modal display | 570.9 ms | 465.9 ms | -18.4% |
| Response-to-paint | 27.6 ms | 28.0 ms | +1.4% |
| CPU | 5,140.6 ms | 4,140.6 ms | -19.5% |
| Peak working set | 419,794,944 B | 336,699,392 B | -19.8% |
| Reads | 16,052,783 B | 15,915,134 B | -0.9% |
| Writes | 36,929 B | 36,695 B | -0.6% |

The 216.3 ms gap p95 contains one run above the exploratory 200 ms target; the
ten-run distribution was 116.6, 133.9, 149.2, 150.7, 166.8, 167.0, 182.3,
183.0, 200.0, and 216.3 ms. The distribution and all dominant server and user
waits improved, so this remains a recorded follow-up rather than a rollback
condition.

Condition A total p95 changed by +1.0%, and condition C total p95 changed by
+1.3%. No CPU, memory, read, or write p95 regressed by more than 10%.

## Evidence files

- `evidence/issue-342-baseline.json`: original A/B/C baseline and trace gate.
- `evidence/issue-342-thumbnail-paint-baseline.json`: corrected B/C paint
  baseline.
- `evidence/issue-342-webp-effort-zero.json`: rejected WebP effort probe.
- `evidence/issue-342-sharp-threads-{1,2,4}-probe.json`: bounded thread sweep.
- `evidence/issue-342-sharp-threads-1-candidate.json`: accepted A/B/C gate.

The next independent performance slice is focused-display slot reservation,
only if a new trace shows modal queue wait remains above its target. It is not
part of this change.
