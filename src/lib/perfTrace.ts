export const PERF_TRACE_ENV = 'PV_PERF_TRACE';

export function isPerfTraceEnabled(
  environment: Readonly<Record<string, string | undefined>> = process.env,
) {
  return environment[PERF_TRACE_ENV] === '1';
}

export function roundPerfMs(value: number) {
  return Math.round(Math.max(0, value) * 10) / 10;
}

export function formatServerTiming(entries: ReadonlyArray<readonly [string, number]>) {
  return entries
    .filter(([, duration]) => Number.isFinite(duration))
    .map(([name, duration]) => `${name};dur=${roundPerfMs(duration).toFixed(1)}`)
    .join(', ');
}
