import type { KeyBindings } from './types';

export interface KeyBindingConflict {
  normalizedKey: string;
  actions: Array<keyof KeyBindings>;
}

/**
 * `KeyboardEvent.key` is case-sensitive for printable keys. Bindings are not:
 * F and f would otherwise dispatch the same viewer action.
 */
export function normalizeKeyBinding(key: string): string {
  const trimmed = key.trim();
  if (trimmed) return trimmed.toLocaleLowerCase();
  return key === ' ' ? 'space' : '';
}

export function getKeyBindingConflicts(
  bindings: Partial<KeyBindings>,
): KeyBindingConflict[] {
  const byKey = new Map<string, Array<keyof KeyBindings>>();
  for (const [rawAction, value] of Object.entries(bindings) as Array<[keyof KeyBindings, unknown]>) {
    if (typeof value !== 'string') continue;
    const normalizedKey = normalizeKeyBinding(value);
    if (!normalizedKey) continue;
    const actions = byKey.get(normalizedKey) ?? [];
    actions.push(rawAction);
    byKey.set(normalizedKey, actions);
  }

  return Array.from(byKey.entries())
    .filter(([, actions]) => actions.length > 1)
    .map(([normalizedKey, actions]) => ({ normalizedKey, actions }));
}

export function hasKeyBindingConflicts(bindings: Partial<KeyBindings>): boolean {
  return getKeyBindingConflicts(bindings).length > 0;
}
