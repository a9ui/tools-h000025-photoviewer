import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ImageFile } from '../lib/types';
import { useImageStore } from '../store/ImageContext';
import RightPreviewPanel from './RightPreviewPanel';

vi.mock('../store/ImageContext', () => ({
  useImageStore: vi.fn(),
}));

vi.mock('./EnhanceQueuePanel', () => ({
  EnhanceSettingsControls: () => null,
  createEnhancementJob: vi.fn(),
  getEnhancementSettings: vi.fn(),
}));

const setView = vi.fn();

const previewImage: ImageFile = {
  id: 'C:/images/preview.png',
  filename: 'preview.png',
  absolutePath: 'C:/images/preview.png',
  fileUrl: '/api/image?preview',
  displayUrl: '/api/image?preview&display=1',
  fullUrl: '/api/image?preview&full=1',
  metadata: { prompt: '', negativePrompt: '', settings: {} },
  createdAt: 1,
  mtime: 1,
};

function createStore(options: { width?: number; withPreview?: boolean; selectedIds?: string[]; favorites?: Record<string, number>; searchResults?: ImageFile[] } = {}) {
  const withPreview = options.withPreview ?? false;
  return {
    previewTabIds: withPreview ? [previewImage.id] : [],
    activePreviewId: withPreview ? previewImage.id : null,
    previewById: withPreview ? { [previewImage.id]: previewImage } : {},
    cycleFavoriteLevel: vi.fn(),
    decreaseFavoriteLevel: vi.fn(),
    setFavoriteLevels: vi.fn(),
    adjustFavoriteLevels: vi.fn(),
    favorites: options.favorites ?? {},
    openExternal: vi.fn(),
    selectedIds: options.selectedIds ?? [],
    searchResults: options.searchResults ?? [],
    clearSelection: vi.fn(),
    deleteImage: vi.fn(),
    view: {
      rightPanelOpen: true,
      rightPanelWidth: options.width ?? 320,
      enhanceQueueOpen: true,
    },
    setView,
    confirmBeforeDelete: true,
    setConfirmBeforeDelete: vi.fn(),
  } as unknown as ReturnType<typeof useImageStore>;
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(useImageStore).mockReturnValue(createStore());
});

describe('RightPreviewPanel resize separator', () => {
  it('keeps the empty panel resize handle as an accessible vertical separator', () => {
    render(<RightPreviewPanel />);

    const separator = screen.getByRole('separator', { name: 'Resize preview panel' });
    expect(separator).toHaveAttribute('aria-orientation', 'vertical');
    expect(separator).toHaveAttribute('aria-valuemin', '240');
    expect(separator).toHaveAttribute('aria-valuemax', '900');
    expect(separator).toHaveAttribute('aria-valuenow', '320');
    expect(separator).toHaveAttribute('tabindex', '0');
  });

  it('preserves mouse resizing and persists the released width', () => {
    render(<RightPreviewPanel />);
    const separator = screen.getByRole('separator', { name: 'Resize preview panel' });

    fireEvent.mouseDown(separator, { button: 0, clientX: 600 });
    fireEvent.mouseMove(document, { clientX: 500 });
    expect(separator).toHaveAttribute('aria-valuenow', '420');
    fireEvent.mouseUp(document);

    expect(setView).toHaveBeenLastCalledWith({ rightPanelWidth: 420 });
  });

  it('clamps keyboard resizing and persists every keyboard width change', () => {
    render(<RightPreviewPanel />);
    const separator = screen.getByRole('separator', { name: 'Resize preview panel' });

    separator.focus();
    fireEvent.keyDown(separator, { key: 'ArrowLeft' });
    expect(separator).toHaveAttribute('aria-valuenow', '340');
    expect(setView).toHaveBeenLastCalledWith({ rightPanelWidth: 340 });

    fireEvent.keyDown(separator, { key: 'ArrowRight', shiftKey: true });
    expect(separator).toHaveAttribute('aria-valuenow', '240');
    expect(setView).toHaveBeenLastCalledWith({ rightPanelWidth: 240 });

    fireEvent.keyDown(separator, { key: 'End' });
    expect(separator).toHaveAttribute('aria-valuenow', '900');
    fireEvent.keyDown(separator, { key: 'ArrowLeft', shiftKey: true });
    expect(separator).toHaveAttribute('aria-valuenow', '900');
    expect(setView).toHaveBeenLastCalledWith({ rightPanelWidth: 900 });
  });

  it('uses the same separator in the normal preview panel', () => {
    const { rerender } = render(<RightPreviewPanel />);
    vi.mocked(useImageStore).mockReturnValue(createStore({ withPreview: true }));
    rerender(<RightPreviewPanel />);

    expect(screen.getAllByRole('separator', { name: 'Resize preview panel' })).toHaveLength(1);
  });
});

describe('RightPreviewPanel bulk favorite levels', () => {
  it('reports mixed selection, applies an exact level once, and leaves stale paths disabled', () => {
    const selectedIds = [previewImage.id, 'C:/images/second.png', 'C:/images/stale.png'];
    const secondImage = { ...previewImage, id: 'C:/images/second.png' };
    const store = createStore({
      withPreview: true,
      selectedIds,
      searchResults: [previewImage, secondImage],
      favorites: { [previewImage.id]: 1, [secondImage.id]: 4 },
    });
    vi.mocked(useImageStore).mockReturnValue(store);

    render(<RightPreviewPanel />);

    expect(screen.getByRole('status')).toHaveTextContent('Mixed levels (Lv1, Lv4) for 2 selected');
    expect(screen.getByText('1 unavailable')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Set selected images to favorite level 3' }));
    expect(store.setFavoriteLevels).toHaveBeenCalledWith([previewImage.id, secondImage.id], 3);
    fireEvent.click(screen.getByRole('button', { name: 'Decrease favorite level for selected images' }));
    expect(store.adjustFavoriteLevels).toHaveBeenCalledWith([previewImage.id, secondImage.id], -1);
  });

  it('disables favorite mutations when every selected path is stale', () => {
    vi.mocked(useImageStore).mockReturnValue(createStore({
      withPreview: true,
      selectedIds: ['C:/images/stale.png'],
      searchResults: [],
    }));
    render(<RightPreviewPanel />);

    expect(screen.getByRole('status')).toHaveTextContent('no longer in the current result');
    expect(screen.getByRole('button', { name: 'Set selected images to favorite level 1' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Increase favorite level for selected images' })).toBeDisabled();
  });
});
