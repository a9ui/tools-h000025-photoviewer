import { describe, expect, it } from 'vitest';
import {
  clampModalEdgeRatio,
  getModalClickAction,
  getSwipeNavigation,
  reconcileModalOrderAfterFilterChange,
} from './modalNavigation';

describe('getSwipeNavigation', () => {
  it('moves to the next image when dragging left far enough', () => {
    expect(getSwipeNavigation(-130, 500, 800)).toBe('next');
  });

  it('moves to the previous image when dragging right far enough', () => {
    expect(getSwipeNavigation(130, 500, 800)).toBe('prev');
  });

  it('accepts a fast flick even when distance is short', () => {
    expect(getSwipeNavigation(-60, 80, 800)).toBe('next');
    expect(getSwipeNavigation(60, 80, 800)).toBe('prev');
  });

  it('ignores small slow drags', () => {
    expect(getSwipeNavigation(40, 300, 800)).toBeNull();
  });
});

describe('getModalClickAction', () => {
  it('navigates previous from the left 28 percent', () => {
    expect(getModalClickAction('image', 100, 1000)).toBe('prev');
    expect(getModalClickAction('empty', 279, 1000)).toBe('prev');
  });

  it('navigates next from the right 28 percent', () => {
    expect(getModalClickAction('image', 720, 1000)).toBe('next');
    expect(getModalClickAction('empty', 900, 1000)).toBe('next');
  });

  it('toggles chrome for center image clicks', () => {
    expect(getModalClickAction('image', 500, 1000)).toBe('toggleChrome');
  });

  it('closes for center empty-space clicks', () => {
    expect(getModalClickAction('empty', 500, 1000)).toBe('close');
  });

  it('uses custom edge ratios', () => {
    expect(getModalClickAction('image', 240, 1000, 0.24)).toBe('prev');
    expect(getModalClickAction('image', 250, 1000, 0.24)).toBe('toggleChrome');
    expect(getModalClickAction('empty', 760, 1000, 0.24)).toBe('next');
    expect(getModalClickAction('empty', 750, 1000, 0.24)).toBe('close');
  });

  it('clamps unsafe edge ratios', () => {
    expect(clampModalEdgeRatio(0.03)).toBe(0.1);
    expect(clampModalEdgeRatio(0.55)).toBe(0.4);
    expect(clampModalEdgeRatio(Number.NaN)).toBe(0.28);
  });
});

describe('reconcileModalOrderAfterFilterChange', () => {
  it('keeps the current image when it remains visible', () => {
    expect(
      reconcileModalOrderAfterFilterChange('b', ['a', 'b', 'c'], ['a', 'b', 'c'])
    ).toEqual({ orderedIds: ['a', 'b', 'c'], selectedId: 'b' });
  });

  it('selects the next visible image when the current image is filtered out', () => {
    expect(
      reconcileModalOrderAfterFilterChange('b', ['a', 'b', 'c', 'd'], ['a', 'c', 'd'])
    ).toEqual({ orderedIds: ['a', 'c', 'd'], selectedId: 'c' });
  });

  it('selects the previous visible image when the removed image was last', () => {
    expect(
      reconcileModalOrderAfterFilterChange('d', ['a', 'b', 'c', 'd'], ['a', 'b', 'c'])
    ).toEqual({ orderedIds: ['a', 'b', 'c'], selectedId: 'c' });
  });

  it('returns no selection when no filtered images remain', () => {
    expect(
      reconcileModalOrderAfterFilterChange('a', ['a'], [])
    ).toEqual({ orderedIds: [], selectedId: null });
  });

  it('falls back to the first visible image if the previous order is missing the current image', () => {
    expect(
      reconcileModalOrderAfterFilterChange('x', ['a', 'b'], ['b', 'c'])
    ).toEqual({ orderedIds: ['b', 'c'], selectedId: 'b' });
  });
});
