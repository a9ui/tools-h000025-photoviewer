import { describe, expect, it } from 'vitest';

import { getKeyBindingConflicts, normalizeKeyBinding } from './keyBindings';
import { DEFAULT_KEY_BINDINGS } from './types';

describe('key binding conflict detection', () => {
  it('normalizes printable keys case-insensitively while preserving Space', () => {
    expect(normalizeKeyBinding(' F ')).toBe('f');
    expect(normalizeKeyBinding(' ')).toBe('space');
  });

  it('identifies every action using the same normalized key', () => {
    const bindings = { ...DEFAULT_KEY_BINDINGS, nextImage: 'F' };

    expect(getKeyBindingConflicts(bindings)).toEqual([
      { normalizedKey: 'f', actions: ['nextImage', 'toggleFavorite'] },
    ]);
  });
});
