import { describe, expect, it } from 'vitest';
import {
  enhancementPathKey,
  enhancementPathsMatch,
  filterEnhancementJobsBySource,
  isPathWithinDirectory,
} from './pathContract';

describe('enhancement path contract', () => {
  it('uses one case-insensitive canonical identity for Windows create and poll paths', () => {
    const stored = 'C:\\Users\\Example\\Pictures\\Target.PNG';
    const requested = 'c:\\users\\example\\pictures\\target.png';
    const jobs = [
      { id: 'target', sourceId: stored },
      { id: 'other', sourceId: 'C:\\Users\\Example\\Pictures\\Other.png' },
    ];

    expect(enhancementPathKey(stored, 'win32')).toBe(requested);
    expect(enhancementPathsMatch(stored, requested, 'win32')).toBe(true);
    expect(filterEnhancementJobsBySource(jobs, requested, 'win32')).toEqual([jobs[0]]);
  });

  it('keeps case-sensitive platforms case-sensitive', () => {
    expect(enhancementPathsMatch('/images/Target.png', '/images/target.png', 'linux')).toBe(false);
  });

  it('does not confuse sibling prefixes with managed descendants', () => {
    expect(isPathWithinDirectory('C:\\cache\\enhance', 'c:\\cache\\enhance\\outputs\\a.png', 'win32')).toBe(true);
    expect(isPathWithinDirectory('C:\\cache\\enhance', 'C:\\cache\\enhance-old\\a.png', 'win32')).toBe(false);
    expect(isPathWithinDirectory('C:\\cache\\enhance', 'C:\\cache\\enhance', 'win32')).toBe(false);
    expect(isPathWithinDirectory('C:\\cache\\enhance', 'C:\\cache\\enhance', 'win32', true)).toBe(true);
  });
});
