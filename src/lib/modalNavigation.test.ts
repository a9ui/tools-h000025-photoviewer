import { describe, expect, it } from 'vitest';
import {
  clampModalEdgeRatio,
  getModalClickAction,
  getSwipeNavigation,
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
