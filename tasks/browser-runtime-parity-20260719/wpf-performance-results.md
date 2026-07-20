# WPF 100k startup and background-I/O performance evidence

Date: 2026-07-19 JST

## Scope and safety

- Same `verify-wpf-catalog-stress.ps1 -Count 100000 -FolderCount 100` TEMP-only fixture and machine for baseline and both after runs.
- Full catalog, filter result, and Grid ItemsSource remained exactly 100,000. Silent truncate stayed 0; Grid/List realized 15/9; tail index 99,999 stayed directly reachable.
- No user image, Favorite, Seen, Recent, thumbnail cache, Enhancement state, or port 3000 process was written or deleted.

## Reproduced waste

1. Enumeration was followed by an extra 100,000-path `File.Exists` snapshot even though `MakeFileTile` already guards disappearing sources and a second pre-commit snapshot owns the race contract.
2. Every PNG was opened once through WIC for dimensions and again for bounded `parameters` metadata.
3. Progressive metadata built full-catalog dimension/prompt dictionaries that no caller used after Tile updates.
4. Empty preview-tab and empty-selection startup paths still built 100,000-entry path sets.
5. Bulk metadata I/O did not explicitly yield to a newly scheduled viewport-thumbnail batch.

## Adopted local change

- Removed only the redundant post-enumeration existence pass; the pre-commit existence check and source-recycle generation guard remain.
- Released the FileInfo preparation list after the complete Tile catalog is published.
- Added one-pass, bounded pre-IDAT PNG IHDR + `parameters` tEXt reads with the previous malformed/non-PNG WIC fallback.
- Removed unused progressive full-catalog result dictionaries.
- Let bulk metadata wait while viewport thumbnails are in flight.
- Fast-pathed empty tab/selection reconciliation and pre-sized the resettable collection.

## Same-condition results

| Metric | Baseline | After 1 | After 2 | Replicated direction |
| --- | ---: | ---: | ---: | --- |
| Viewer/catalog ready | 5,209 ms | 3,762 ms | 4,040 ms | 22.4–27.8% faster |
| Full load | 37,192 ms | 30,756 ms | 29,196 ms | 17.3–21.5% faster |
| Background metadata | 31,551 ms | 26,617 ms | 24,697 ms | 15.6–21.7% faster |
| Dispatcher max gap | 496 ms | 425 ms | 470 ms | 5.2–14.3% lower |
| Working set after | 326,606,848 B | 319,459,328 B | 319,303,680 B | about 7.2 MB lower |
| Tail List thumbnail | 166 ms | 169 ms | 161 ms | no regression |

After artifacts:

- `%TEMP%\photoviewer-wpf-catalog-perf-after-20260719.json`
- `%TEMP%\photoviewer-wpf-catalog-perf-after2-20260719.json`

## Focused verification

- Release build: 0 warnings / 0 errors.
- `verify-wpf-scan-materialization-race.ps1`: PASS; vanished source skipped and selection/modal/state/store ownership stayed coherent.
- `verify-wpf-path-robustness.ps1`: PASS; Unicode/long path/lock/corrupt/missing-root behavior and passive Enhancement contract stayed coherent.
- `--png-metadata-smoke`: PASS; Prompt/Negative/Settings, copy state, missing metadata, unrelated tEXt, latest-selection guard all stayed correct.
- `verify-wpf-prompt-tag-search.ps1`: PASS; indexed Prompt tags still filter, dedupe, focus, persist, and reload without source or Enhancement mutation.
- Exact 100,000 / 100 folders: two consecutive after runs PASS with cleanup success, source count unchanged, Enhancement reads/candidates 0.
