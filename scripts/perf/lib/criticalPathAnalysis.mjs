export function percentile(values, requestedPercentile) {
  const finite = values.filter(Number.isFinite).sort((a, b) => a - b);
  if (finite.length === 0) return null;
  const index = Math.min(
    finite.length - 1,
    Math.max(0, Math.ceil((requestedPercentile / 100) * finite.length) - 1),
  );
  return Math.round(finite[index] * 10) / 10;
}

export function median(values) {
  return percentile(values, 50);
}

export function parseServerTiming(value) {
  if (!value) return {};
  const parsed = {};
  for (const entry of value.split(',')) {
    const [rawName, ...parameters] = entry.trim().split(';');
    if (!/^[a-z][a-z0-9_-]*$/i.test(rawName)) continue;
    const duration = parameters
      .map((parameter) => /^dur=(-?\d+(?:\.\d+)?)$/i.exec(parameter.trim()))
      .find(Boolean);
    if (!duration) continue;
    const numeric = Number(duration[1]);
    if (Number.isFinite(numeric) && numeric >= 0) parsed[rawName] = numeric;
  }
  return parsed;
}

function metricSummary(values) {
  return {
    median: median(values),
    p95: percentile(values, 95),
    max: values.filter(Number.isFinite).length > 0
      ? Math.round(Math.max(...values.filter(Number.isFinite)) * 10) / 10
      : null,
  };
}

export function summarizeRuns(runs) {
  const byCondition = {};
  for (const condition of ['A', 'B', 'C']) {
    const selected = runs.filter((run) => run.condition === condition);
    if (selected.length === 0) continue;
    const metricNames = new Set(selected.flatMap((run) => Object.keys(run.timingsMs ?? {})));
    byCondition[condition] = {
      observations: selected.length,
      metrics: Object.fromEntries([...metricNames].map((name) => [
        name,
        metricSummary(selected.map((run) => run.timingsMs?.[name])),
      ])),
      runtime: {
        cpuMs: metricSummary(selected.map((run) => run.runtime?.cpuMs)),
        peakWorkingSetBytes: metricSummary(selected.map((run) => run.runtime?.peakWorkingSetBytes)),
        readBytes: metricSummary(selected.map((run) => run.runtime?.readBytes)),
        writeBytes: metricSummary(selected.map((run) => run.runtime?.writeBytes)),
      },
    };
  }
  return byCondition;
}

function medianMetric(summary, condition, metric) {
  return summary[condition]?.metrics?.[metric]?.median ?? null;
}

export function diagnoseCriticalPath(summary) {
  const total = medianMetric(summary, 'A', 'totalPerceived');
  const stages = [
    ['startup', medianMetric(summary, 'A', 'startup')],
    ['scan', medianMetric(summary, 'A', 'scan')],
    ['postScan', medianMetric(summary, 'A', 'postScan')],
    ['initialThumbnailFill', medianMetric(summary, 'A', 'initialThumbnailFill')],
    ['continuedThumbnailFill', medianMetric(summary, 'A', 'continuedThumbnailFill')],
  ];
  const ranked = stages
    .filter(([, duration]) => Number.isFinite(duration))
    .map(([stage, duration]) => ({
      stage,
      medianMs: duration,
      share: total && total > 0 ? Math.round((duration / total) * 1000) / 1000 : null,
      dominant: Boolean(total && duration >= 150 && duration / total >= 0.35),
    }))
    .sort((a, b) => b.medianMs - a.medianMs);

  const coldScan = medianMetric(summary, 'A', 'scan');
  const warmScan = medianMetric(summary, 'B', 'scan');
  const coldThumbFill = medianMetric(summary, 'B', 'initialThumbnailFill');
  const warmThumbFill = medianMetric(summary, 'C', 'initialThumbnailFill');
  return {
    ranked,
    thresholds: {
      scanCacheDeltaMs: Number.isFinite(coldScan) && Number.isFinite(warmScan)
        ? Math.round((coldScan - warmScan) * 10) / 10
        : null,
      thumbnailCacheDeltaMs: Number.isFinite(coldThumbFill) && Number.isFinite(warmThumbFill)
        ? Math.round((coldThumbFill - warmThumbFill) * 10) / 10
        : null,
      scanConfirmed: Boolean(total && coldScan && (
        coldScan / total >= 0.4 || (warmScan != null && coldScan - warmScan >= 500)
      )),
      serverThumbnailConfirmed: Boolean(
        coldThumbFill != null && warmThumbFill != null && coldThumbFill - warmThumbFill >= 300
      ),
    },
  };
}

export function regressionPercent(baseline, candidate) {
  if (!Number.isFinite(baseline) || baseline <= 0 || !Number.isFinite(candidate)) return null;
  return Math.round((((candidate - baseline) / baseline) * 100) * 10) / 10;
}
