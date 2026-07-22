import assert from 'node:assert/strict';
import test from 'node:test';

import {
  diagnoseCriticalPath,
  parseServerTiming,
  percentile,
  regressionPercent,
  summarizeRuns,
} from './criticalPathAnalysis.mjs';

test('parses numeric Server-Timing entries and ignores malformed data', () => {
  assert.deepEqual(
    parseServerTiming('cache;dur=2.1, queue;dur=4.0, bad;dur=-1, nope;desc=x'),
    { cache: 2.1, queue: 4 },
  );
});

test('uses nearest-rank percentiles', () => {
  assert.equal(percentile([5, 1, 4, 2, 3], 50), 3);
  assert.equal(percentile([5, 1, 4, 2, 3], 95), 5);
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

  assert.equal(diagnosis.ranked[0].stage, 'scan');
  assert.equal(diagnosis.ranked[0].dominant, true);
  assert.equal(diagnosis.thresholds.scanConfirmed, true);
  assert.equal(diagnosis.thresholds.serverThumbnailConfirmed, true);
  assert.equal(regressionPercent(100, 111), 11);
});
