import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DEFAULT_KEY_BINDINGS, type ImageFile } from '../lib/types';
import ImageModal from './ImageModal';

const store = vi.hoisted(() => ({
  deleteImage: vi.fn(),
  resolveModalNavigationTarget: vi.fn(),
  setSelectedIndex: vi.fn(),
  setModalImageIds: vi.fn(),
  searchResults: [] as Array<Record<string, unknown>>,
  selectedIndex: 0 as number | null,
}));

const fixtures = vi.hoisted(() => {
  const first = {
    id: 'C:/images/first.png',
    filename: 'first.png',
    absolutePath: 'C:/images/first.png',
    fileUrl: '/api/image?first',
    displayUrl: '/api/image?first&display=1',
    fullUrl: '/api/image?first&full=1',
    metadata: null,
    createdAt: 1,
    mtime: 1,
  };
  return {
    first,
    second: {
      ...first,
      id: 'C:/images/second.png',
      filename: 'second.png',
      absolutePath: 'C:/images/second.png',
      fileUrl: '/api/image?second',
      displayUrl: '/api/image?second&display=1',
      fullUrl: '/api/image?second&full=1',
    },
    third: {
      ...first,
      id: 'C:/images/third.png',
      filename: 'third.png',
      absolutePath: 'C:/images/third.png',
      fileUrl: '/api/image?third',
      displayUrl: '/api/image?third&display=1',
      fullUrl: '/api/image?third&full=1',
    },
  };
});

vi.mock('../store/ImageContext', () => ({
  useImageStore: () => ({
    searchResults: store.searchResults,
    searchTotal: store.searchResults.length,
    searchQuery: '',
    setSearchQuery: vi.fn(),
    selectedIndex: store.selectedIndex,
    setSelectedIndex: store.setSelectedIndex,
    modalImageIds: [],
    setModalImageIds: store.setModalImageIds,
    ensureSearchRange: vi.fn(),
    resolveModalNavigationTarget: store.resolveModalNavigationTarget,
    cycleFavoriteLevel: vi.fn(),
    decreaseFavoriteLevel: vi.fn(),
    favorites: {},
    markImageSeen: vi.fn(),
    requestRevealImage: vi.fn(),
    keyBindings: DEFAULT_KEY_BINDINGS,
    deleteImage: store.deleteImage,
    openExternal: vi.fn(),
    confirmBeforeDelete: false,
    setConfirmBeforeDelete: vi.fn(),
    view: { modalEdgeRatio: 0.28 },
    indexToken: null,
  }),
}));

vi.mock('../lib/clientImageCache', async (importOriginal) => ({
  ...await importOriginal<typeof import('../lib/clientImageCache')>(),
  loadCachedImageUrl: vi.fn(async () => '/cached-image'),
}));

const firstImage = fixtures.first as ImageFile;
const secondImage = fixtures.second as ImageFile;
const thirdImage = fixtures.third as ImageFile;

describe('ImageModal sparse navigation lock', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    store.searchResults = [fixtures.first, fixtures.second];
    store.selectedIndex = 0;
    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: true,
      json: async () => ({ jobs: [] }),
    }) as Response));
    store.resolveModalNavigationTarget.mockResolvedValue({
      status: 'found',
      index: 1,
      id: secondImage.id,
      filteredOrderedIds: null,
    });
  });

  it('releases navigation after a failed delete so next still works', async () => {
    store.deleteImage.mockResolvedValue(false);
    render(<ImageModal />);

    fireEvent.keyDown(window, { key: DEFAULT_KEY_BINDINGS.deleteImage });
    await waitFor(() => expect(store.deleteImage).toHaveBeenCalledWith(firstImage.id));
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Move image to Recycle Bin' })).not.toBeDisabled();
    });

    fireEvent.keyDown(window, { key: DEFAULT_KEY_BINDINGS.nextImage });
    await waitFor(() => {
      expect(store.resolveModalNavigationTarget).toHaveBeenCalledWith(0, 'next');
      expect(store.setSelectedIndex).toHaveBeenCalledWith(1);
    });
  });

  it('closes and clears a deleted modal when its sparse neighbor is unavailable', async () => {
    store.deleteImage.mockResolvedValue(true);
    store.resolveModalNavigationTarget.mockResolvedValue({ status: 'unavailable' });
    render(<ImageModal />);

    fireEvent.keyDown(window, { key: DEFAULT_KEY_BINDINGS.deleteImage });
    await waitFor(() => {
      expect(store.resolveModalNavigationTarget).toHaveBeenCalledWith(0, 'delete');
      expect(store.setSelectedIndex).toHaveBeenCalledWith(null);
      expect(store.setModalImageIds).toHaveBeenCalledWith([]);
    });
  });

  it('does not overwrite a newer window when delete resolution becomes stale', async () => {
    store.deleteImage.mockResolvedValue(true);
    store.resolveModalNavigationTarget.mockResolvedValue({ status: 'stale' });
    render(<ImageModal />);

    fireEvent.keyDown(window, { key: DEFAULT_KEY_BINDINGS.deleteImage });
    await waitFor(() => expect(store.resolveModalNavigationTarget).toHaveBeenCalledWith(0, 'delete'));
    expect(store.setSelectedIndex).not.toHaveBeenCalled();
    expect(store.setModalImageIds).not.toHaveBeenCalled();
  });

  it('moves exactly once from a deleted middle image to its original right neighbor', async () => {
    store.searchResults = [fixtures.first, fixtures.second, fixtures.third];
    store.selectedIndex = 1;
    store.deleteImage.mockResolvedValue(true);
    store.resolveModalNavigationTarget.mockResolvedValue({
      status: 'found',
      index: 2,
      id: thirdImage.id,
      filteredOrderedIds: null,
    });
    const { rerender } = render(<ImageModal />);

    fireEvent.keyDown(window, { key: DEFAULT_KEY_BINDINGS.deleteImage });
    await waitFor(() => expect(store.setSelectedIndex).toHaveBeenCalledWith(2));
    expect(store.deleteImage).toHaveBeenCalledTimes(1);
    expect(store.deleteImage).toHaveBeenCalledWith(secondImage.id);
    expect(store.resolveModalNavigationTarget).toHaveBeenCalledTimes(1);
    expect(store.resolveModalNavigationTarget).toHaveBeenCalledWith(1, 'delete');
    expect(store.setSelectedIndex).toHaveBeenCalledTimes(1);

    // A late search refresh compacts [first, deleted, third] to [first, third].
    // It changes the surviving image's numeric slot, but must not trigger a
    // second navigation/selection commit that skips over it.
    store.searchResults = [fixtures.first, fixtures.third];
    store.selectedIndex = 1;
    rerender(<ImageModal />);

    expect(await screen.findByText('third.png')).toBeVisible();
    await Promise.resolve();
    expect(store.resolveModalNavigationTarget).toHaveBeenCalledTimes(1);
    expect(store.setSelectedIndex).toHaveBeenCalledTimes(1);
  });
});
