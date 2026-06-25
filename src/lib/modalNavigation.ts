export type ModalSwipeNavigation = 'prev' | 'next' | null;
export type ModalClickTarget = 'image' | 'empty';
export type ModalClickAction = 'toggleChrome' | 'close' | 'prev' | 'next';

export interface ModalOrderReconcileResult {
  orderedIds: string[];
  selectedId: string | null;
}

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

function uniqueImageIds(ids: string[]) {
  return Array.from(new Set(ids.filter(Boolean)));
}

export function reconcileModalOrderAfterFilterChange(
  currentId: string | null,
  previousOrderedIds: string[],
  nextOrderedIds: string[]
): ModalOrderReconcileResult {
  const orderedIds = uniqueImageIds(nextOrderedIds);
  if (orderedIds.length === 0) {
    return { orderedIds, selectedId: null };
  }

  const orderedIdSet = new Set(orderedIds);
  if (currentId && orderedIdSet.has(currentId)) {
    return { orderedIds, selectedId: currentId };
  }

  const previousIds = uniqueImageIds(previousOrderedIds);
  const previousIndex = currentId ? previousIds.indexOf(currentId) : -1;
  if (previousIndex < 0) {
    return { orderedIds, selectedId: orderedIds[0] ?? null };
  }

  for (let i = previousIndex + 1; i < previousIds.length; i++) {
    if (orderedIdSet.has(previousIds[i])) {
      return { orderedIds, selectedId: previousIds[i] };
    }
  }

  for (let i = previousIndex - 1; i >= 0; i--) {
    if (orderedIdSet.has(previousIds[i])) {
      return { orderedIds, selectedId: previousIds[i] };
    }
  }

  return { orderedIds, selectedId: orderedIds[0] ?? null };
}
