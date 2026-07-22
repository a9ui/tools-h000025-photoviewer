import path from 'path';

import { describe, expect, it } from 'vitest';

import { DERIVED_CACHE_ROOT_ENV, resolveDerivedCacheRoot } from './derivedCacheRoot';

describe('resolveDerivedCacheRoot', () => {
  it('keeps the established checkout-local cache by default', () => {
    const cwd = path.resolve('synthetic-project');
    expect(resolveDerivedCacheRoot({}, cwd)).toBe(path.join(cwd, '.cache'));
  });

  it('accepts an absolute derived-cache override', () => {
    const target = path.resolve('synthetic-temp', 'derived');
    expect(resolveDerivedCacheRoot({ [DERIVED_CACHE_ROOT_ENV]: target })).toBe(target);
  });

  it.each(['', 'relative-cache', `bad\0cache`])('rejects unsafe override %j', (value) => {
    expect(() => resolveDerivedCacheRoot({ [DERIVED_CACHE_ROOT_ENV]: value }))
      .toThrow(`${DERIVED_CACHE_ROOT_ENV} must be an absolute directory path.`);
  });
});
