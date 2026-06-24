export type ModalSwipeNavigation = 'prev' | 'next' | null;
export type ModalClickTarget = 'image' | 'empty';
export type ModalClickAction = 'toggleChrome' | 'close' | 'prev' | 'next';

export const DEFAULT_MODAL_EDGE_RATIO = 0.28;
export const MIN_MODAL_EDGE_RATIO = 0.1;
export const MAX_MODAL_EDGE_RATIO = 0.4;

export function clampModalEdgeRatio(edgeRatio: number) {
  if (!Number.isFinite(edgeRatio)) return DEFAULT_MODAL_EDGE_RATIO;
  return Math.min(MAX_MODAL_EDGE_RATIO, Math.max(MIN_MODAL_EDGE_RATIO, edgeRatio));
}

export function getModalClickAction(
  target: ModalClickTarget,
  relativeX: number,
  width: number,
  edgeRatio = DEFAULT_MODAL_EDGE_RATIO
): ModalClickAction {
  const safeWidth = Math.max(1, width);
  const safeEdgeRatio = clampModalEdgeRatio(edgeRatio);
  const leftBoundary = safeWidth * safeEdgeRatio;
  const rightBoundary = safeWidth * (1 - safeEdgeRatio);

  if (relativeX <= leftBoundary) return 'prev';
  if (relativeX >= rightBoundary) return 'next';
  return target === 'image' ? 'toggleChrome' : 'close';
}

export function getSwipeNavigation(
  deltaX: number,
  elapsedMs: number,
  width: number
): ModalSwipeNavigation {
  const distanceThreshold = Math.min(180, Math.max(72, width * 0.16));
  const velocity = elapsedMs > 0 ? deltaX / elapsedMs : 0;
  const velocityThreshold = 0.55;

  if (deltaX <= -distanceThreshold || velocity <= -velocityThreshold) return 'next';
  if (deltaX >= distanceThreshold || velocity >= velocityThreshold) return 'prev';
  return null;
}
