# Pre-change cold baseline

Measured from revision `d284e11be0fcb34c94cc9dcf681c663fe315f58c` on
2026-07-19 with the existing TEMP-only catalog stress fixture.

Command:

```powershell
$out = Join-Path $env:TEMP ('photoviewer-wpf-metadata-cold-baseline-' + [guid]::NewGuid().ToString('N') + '.json')
& .\scripts\verify-wpf-catalog-stress.ps1 -Count 100000 -FolderCount 100 -OutputPath $out -OverallTimeoutSeconds 180
```

Fresh measurement:

- catalog ready: 3,721 ms
- background metadata: 27,176 ms
- full load: 31,269 ms
- scan: 1,842 ms
- materialize: 1,872 ms
- Grid/List realized: 15 / 9
- tail List thumbnail: 167 ms
- dispatcher maximum gap: 386 ms
- external WM_NULL maximum unresponsive streak: 374 ms
- exact catalog/source: 100,000 / 100,000; silent truncation: 0
- Enhancement reads/candidates: 0 / 0
- cleanup: succeeded; timeout: false; result: PASS

Three-run current median (the fresh run plus the two immediately preceding
100k measurements):

- catalog ready: 3,762 ms
- background metadata: 26,617 ms
- full load: 30,756 ms

The raw fresh result was written to
`%TEMP%\photoviewer-wpf-metadata-cold-baseline-b4ada004e7ba464db0e96584dea4a908.json`.
The TEMP path is evidence location only; this bounded summary is the durable
project artifact.
