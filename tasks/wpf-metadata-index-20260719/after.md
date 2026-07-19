# Persistent metadata index 100k result

Measured on 2026-07-19 with the same TEMP-only 100,000-image / 100-folder
catalog stress shape used by `baseline.md`. The enhanced gate performs a cold
load and then opens a separate `MainWindow` over the same fixture and index.

## Result

- structural result: PASS; exact source/catalog/filtered count 100,000;
  silent truncation 0
- cold catalog ready: 3,809 ms
- cold background metadata: 26,659 ms
- cold full load: 30,850 ms
- warm catalog ready: 3,396 ms
- warm background metadata: 213 ms
- warm full load: 3,928 ms
- warm index read/write: 65 / 0 ms
- warm index accounting: 100,000 hits / 0 misses
- warm index publication: save contract succeeded, `Written=false`
- index path, SHA-256, and last-write timestamp: unchanged
- cold and warm Grid/List realization: 15 / 9, bounded
- cold and warm far-tail index: 99,999 reached with canonical and visual
  selection intact
- warm zoom anchor drift: 0 / 0 px
- dispatcher maximum gap: 424 ms overall; 245 ms in the warm phase
- external WM_NULL maximum unresponsive streak: 387 ms; gate 750 ms
- Enhancement reads/candidates: 0 / 0 in both phases
- source fixture unchanged; TEMP cleanup succeeded; timeout false

Against the fresh cold baseline, the restart-warm metadata phase fell from
27,176 ms to 213 ms (99.2% shorter), while full load fell from 31,269 ms to
3,928 ms (87.4% shorter). Catalog-ready time did not regress: 3,721 ms baseline
versus 3,396 ms warm.

Raw result:

`%TEMP%\photoviewer-wpf-metadata-index-100k-after-20260719-01.json`

The raw TEMP JSON is an evidence location only. This bounded summary and the
focused verifier are the durable project evidence.
