import { describe, expect, it } from 'vitest';

import { formatServerTiming, isPerfTraceEnabled, roundPerfMs } from './perfTrace';

describe('perfTrace', () => {
  it('requires the exact opt-in value', () => {
    expect(isPerfTraceEnabled({ PV_PERF_TRACE: '1' })).toBe(true);
    expect(isPerfTraceEnabled({ PV_PERF_TRACE: 'true' })).toBe(false);
    expect(isPerfTraceEnabled({})).toBe(false);
  });

  it('formats bounded numeric Server-Timing values', () => {
    expect(roundPerfMs(-2)).toBe(0);
    expect(formatServerTiming([
      ['queue', 12.34],
      ['sharp', 45.67],
      ['invalid', Number.NaN],
    ])).toBe('queue;dur=12.3, sharp;dur=45.7');
  });
});
