import { expect, test } from 'vitest';

import {
  diagnoseCriticalPath,
  parseServerTiming,
  percentile,
  regressionPercent,
  summarizeRuns,
} from './criticalPathAnalysis.mjs';

test('parses numeric Server-Timing entries and ignores malformed data', () => {
  expect(parseServerTiming('cache;dur=2.1, queue;dur=4.0, bad;dur=-1, nope;desc=x'))
    .toEqual({ cache: 2.1, queue: 4 });
});

test('uses nearest-rank percentiles', () => {
  expect(percentile([5, 1, 4, 2, 3], 50)).toBe(3);
  expect(percentile([5, 1, 4, 2, 3], 95)).toBe(5);
});

test('summarizes and diagnoses the dominant cold scan stage', () => {
  const runs = ['A', 'A', 'B', 'B', 'C', 'C'].map((condition, index) => ({
    condition,
    timingsMs: {
      totalPerceived: 1_000,
      startup: 100,
      scan: condition === 'A' ? 600 + index : 50,
      postScan: 80,
      initialThumbnailFill: condition === 'B' ? 500 : condition === 'C' ? 100 : 150,
      continuedThumbnailFill: 70,
    },
    runtime: { cpuMs: 100, peakWorkingSetBytes: 200, readBytes: 300, writeBytes: 400 },
  }));
  const summary = summarizeRuns(runs);
  const diagnosis = diagnoseCriticalPath(summary);

  expect(diagnosis.ranked[0].stage).toBe('scan');
  expect(diagnosis.ranked[0].dominant).toBe(true);
  expect(diagnosis.thresholds.scanConfirmed).toBe(true);
  expect(diagnosis.thresholds.serverThumbnailConfirmed).toBe(true);
  expect(regressionPercent(100, 111)).toBe(11);
});
