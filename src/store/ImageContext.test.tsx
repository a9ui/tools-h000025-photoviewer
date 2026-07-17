import React from 'react';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ImageProvider, normalizeStoredView, reorderPreviewTabIds, useImageStore } from './ImageContext';
import type { ImageFile } from '../lib/types';
import BottomPreviewTabs from '../components/BottomPreviewTabs';

function PreferencesProbe() {
  const {
    favoriteFilterLevels,
    toggleFavoriteFilterLevel,
    view,
    setView,
  } = useImageStore();

  return (
    <div>
      <output aria-label="favorite filters">{favoriteFilterLevels.join(',')}</output>
      <output aria-label="unseen markers">{view.showUnseenMarkers ? 'enabled' : 'disabled'}</output>
      <output aria-label="view settings">{JSON.stringify(view)}</output>
      <button type="button" onClick={() => toggleFavoriteFilterLevel(2)}>Toggle level 2</button>
      <button type="button" onClick={() => toggleFavoriteFilterLevel(5)}>Toggle level 5</button>
      <button type="button" onClick={() => setView({ showUnseenMarkers: !view.showUnseenMarkers })}>
        Toggle unseen markers
      </button>
      <button type="button" onClick={() => setView({ foldersExpanded: !view.foldersExpanded })}>
        Toggle folders
      </button>
    </div>
  );
}

function FavoritesProbe() {
  const { favorites, cycleFavoriteLevel, toggleFavorite, setFavoriteLevels, adjustFavoriteLevels } = useImageStore();
  return (
    <div>
      <output aria-label="favorites state">{JSON.stringify(favorites)}</output>
      <button type="button" onClick={() => cycleFavoriteLevel('clicked-before-hydration')}>
        Favorite before hydration
      </button>
      <button type="button" onClick={() => toggleFavorite('same-key')}>
        Toggle same key before hydration
      </button>
      <button type="button" onClick={() => setFavoriteLevels(['bulk-a', 'bulk-b', 'bulk-a'], 3)}>
        Set bulk favorites to level 3
      </button>
      <button type="button" onClick={() => adjustFavoriteLevels(['bulk-a', 'bulk-b'], -1)}>
        Decrease bulk favorites
      </button>
    </div>
  );
}

function SeenProbe() {
  const { seenImageIds, markImageSeen } = useImageStore();
  return (
    <div>
      <output aria-label="seen state">{JSON.stringify(seenImageIds)}</output>
      <button type="button" onClick={() => markImageSeen('browser-explicit.png')}>Mark seen</button>
    </div>
  );
}

const previewProbeImage: ImageFile = {
  id: 'C:/images/preview-probe.png',
  filename: 'preview-probe.png',
  absolutePath: 'C:/images/preview-probe.png',
  fileUrl: '/api/image?preview-probe',
  displayUrl: '/api/image?preview-probe&display=1',
  fullUrl: '/api/image?preview-probe&full=1',
  metadata: null,
  createdAt: 1,
  mtime: 1,
};

const secondPreviewProbeImage: ImageFile = {
  ...previewProbeImage,
  id: 'C:/images/preview-probe-second.png',
  filename: 'preview-probe-second.png',
  absolutePath: 'C:/images/preview-probe-second.png',
  fileUrl: '/api/image?preview-probe-second',
  displayUrl: '/api/image?preview-probe-second&display=1',
  fullUrl: '/api/image?preview-probe-second&full=1',
};

const thirdPreviewProbeImage: ImageFile = {
  ...previewProbeImage,
  id: 'C:/images/preview-probe-third.png',
  filename: 'preview-probe-third.png',
  absolutePath: 'C:/images/preview-probe-third.png',
  fileUrl: '/api/image?preview-probe-third',
  displayUrl: '/api/image?preview-probe-third&display=1',
  fullUrl: '/api/image?preview-probe-third&full=1',
};

function PreviewTabsProbe() {
  const {
    previewTabIds, activePreviewId, pinnedPreviewIds, closedPreviewTabCount,
    openPreviewTab, closePreviewTab, reorderPreviewTab, togglePinPreviewTab,
  } = useImageStore();
  return (
    <div>
      <output aria-label="preview tabs">{previewTabIds.join(',')}</output>
      <output aria-label="active preview tab">{activePreviewId ?? ''}</output>
      <output aria-label="pinned preview tabs">{pinnedPreviewIds.join(',')}</output>
      <output aria-label="closed preview tabs">{closedPreviewTabCount}</output>
      <button type="button" onClick={() => openPreviewTab(previewProbeImage)}>Open preview tab</button>
      <button type="button" onClick={() => openPreviewTab(secondPreviewProbeImage)}>Open second preview tab</button>
      <button type="button" onClick={() => closePreviewTab(previewProbeImage.id)}>Close preview tab</button>
      <button type="button" onClick={() => togglePinPreviewTab(secondPreviewProbeImage.id)}>Pin second preview tab</button>
      <button type="button" onClick={() => reorderPreviewTab(previewProbeImage.id, 1)}>Move first preview tab after second</button>
      <button type="button" onClick={() => reorderPreviewTab(previewProbeImage.id, 1)}>Repeat current preview order</button>
      <button type="button" onClick={() => reorderPreviewTab('C:/images/not-open.png', 0)}>Try invalid preview reorder</button>
    </div>
  );
}

function SearchProbe() {
  const {
    phase,
    dirPath,
    setPhase,
    setDirPath,
    setSearchQuery,
    searchResults,
    searchError,
    searchErrorKind,
    retrySearch,
    rescanExpiredSearchSession,
  } = useImageStore();
  return (
    <div>
      <output aria-label="search phase">{phase}</output>
      <output aria-label="search directory">{dirPath}</output>
      <output aria-label="search result ids">
        {searchResults.filter((image): image is ImageFile => Boolean(image)).map((image) => image.id).join(',')}
      </output>
      <output aria-label="search error state">{searchError ?? ''}</output>
      <output aria-label="search error kind">{searchErrorKind ?? ''}</output>
      <button type="button" onClick={() => {
        setDirPath('C:/images');
        setPhase('viewer');
      }}>Load initial search</button>
      <button type="button" onClick={() => setSearchQuery('next query')}>Run failing search</button>
      <button type="button" onClick={() => setSearchQuery('newer query')}>Run newer search</button>
      <button type="button" onClick={retrySearch}>Retry current search</button>
      <button type="button" onClick={rescanExpiredSearchSession}>Rescan expired session</button>
    </div>
  );
}

function FavoriteFilterNavigationProbe() {
  const {
    phase,
    setPhase,
    setDirPath,
    searchResults,
    favorites,
    setFavoriteLevels,
    cycleFavoriteLevel,
    toggleFavoriteFilterLevel,
    setShowFavOnly,
    setShowUnfavOnly,
    selectedIndex,
    setSelectedIndex,
    modalImageIds,
    setModalImageIds,
    selectedIds,
    selectImage,
    openModalAtImage,
    activePreviewId,
    openPreviewTab,
    previewTabIds,
  } = useImageStore();
  const loadedImages = searchResults.filter((image): image is ImageFile => Boolean(image));
  const loadedIds = loadedImages.map((image) => image.id);
  const modalCurrentId = selectedIndex !== null ? searchResults[selectedIndex]?.id ?? '' : '';
  const currentId = modalCurrentId || activePreviewId || '';

  const openMiddle = (modal: boolean) => {
    const image = loadedImages[1];
    if (!image) return;
    selectImage(image, loadedIds);
    if (modal) {
      openPreviewTab(image, { makeActive: true, pin: true });
      openModalAtImage(image.id, 1, loadedIds);
    }
    else {
      setSelectedIndex(null);
      setModalImageIds([]);
    }
  };

  return (
    <div>
      <output aria-label="favorite navigation phase">{phase}</output>
      <output aria-label="favorite navigation results">{loadedIds.join(',')}</output>
      <output aria-label="favorite navigation state">{JSON.stringify(favorites)}</output>
      <output aria-label="favorite navigation modal current">{modalCurrentId}</output>
      <output aria-label="favorite navigation modal order">{modalImageIds.join(',')}</output>
      <output aria-label="favorite navigation active preview">{activePreviewId ?? ''}</output>
      <output aria-label="favorite navigation preview tabs">{previewTabIds.join(',')}</output>
      <output aria-label="favorite navigation selection">{selectedIds.join(',')}</output>
      <button type="button" onClick={() => { setDirPath('C:/images'); setPhase('viewer'); }}>Load favorite navigation results</button>
      <button type="button" onClick={() => setFavoriteLevels(loadedIds, 2)}>Seed all at level 2</button>
      <button type="button" onClick={() => toggleFavoriteFilterLevel(2)}>Toggle exact level 2</button>
      <button type="button" onClick={() => toggleFavoriteFilterLevel(3)}>Toggle exact level 3</button>
      <button type="button" onClick={() => { setShowFavOnly(false); setShowUnfavOnly(false); }}>Disable favorite filters</button>
      <button type="button" onClick={() => setShowUnfavOnly(true)}>Enable unrated filter</button>
      <button type="button" onClick={() => openMiddle(true)}>Open middle in modal</button>
      <button type="button" onClick={() => openMiddle(false)}>Open middle in preview</button>
      <button type="button" onClick={() => { if (currentId) cycleFavoriteLevel(currentId); }}>Increase current favorite</button>
    </div>
  );
}

function createFavoriteNavigationFetch(
  images: ImageFile[],
  initialFavorites: Record<string, number> = {}
) {
  return vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url.includes('/api/search')) {
      return {
        ok: true,
        json: async () => ({ results: images, total: images.length, page: 0, totalPages: 1 }),
      } as Response;
    }
    if (url.includes('/api/favorites')) {
      if (init?.method === 'PUT') {
        const body = JSON.parse(String(init.body || '{}')) as { favorites?: Record<string, number> };
        return { ok: true, json: async () => ({ favorites: body.favorites ?? {} }) } as Response;
      }
      return { ok: true, json: async () => ({ favorites: initialFavorites }) } as Response;
    }
    if (url.includes('/api/seen')) {
      return { ok: true, json: async () => ({ seen: {} }) } as Response;
    }
    return {
      ok: true,
      json: async () => url.includes('/api/enhance/jobs') ? { jobs: [] } : {},
    } as Response;
  });
}

class MockEventSource {
  static instances: MockEventSource[] = [];
  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  close = vi.fn();

  constructor(readonly url: string) {
    MockEventSource.instances.push(this);
  }
}

function ScanProbe() {
  const { phase, dirPath, indexToken, searchResults, scanProgress, scanError, startScan, dismissScanError } = useImageStore();
  return (
    <div>
      <output aria-label="scan phase">{phase}</output>
      <output aria-label="scan directory">{dirPath}</output>
      <output aria-label="scan error">{scanError ?? ''}</output>
      <output aria-label="scan index token">{indexToken ?? ''}</output>
      <output aria-label="scan first image url">{searchResults[0]?.fileUrl ?? ''}</output>
      <output aria-label="scan progress">{scanProgress ? 'active' : 'none'}</output>
      <output aria-label="scan processed">{scanProgress?.processed ?? ''}</output>
      <button type="button" onClick={() => startScan({ dir: 'C:/images' })}>Start test scan</button>
      <button type="button" onClick={() => startScan({ dir: '' })}>Start empty scan</button>
      <button type="button" onClick={dismissScanError}>Dismiss scan error</button>
    </div>
  );
}

describe('normalizeStoredView', () => {
  it.each([
    {
      name: 'keeps valid sibling settings while clamping or defaulting only malformed fields',
      snapshot: {
        viewMode: 'list', thumbSize: 999, aspectMode: 'not-an-aspect', displayStyle: 'compact', columns: 12,
        sidebarOpen: false, rightPanelOpen: 'yes', rightPanelWidth: 120, sortBy: 'name',
        randomSeed: '  fixed-seed  ', folderSortBy: 'count-desc', modalEdgeRatio: 0.6, enhanceQueueOpen: false,
        dateFrom: '2024-02-29', dateTo: '2024-02-30',
        hiddenFolders: [' C:/Images/Hidden ', 'c:/images/hidden', 12, 'C:/Images/Other'],
        showUnseenMarkers: true, foldersExpanded: false,
      },
      expected: {
        viewMode: 'list', thumbSize: 600, aspectMode: 'original', displayStyle: 'compact', columns: 0,
        sidebarOpen: false, rightPanelOpen: true, rightPanelWidth: 240, sortBy: 'name',
        randomSeed: 'fixed-seed', folderSortBy: 'count-desc', modalEdgeRatio: 0.4, enhanceQueueOpen: false,
        dateFrom: '2024-02-29', dateTo: '', hiddenFolders: ['C:/Images/Hidden', 'C:/Images/Other'],
        showUnseenMarkers: true, foldersExpanded: false,
      },
    },
    {
      name: 'uses defaults for NaN, infinity, invalid enum, and non-boolean values',
      snapshot: {
        viewMode: 'masonry', thumbSize: Number.NaN, rightPanelWidth: Infinity, modalEdgeRatio: -Infinity,
        sidebarOpen: 1, rightPanelOpen: null, enhanceQueueOpen: 'false', showUnseenMarkers: 0, foldersExpanded: 'true',
      },
      expected: {
        viewMode: 'grid', thumbSize: 200, rightPanelWidth: 320, modalEdgeRatio: 0.28,
        sidebarOpen: true, rightPanelOpen: true, enhanceQueueOpen: true, showUnseenMarkers: false, foldersExpanded: true,
      },
    },
    {
      name: 'keeps legacy snapshots usable while permanently clearing obsolete columns',
      snapshot: { columns: 48, thumbSize: 160, sidebarOpen: false },
      expected: {
        columns: 0, thumbSize: 160, sidebarOpen: false, foldersExpanded: true, displayStyle: 'standard', showUnseenMarkers: false,
      },
    },
  ])('$name', ({ snapshot, expected }) => {
    expect(normalizeStoredView(snapshot)).toMatchObject(expected);
  });

  it('ignores inherited and accessor-backed values without losing valid own fields', () => {
    const inherited = Object.create({ viewMode: 'list', thumbSize: 560, sidebarOpen: false }) as Record<string, unknown>;
    Object.defineProperty(inherited, 'rightPanelWidth', { enumerable: true, get: () => 900 });
    inherited.displayStyle = 'poster';

    expect(normalizeStoredView(inherited)).toMatchObject({
      viewMode: 'grid', thumbSize: 200, sidebarOpen: true, rightPanelWidth: 320, displayStyle: 'poster',
    });
  });

  it('bounds long persisted text and folder lists without corrupting valid entries', () => {
    const folders = Array.from({ length: 505 }, (_, index) => `C:/Images/Hidden-${index}`);
    const normalized = normalizeStoredView({
      randomSeed: 'x'.repeat(257), hiddenFolders: ['x'.repeat(4097), ...folders],
      dateFrom: 'not-a-date', dateTo: '2025-01-01T00:00:00Z',
    });

    expect(normalized.randomSeed).toBe('default');
    expect(normalized.hiddenFolders).toHaveLength(500);
    expect(normalized.hiddenFolders[0]).toBe('C:/Images/Hidden-0');
    expect(normalized.hiddenFolders.at(-1)).toBe('C:/Images/Hidden-499');
    expect(normalized.dateFrom).toBe('');
    expect(normalized.dateTo).toBe('');
  });
});

describe('ImageProvider browser UI preferences', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      const payload = url.includes('/api/favorites')
        ? { favorites: {} }
        : url.includes('/api/enhance/jobs')
          ? { jobs: [] }
          : {};
      return {
        ok: true,
        json: async () => payload,
      } as Response;
    }));
  });

  it('hydrates and persists independent favorite levels', async () => {
    localStorage.setItem('pvu_fav_only', '1');
    localStorage.setItem('pvu_fav_levels', JSON.stringify([2, 4]));
    const user = userEvent.setup();

    render(
      <ImageProvider>
        <PreferencesProbe />
      </ImageProvider>
    );

    expect(await screen.findByRole('status', { name: 'favorite filters' })).toHaveTextContent('2,4');

    await user.click(screen.getByRole('button', { name: 'Toggle level 2' }));
    await user.click(screen.getByRole('button', { name: 'Toggle level 5' }));

    await waitFor(() => {
      expect(JSON.parse(localStorage.getItem('pvu_fav_levels') || '[]')).toEqual([4, 5]);
    });
  });

  it('defaults unseen markers off and persists an explicit setting change', async () => {
    const user = userEvent.setup();

    render(
      <ImageProvider>
        <PreferencesProbe />
      </ImageProvider>
    );

    expect(await screen.findByRole('status', { name: 'unseen markers' })).toHaveTextContent('disabled');
    await user.click(screen.getByRole('button', { name: 'Toggle unseen markers' }));

    await waitFor(() => {
      const storedView = JSON.parse(localStorage.getItem('pvu_view') || '{}');
      expect(storedView.showUnseenMarkers).toBe(true);
    });
  });

  it('clears an obsolete fixed-column value while preserving current view settings', async () => {
    localStorage.setItem('pvu_view', JSON.stringify({
      columns: 24,
      thumbSize: 200,
      sidebarOpen: false,
      showUnseenMarkers: true,
    }));

    render(
      <ImageProvider>
        <PreferencesProbe />
      </ImageProvider>
    );

    await waitFor(() => {
      const renderedView = JSON.parse(
        screen.getByRole('status', { name: 'view settings' }).textContent || '{}'
      );
      expect(renderedView).toMatchObject({
        columns: 0,
        thumbSize: 200,
        sidebarOpen: false,
        showUnseenMarkers: true,
        foldersExpanded: true,
      });
    });

    await waitFor(() => {
      expect(JSON.parse(localStorage.getItem('pvu_view') || '{}')).toMatchObject({
        columns: 0,
        thumbSize: 200,
        sidebarOpen: false,
        showUnseenMarkers: true,
        foldersExpanded: true,
      });
    });
  });

  it('defaults an older or malformed folders collapse preference to expanded and normalizes it', async () => {
    localStorage.setItem('pvu_view', JSON.stringify({
      thumbSize: 200,
      foldersExpanded: 'collapsed',
    }));

    render(
      <ImageProvider>
        <PreferencesProbe />
      </ImageProvider>
    );

    await waitFor(() => {
      const renderedView = JSON.parse(
        screen.getByRole('status', { name: 'view settings' }).textContent || '{}'
      );
      expect(renderedView.foldersExpanded).toBe(true);
    });
    await waitFor(() => {
      expect(JSON.parse(localStorage.getItem('pvu_view') || '{}').foldersExpanded).toBe(true);
    });
  });

  it('persists folders collapse into pvu_view for a subsequent reload', async () => {
    const user = userEvent.setup();

    const view = render(
      <ImageProvider>
        <PreferencesProbe />
      </ImageProvider>
    );

    await user.click(screen.getByRole('button', { name: 'Toggle folders' }));
    await waitFor(() => {
      expect(JSON.parse(localStorage.getItem('pvu_view') || '{}').foldersExpanded).toBe(false);
    });
    view.unmount();

    render(
      <ImageProvider>
        <PreferencesProbe />
      </ImageProvider>
    );

    await waitFor(() => {
      const renderedView = JSON.parse(
        screen.getByRole('status', { name: 'view settings' }).textContent || '{}'
      );
      expect(renderedView.foldersExpanded).toBe(false);
    });
  });

  it('falls back to default view settings for a non-object snapshot', async () => {
    localStorage.setItem('pvu_view', JSON.stringify(['obsolete']));

    render(
      <ImageProvider>
        <PreferencesProbe />
      </ImageProvider>
    );

    await waitFor(() => {
      const renderedView = JSON.parse(
        screen.getByRole('status', { name: 'view settings' }).textContent || '{}'
      );
      expect(renderedView).toMatchObject({
        columns: 0,
        thumbSize: 200,
        sidebarOpen: true,
        showUnseenMarkers: false,
      });
    });
  });

  it('preserves a favorite change made while the server snapshot is still loading', async () => {
    let resolveFavorites!: (response: Response) => void;
    const pendingFavorites = new Promise<Response>((resolve) => {
      resolveFavorites = resolve;
    });
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.includes('/api/favorites') && (!init?.method || init.method === 'GET')) {
        return pendingFavorites;
      }
      const payload = url.includes('/api/enhance/jobs') ? { jobs: [] } : {};
      return Promise.resolve({
        ok: true,
        json: async () => payload,
      } as Response);
    }));
    const user = userEvent.setup();

    render(
      <ImageProvider>
        <FavoritesProbe />
      </ImageProvider>
    );

    await user.click(screen.getByRole('button', { name: 'Favorite before hydration' }));
    expect(screen.getByRole('status', { name: 'favorites state' }))
      .toHaveTextContent('"clicked-before-hydration":1');

    resolveFavorites({
      ok: true,
      json: async () => ({ favorites: { 'server-favorite': 3 } }),
    } as Response);

    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'favorites state' }))
        .toHaveTextContent('"server-favorite":3');
    });
    expect(screen.getByRole('status', { name: 'favorites state' }))
      .toHaveTextContent('"clicked-before-hydration":1');
  });

  it('sets and adjusts multiple favorite levels in one state transaction', async () => {
    const user = userEvent.setup();
    render(<ImageProvider><FavoritesProbe /></ImageProvider>);

    await user.click(screen.getByRole('button', { name: 'Set bulk favorites to level 3' }));
    expect(screen.getByRole('status', { name: 'favorites state' })).toHaveTextContent('"bulk-a":3');
    expect(screen.getByRole('status', { name: 'favorites state' })).toHaveTextContent('"bulk-b":3');
    await user.click(screen.getByRole('button', { name: 'Decrease bulk favorites' }));
    expect(screen.getByRole('status', { name: 'favorites state' })).toHaveTextContent('"bulk-a":2');
    expect(screen.getByRole('status', { name: 'favorites state' })).toHaveTextContent('"bulk-b":2');
  });

  it('does not resurrect a favorite cleared while the server snapshot is loading', async () => {
    localStorage.setItem('pvu_favorites', JSON.stringify({ 'same-key': 1 }));
    let resolveFavorites!: (response: Response) => void;
    const pendingFavorites = new Promise<Response>((resolve) => {
      resolveFavorites = resolve;
    });
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.includes('/api/favorites') && (!init?.method || init.method === 'GET')) {
        return pendingFavorites;
      }
      const payload = url.includes('/api/enhance/jobs') ? { jobs: [] } : {};
      return Promise.resolve({
        ok: true,
        json: async () => payload,
      } as Response);
    }));
    const user = userEvent.setup();

    render(
      <ImageProvider>
        <FavoritesProbe />
      </ImageProvider>
    );

    expect(await screen.findByRole('status', { name: 'favorites state' }))
      .toHaveTextContent('"same-key":1');
    await user.click(screen.getByRole('button', { name: 'Toggle same key before hydration' }));
    expect(screen.getByRole('status', { name: 'favorites state' })).not.toHaveTextContent('same-key');

    resolveFavorites({
      ok: true,
      json: async () => ({ favorites: { 'same-key': 3 } }),
    } as Response);

    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'favorites state' })).toHaveTextContent('{}');
    });
  });

  it('unions shared seen state and preserves a newer local mark during response reconciliation', async () => {
    const seenPutBodies: unknown[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.includes('/api/seen') && (!init?.method || init.method === 'GET')) {
        return {
          ok: true,
          json: async () => ({ seen: { 'wpf-existing.png': true }, malformed: false }),
        } as Response;
      }
      if (url.includes('/api/seen') && init?.method === 'PUT') {
        seenPutBodies.push(JSON.parse(String(init.body)));
        return {
          ok: true,
          json: async () => ({
            seen: {
              'wpf-existing.png': true,
              'wpf-during-write.png': true,
              'browser-explicit.png': true,
            },
            malformed: false,
          }),
        } as Response;
      }
      return {
        ok: true,
        json: async () => (url.includes('/api/favorites') ? { favorites: {} } : { jobs: [] }),
      } as Response;
    }));
    const user = userEvent.setup();

    render(
      <ImageProvider>
        <SeenProbe />
      </ImageProvider>
    );

    expect(await screen.findByRole('status', { name: 'seen state' }))
      .toHaveTextContent('"wpf-existing.png":true');
    await user.click(screen.getByRole('button', { name: 'Mark seen' }));

    await waitFor(() => {
      expect(seenPutBodies).toEqual([{
        seen: { 'wpf-existing.png': true, 'browser-explicit.png': true },
      }]);
    });
    await waitFor(() => {
      const seen = screen.getByRole('status', { name: 'seen state' });
      expect(seen).toHaveTextContent('"browser-explicit.png":true');
    });
    expect(screen.getByRole('status', { name: 'seen state' }))
      .toHaveTextContent('"wpf-during-write.png":true');
  });

  it('restores a valid favorite backup when the primary key is missing', async () => {
    localStorage.setItem('pvu_favorites_backup', JSON.stringify({
      'backup-favorite': 4,
      'legacy-boolean': true,
      'future-string-level': 'future-value',
    }));

    render(
      <ImageProvider>
        <FavoritesProbe />
      </ImageProvider>
    );

    const favorites = await screen.findByRole('status', { name: 'favorites state' });
    expect(favorites).toHaveTextContent('"backup-favorite":4');
    expect(favorites).toHaveTextContent('"legacy-boolean":1');
    expect(favorites).toHaveTextContent('"future-string-level":1');

    await waitFor(() => {
      expect(JSON.parse(localStorage.getItem('pvu_favorites') || '{}')).toEqual({
        'backup-favorite': 4,
        'legacy-boolean': 1,
        'future-string-level': 1,
      });
    });
  });

  it('does not replace an intentionally empty primary with its backup', async () => {
    localStorage.setItem('pvu_favorites', '{}');
    localStorage.setItem('pvu_favorites_backup', JSON.stringify({ 'old-backup': 5 }));

    render(
      <ImageProvider>
        <FavoritesProbe />
      </ImageProvider>
    );

    expect(await screen.findByRole('status', { name: 'favorites state' })).toHaveTextContent('{}');
    expect(localStorage.getItem('pvu_favorites')).toBe('{}');
    expect(localStorage.getItem('pvu_favorites_backup')).toBe(JSON.stringify({ 'old-backup': 5 }));
  });

  it('keeps a malformed primary and backup read-only during automatic hydration', async () => {
    const malformedPrimary = '{not-json';
    const validBackup = JSON.stringify({ 'backup-must-not-replace-primary': 5 });
    localStorage.setItem('pvu_favorites', malformedPrimary);
    localStorage.setItem('pvu_favorites_backup', validBackup);

    render(
      <ImageProvider>
        <FavoritesProbe />
      </ImageProvider>
    );

    await new Promise((resolve) => setTimeout(resolve, 350));

    expect(localStorage.getItem('pvu_favorites')).toBe(malformedPrimary);
    expect(localStorage.getItem('pvu_favorites_backup')).toBe(validBackup);
  });

  it('keeps a malformed backup read-only when the primary key is missing', async () => {
    const malformedBackup = '{not-json';
    localStorage.setItem('pvu_favorites_backup', malformedBackup);

    render(
      <ImageProvider>
        <FavoritesProbe />
      </ImageProvider>
    );

    await new Promise((resolve) => setTimeout(resolve, 350));

    expect(localStorage.getItem('pvu_favorites')).toBeNull();
    expect(localStorage.getItem('pvu_favorites_backup')).toBe(malformedBackup);
  });

  it('persists favorites as a three-way change against the hydrated server base', async () => {
    const putBodies: unknown[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.includes('/api/favorites') && (!init?.method || init.method === 'GET')) {
        return {
          ok: true,
          json: async () => ({ favorites: { 'same-key': 3 } }),
        } as Response;
      }
      if (url.includes('/api/favorites') && init?.method === 'PUT') {
        putBodies.push(JSON.parse(String(init.body)));
        return {
          ok: true,
          json: async () => ({ favorites: { external: 4 } }),
        } as Response;
      }
      return {
        ok: true,
        json: async () => (url.includes('/api/enhance/jobs') ? { jobs: [] } : {}),
      } as Response;
    }));
    const user = userEvent.setup();

    render(
      <ImageProvider>
        <FavoritesProbe />
      </ImageProvider>
    );

    expect(await screen.findByRole('status', { name: 'favorites state' }))
      .toHaveTextContent('"same-key":3');
    await user.click(screen.getByRole('button', { name: 'Toggle same key before hydration' }));

    await waitFor(() => {
      expect(putBodies[0]).toEqual({
        favorites: {},
        baseFavorites: { 'same-key': 3 },
      });
    });
    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'favorites state' }))
        .toHaveTextContent('"external":4');
    });

    await waitFor(() => {
      expect(putBodies[1]).toEqual({
        favorites: { external: 4 },
        baseFavorites: { external: 4 },
      });
    });
  });
});

describe('ImageProvider favorite filter mutation navigation', () => {
  const images = [previewProbeImage, secondPreviewProbeImage, thirdPreviewProbeImage];

  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal('fetch', createFavoriteNavigationFetch(images));
  });

  async function loadNavigationProbe() {
    const user = userEvent.setup();
    render(<ImageProvider><FavoriteFilterNavigationProbe /></ImageProvider>);
    await user.click(screen.getByRole('button', { name: 'Load favorite navigation results' }));
    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'favorite navigation results' }))
        .toHaveTextContent(images.map((image) => image.id).join(','));
    });
    return user;
  }

  it('moves exact-filter modal state next then previous and closes when no match remains', async () => {
    const user = await loadNavigationProbe();
    await user.click(screen.getByRole('button', { name: 'Seed all at level 2' }));
    await user.click(screen.getByRole('button', { name: 'Toggle exact level 2' }));
    await user.click(screen.getByRole('button', { name: 'Open middle in modal' }));

    expect(screen.getByRole('status', { name: 'favorite navigation modal current' }))
      .toHaveTextContent(secondPreviewProbeImage.id);
    await user.click(screen.getByRole('button', { name: 'Increase current favorite' }));

    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'favorite navigation modal current' }))
        .toHaveTextContent(thirdPreviewProbeImage.id);
      expect(screen.getByRole('status', { name: 'favorite navigation active preview' }))
        .toHaveTextContent(thirdPreviewProbeImage.id);
      expect(screen.getByRole('status', { name: 'favorite navigation selection' }))
        .toHaveTextContent(thirdPreviewProbeImage.id);
      expect(screen.getByRole('status', { name: 'favorite navigation modal order' }))
        .toHaveTextContent(`${previewProbeImage.id},${thirdPreviewProbeImage.id}`);
      expect(screen.getByRole('status', { name: 'favorite navigation preview tabs' }))
        .toHaveTextContent(`${secondPreviewProbeImage.id},${thirdPreviewProbeImage.id}`);
    });
    await waitFor(() => {
      expect(JSON.parse(localStorage.getItem('pvu_preview_tabs') || '{}')).toEqual({
        version: 1,
        tabIds: [secondPreviewProbeImage.id, thirdPreviewProbeImage.id],
        activeId: thirdPreviewProbeImage.id,
      });
    });

    await user.click(screen.getByRole('button', { name: 'Increase current favorite' }));
    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'favorite navigation modal current' }))
        .toHaveTextContent(previewProbeImage.id);
      expect(screen.getByRole('status', { name: 'favorite navigation active preview' }))
        .toHaveTextContent(previewProbeImage.id);
    });

    await user.click(screen.getByRole('button', { name: 'Increase current favorite' }));
    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'favorite navigation modal current' })).toHaveTextContent('');
      expect(screen.getByRole('status', { name: 'favorite navigation modal order' })).toHaveTextContent('');
      expect(screen.getByRole('status', { name: 'favorite navigation active preview' })).toHaveTextContent('');
      expect(screen.getByRole('status', { name: 'favorite navigation selection' })).toHaveTextContent('');
    });
  });

  it('keeps the current image for matching mutations and while favorite filters are off', async () => {
    const user = await loadNavigationProbe();
    await user.click(screen.getByRole('button', { name: 'Seed all at level 2' }));
    await user.click(screen.getByRole('button', { name: 'Toggle exact level 2' }));
    await user.click(screen.getByRole('button', { name: 'Toggle exact level 3' }));
    await user.click(screen.getByRole('button', { name: 'Open middle in modal' }));
    await user.click(screen.getByRole('button', { name: 'Increase current favorite' }));

    expect(screen.getByRole('status', { name: 'favorite navigation modal current' }))
      .toHaveTextContent(secondPreviewProbeImage.id);
    expect(screen.getByRole('status', { name: 'favorite navigation preview tabs' }))
      .toHaveTextContent(secondPreviewProbeImage.id);

    await user.click(screen.getByRole('button', { name: 'Disable favorite filters' }));
    await user.click(screen.getByRole('button', { name: 'Increase current favorite' }));
    expect(screen.getByRole('status', { name: 'favorite navigation modal current' }))
      .toHaveTextContent(secondPreviewProbeImage.id);
    expect(screen.getByRole('status', { name: 'favorite navigation active preview' }))
      .toHaveTextContent(secondPreviewProbeImage.id);
  });

  it('moves an unrated right preview without opening a modal or inventing a tab', async () => {
    const user = await loadNavigationProbe();
    await user.click(screen.getByRole('button', { name: 'Enable unrated filter' }));
    await user.click(screen.getByRole('button', { name: 'Open middle in preview' }));
    await user.click(screen.getByRole('button', { name: 'Increase current favorite' }));

    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'favorite navigation modal current' })).toHaveTextContent('');
      expect(screen.getByRole('status', { name: 'favorite navigation active preview' }))
        .toHaveTextContent(thirdPreviewProbeImage.id);
      expect(screen.getByRole('status', { name: 'favorite navigation selection' }))
        .toHaveTextContent(thirdPreviewProbeImage.id);
      expect(screen.getByRole('status', { name: 'favorite navigation preview tabs' })).toHaveTextContent('');
    });
  });

  it('does not navigate when shared favorite hydration changes filter membership', async () => {
    localStorage.setItem('pvu_favorites', JSON.stringify({
      [previewProbeImage.id]: 2,
      [secondPreviewProbeImage.id]: 2,
      [thirdPreviewProbeImage.id]: 2,
    }));
    let resolveFavorites!: (response: Response) => void;
    const pendingFavorites = new Promise<Response>((resolve) => { resolveFavorites = resolve; });
    const fallbackFetch = createFavoriteNavigationFetch(images);
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input).includes('/api/favorites') && (!init?.method || init.method === 'GET')) {
        return pendingFavorites;
      }
      return fallbackFetch(input, init);
    }));

    const user = userEvent.setup();
    render(<ImageProvider><FavoriteFilterNavigationProbe /></ImageProvider>);
    await user.click(screen.getByRole('button', { name: 'Load favorite navigation results' }));
    await screen.findByText(images.map((image) => image.id).join(','));
    await user.click(screen.getByRole('button', { name: 'Toggle exact level 2' }));
    await user.click(screen.getByRole('button', { name: 'Open middle in modal' }));

    resolveFavorites({
      ok: true,
      json: async () => ({ favorites: {
        [previewProbeImage.id]: 2,
        [secondPreviewProbeImage.id]: 3,
        [thirdPreviewProbeImage.id]: 2,
      } }),
    } as Response);

    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'favorite navigation state' }))
        .toHaveTextContent(`"${secondPreviewProbeImage.id}":3`);
    });
    expect(screen.getByRole('status', { name: 'favorite navigation modal current' }))
      .toHaveTextContent(secondPreviewProbeImage.id);
    expect(screen.getByRole('status', { name: 'favorite navigation active preview' }))
      .toHaveTextContent(secondPreviewProbeImage.id);
    expect(screen.getByRole('status', { name: 'favorite navigation selection' }))
      .toHaveTextContent(secondPreviewProbeImage.id);
    expect(screen.getByRole('status', { name: 'favorite navigation preview tabs' }))
      .toHaveTextContent(secondPreviewProbeImage.id);
  });
});

describe('ImageProvider preview tab persistence', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: true,
      json: async () => ({ favorites: {}, jobs: [] }),
    }) as Response));
  });

  it('persists the open tab order and active tab after a user opens or closes a tab', async () => {
    const user = userEvent.setup();
    render(
      <ImageProvider>
        <PreviewTabsProbe />
      </ImageProvider>
    );

    await user.click(screen.getByRole('button', { name: 'Open preview tab' }));
    await waitFor(() => {
      expect(JSON.parse(localStorage.getItem('pvu_preview_tabs') || '{}')).toEqual({
        version: 1,
        tabIds: [previewProbeImage.id],
        activeId: previewProbeImage.id,
      });
    });

    await user.click(screen.getByRole('button', { name: 'Close preview tab' }));
    await waitFor(() => {
      expect(JSON.parse(localStorage.getItem('pvu_preview_tabs') || '{}')).toEqual({
        version: 1,
        tabIds: [],
        activeId: null,
      });
    });
  });

  it('reorders tabs without changing active or pinned state, and persists the resulting order', async () => {
    const user = userEvent.setup();
    render(
      <ImageProvider>
        <PreviewTabsProbe />
      </ImageProvider>
    );

    await user.click(screen.getByRole('button', { name: 'Open preview tab' }));
    await user.click(screen.getByRole('button', { name: 'Open second preview tab' }));
    await user.click(screen.getByRole('button', { name: 'Pin second preview tab' }));
    await user.click(screen.getByRole('button', { name: 'Move first preview tab after second' }));

    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'preview tabs' }))
        .toHaveTextContent(`${secondPreviewProbeImage.id},${previewProbeImage.id}`);
      expect(screen.getByRole('status', { name: 'active preview tab' })).toHaveTextContent(secondPreviewProbeImage.id);
      expect(screen.getByRole('status', { name: 'pinned preview tabs' })).toHaveTextContent(secondPreviewProbeImage.id);
      expect(JSON.parse(localStorage.getItem('pvu_preview_tabs') || '{}')).toEqual({
        version: 1,
        tabIds: [secondPreviewProbeImage.id, previewProbeImage.id],
        activeId: secondPreviewProbeImage.id,
      });
    });

    await user.click(screen.getByRole('button', { name: 'Repeat current preview order' }));
    await user.click(screen.getByRole('button', { name: 'Try invalid preview reorder' }));
    expect(screen.getByRole('status', { name: 'preview tabs' }))
      .toHaveTextContent(`${secondPreviewProbeImage.id},${previewProbeImage.id}`);
  });

  it('restores a persisted reordered preview-tab snapshot in the same order after a scan', async () => {
    localStorage.setItem('pvu_preview_tabs', JSON.stringify({
      version: 1,
      tabIds: [secondPreviewProbeImage.id, previewProbeImage.id],
      activeId: secondPreviewProbeImage.id,
    }));
    MockEventSource.instances = [];
    vi.stubGlobal('EventSource', MockEventSource);
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      if (String(input).includes('/api/search')) {
        return {
          ok: true,
          json: async () => ({ results: [previewProbeImage, secondPreviewProbeImage], total: 2, page: 0, totalPages: 1 }),
        } as Response;
      }
      return { ok: true, json: async () => ({ favorites: {}, jobs: [] }) } as Response;
    }));
    render(
      <ImageProvider>
        <ScanProbe />
        <PreviewTabsProbe />
      </ImageProvider>
    );

    fireEvent.click(screen.getByRole('button', { name: 'Start test scan' }));
    act(() => {
      MockEventSource.instances[0].onmessage?.({
        data: JSON.stringify({ type: 'complete', processed: 2, total: 2, newFiles: 2, stage: 'complete' }),
      } as MessageEvent);
    });

    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'preview tabs' }))
        .toHaveTextContent(`${secondPreviewProbeImage.id},${previewProbeImage.id}`);
      expect(screen.getByRole('status', { name: 'active preview tab' })).toHaveTextContent(secondPreviewProbeImage.id);
    });
  });

  it('keeps a focused Restore control after closing the final preview tab and restores it', async () => {
    const user = userEvent.setup();
    render(
      <ImageProvider>
        <PreviewTabsProbe />
        <BottomPreviewTabs />
      </ImageProvider>
    );

    expect(screen.queryByRole('region', { name: 'Recently closed preview tabs' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Open preview tab' }));
    await user.click(screen.getByRole('button', { name: 'Close preview tab' }));

    const restore = await screen.findByRole('button', { name: /restore last closed preview tab/i });
    expect(screen.getByRole('status', { name: 'preview tabs' })).toHaveTextContent('');
    expect(screen.getByRole('status', { name: 'closed preview tabs' })).toHaveTextContent('1');
    expect(restore).toHaveFocus();

    await user.click(restore);
    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'preview tabs' })).toHaveTextContent(previewProbeImage.id);
      expect(screen.getByRole('status', { name: 'closed preview tabs' })).toHaveTextContent('0');
      expect(screen.queryByRole('button', { name: /restore last closed preview tab/i })).not.toBeInTheDocument();
    });
    expect(screen.getByRole('tab', { name: /open preview preview-probe\.png/i })).toHaveFocus();
  });

  it('restores only tabs found in the current scan and safely falls back from a missing active tab', async () => {
    localStorage.setItem('pvu_preview_tabs', JSON.stringify({
      version: 1,
      tabIds: [previewProbeImage.id, 'C:/images/no-longer-indexed.png'],
      activeId: 'C:/images/no-longer-indexed.png',
    }));
    MockEventSource.instances = [];
    vi.stubGlobal('EventSource', MockEventSource);
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      if (String(input).includes('/api/search')) {
        return {
          ok: true,
          json: async () => ({ results: [previewProbeImage], total: 1, page: 0, totalPages: 1 }),
        } as Response;
      }
      return {
        ok: true,
        json: async () => ({ favorites: {}, jobs: [] }),
      } as Response;
    }));

    render(
      <ImageProvider>
        <ScanProbe />
        <PreviewTabsProbe />
      </ImageProvider>
    );

    fireEvent.click(screen.getByRole('button', { name: 'Start test scan' }));
    act(() => {
      MockEventSource.instances[0].onmessage?.({
        data: JSON.stringify({ type: 'complete', processed: 1, total: 1, newFiles: 1, stage: 'complete' }),
      } as MessageEvent);
    });

    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'preview tabs' }))
        .toHaveTextContent(previewProbeImage.id);
      expect(screen.getByRole('status', { name: 'active preview tab' }))
        .toHaveTextContent(previewProbeImage.id);
    });
    await waitFor(() => {
      expect(JSON.parse(localStorage.getItem('pvu_preview_tabs') || '{}')).toEqual({
        version: 1,
        tabIds: [previewProbeImage.id],
        activeId: previewProbeImage.id,
      });
    });
  });
});

describe('reorderPreviewTabIds', () => {
  it('preserves the original list for invalid, duplicate, and no-op reorder requests', () => {
    const valid = [previewProbeImage.id, secondPreviewProbeImage.id];
    const duplicate = [previewProbeImage.id, previewProbeImage.id];

    expect(reorderPreviewTabIds(valid, previewProbeImage.id, 0)).toBe(valid);
    expect(reorderPreviewTabIds(valid, 'C:/images/not-open.png', 0)).toBe(valid);
    expect(reorderPreviewTabIds(valid, previewProbeImage.id, -1)).toBe(valid);
    expect(reorderPreviewTabIds(duplicate, previewProbeImage.id, 1)).toBe(duplicate);
  });
});

describe('ImageProvider search recovery', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('keeps the last successful result set visible after a current search fails and retries it', async () => {
    const nextImage: ImageFile = {
      ...previewProbeImage,
      id: 'C:/images/retried.png',
      filename: 'retried.png',
    };
    let searchCalls = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      if (String(input).includes('/api/search')) {
        searchCalls += 1;
        if (searchCalls === 1) {
          return {
            ok: true,
            json: async () => ({ results: [previewProbeImage], total: 1, page: 0, totalPages: 1 }),
          } as Response;
        }
        if (searchCalls === 2) {
          return new Response(JSON.stringify({ error: 'Search service temporarily unavailable' }), { status: 500 });
        }
        return {
          ok: true,
          json: async () => ({ results: [nextImage], total: 1, page: 0, totalPages: 1 }),
        } as Response;
      }
      return {
        ok: true,
        json: async () => ({ favorites: {}, jobs: [] }),
      } as Response;
    }));

    render(
      <ImageProvider>
        <SearchProbe />
      </ImageProvider>
    );

    fireEvent.click(screen.getByRole('button', { name: 'Load initial search' }));
    await screen.findByText(previewProbeImage.id);

    fireEvent.click(screen.getByRole('button', { name: 'Run failing search' }));
    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'search error state' }))
        .toHaveTextContent('Search service temporarily unavailable');
      expect(screen.getByRole('status', { name: 'search error kind' })).toHaveTextContent('transient');
    });
    expect(screen.getByRole('status', { name: 'search result ids' }))
      .toHaveTextContent(previewProbeImage.id);

    fireEvent.click(screen.getByRole('button', { name: 'Retry current search' }));
    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'search result ids' })).toHaveTextContent(nextImage.id);
      expect(screen.getByRole('status', { name: 'search error state' })).toHaveTextContent('');
    });
  });

  it('classifies a 410 as an expired session, retains results, and rescans the same folder set', async () => {
    MockEventSource.instances = [];
    vi.stubGlobal('EventSource', MockEventSource);
    let searchCalls = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.includes('/api/search')) {
        searchCalls += 1;
        if (searchCalls === 1) {
          return { ok: true, json: async () => ({ results: [previewProbeImage], total: 1, page: 0, totalPages: 1 }) } as Response;
        }
        if (searchCalls === 2) {
          return new Response(JSON.stringify({ error: 'This viewer session expired. Scan the folder set again to refresh it.' }), { status: 410 });
        }
        return { ok: true, json: async () => ({ results: [secondPreviewProbeImage], total: 1, page: 0, totalPages: 1 }) } as Response;
      }
      return { ok: true, json: async () => ({ favorites: {}, jobs: [] }) } as Response;
    }));

    render(
      <ImageProvider>
        <SearchProbe />
      </ImageProvider>
    );
    fireEvent.click(screen.getByRole('button', { name: 'Load initial search' }));
    await screen.findByText(previewProbeImage.id);
    fireEvent.click(screen.getByRole('button', { name: 'Run failing search' }));

    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'search error kind' })).toHaveTextContent('session-expired');
      expect(screen.getByRole('status', { name: 'search result ids' })).toHaveTextContent(previewProbeImage.id);
      expect(screen.getByRole('status', { name: 'search directory' })).toHaveTextContent('C:/images');
    });

    fireEvent.click(screen.getByRole('button', { name: 'Rescan expired session' }));
    expect(screen.getByRole('status', { name: 'search phase' })).toHaveTextContent('scanning');
    expect(MockEventSource.instances[0].url).toContain('dir=C%3A%2Fimages');
    act(() => {
      MockEventSource.instances[0].onmessage?.({
        data: JSON.stringify({ type: 'complete', processed: 1, total: 1, newFiles: 0, stage: 'complete', indexToken: 'idx_refreshed' }),
      } as MessageEvent);
    });

    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'search phase' })).toHaveTextContent('viewer');
      expect(screen.getByRole('status', { name: 'search error state' })).toHaveTextContent('');
      expect(screen.getByRole('status', { name: 'search result ids' })).toHaveTextContent(secondPreviewProbeImage.id);
    });
  });

  it('discards a stale 410 when a newer search generation succeeds', async () => {
    let resolveExpired!: (response: Response) => void;
    let searchCalls = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      if (!String(input).includes('/api/search')) {
        return { ok: true, json: async () => ({ favorites: {}, jobs: [] }) } as Response;
      }
      searchCalls += 1;
      if (searchCalls === 1) {
        return { ok: true, json: async () => ({ results: [previewProbeImage], total: 1, page: 0, totalPages: 1 }) } as Response;
      }
      if (searchCalls === 2) {
        return new Promise<Response>((resolve) => { resolveExpired = resolve; });
      }
      return { ok: true, json: async () => ({ results: [secondPreviewProbeImage], total: 1, page: 0, totalPages: 1 }) } as Response;
    }));
    render(
      <ImageProvider>
        <SearchProbe />
      </ImageProvider>
    );
    fireEvent.click(screen.getByRole('button', { name: 'Load initial search' }));
    await screen.findByText(previewProbeImage.id);
    fireEvent.click(screen.getByRole('button', { name: 'Run failing search' }));
    await waitFor(() => expect(searchCalls).toBe(2));
    fireEvent.click(screen.getByRole('button', { name: 'Run newer search' }));
    await waitFor(() => expect(screen.getByRole('status', { name: 'search result ids' })).toHaveTextContent(secondPreviewProbeImage.id));

    await act(async () => {
      resolveExpired(new Response(JSON.stringify({ error: 'This viewer session expired.' }), { status: 410 }));
    });
    expect(screen.getByRole('status', { name: 'search error kind' })).toHaveTextContent('');
    expect(screen.getByRole('status', { name: 'search result ids' })).toHaveTextContent(secondPreviewProbeImage.id);
  });
});

describe('ImageProvider scan failures', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    MockEventSource.instances = [];
    vi.stubGlobal('EventSource', MockEventSource);
    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: true,
      json: async () => ({ favorites: {}, jobs: [] }),
    }) as Response));
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.spyOn(window, 'alert').mockImplementation(() => {});
  });

  it('stores a server scan failure without calling window.alert', async () => {
    render(
      <ImageProvider>
        <ScanProbe />
      </ImageProvider>
    );

    fireEvent.click(screen.getByRole('button', { name: 'Start test scan' }));
    const source = MockEventSource.instances[0];
    fireEvent.click(screen.getByRole('button', { name: 'Start test scan' }));
    expect(MockEventSource.instances).toHaveLength(1);
    act(() => {
      source.onmessage?.({ data: JSON.stringify({ type: 'error', message: 'Folder is unavailable.' }) } as MessageEvent);
    });

    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'scan phase' })).toHaveTextContent('landing');
      expect(screen.getByRole('status', { name: 'scan error' })).toHaveTextContent('Folder is unavailable.');
    });
    expect(screen.getByRole('status', { name: 'scan progress' })).toHaveTextContent('none');
    expect(window.alert).not.toHaveBeenCalled();
    expect(source.close).toHaveBeenCalledTimes(1);
  });

  it('uses a completed scan token for subsequent search and image requests', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.includes('/api/search')) {
        return {
          ok: true,
          json: async () => ({ results: [previewProbeImage], total: 1, page: 0, totalPages: 1 }),
        } as Response;
      }
      return {
        ok: true,
        json: async () => url.includes('/api/enhance/jobs') ? { jobs: [] } : { favorites: {} },
      } as Response;
    });
    vi.stubGlobal('fetch', fetchMock);
    render(
      <ImageProvider>
        <ScanProbe />
      </ImageProvider>
    );

    fireEvent.click(screen.getByRole('button', { name: 'Start test scan' }));
    act(() => {
      MockEventSource.instances[0].onmessage?.({
        data: JSON.stringify({
          type: 'complete', processed: 1, total: 1, newFiles: 1, stage: 'complete', indexToken: 'idx_session_a',
        }),
      } as MessageEvent);
    });

    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'scan index token' })).toHaveTextContent('idx_session_a');
      expect(screen.getByRole('status', { name: 'scan first image url' }))
        .toHaveTextContent('indexToken=idx_session_a');
      expect(fetchMock.mock.calls.some(([input]) => String(input).includes('/api/search')
        && String(input).includes('indexToken=idx_session_a'))).toBe(true);
    });
  });

  it('shows an SSE scan conflict reason instead of a generic connection-loss message', async () => {
    render(
      <ImageProvider>
        <ScanProbe />
      </ImageProvider>
    );

    fireEvent.click(screen.getByRole('button', { name: 'Start test scan' }));
    const source = MockEventSource.instances[0];
    act(() => {
      source.onmessage?.({ data: JSON.stringify({
        type: 'error',
        message: 'A scan for this folder set is already running. Please retry when it completes.',
      }) } as MessageEvent);
      source.onerror?.(new Event('error'));
    });

    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'scan error' }))
        .toHaveTextContent('A scan for this folder set is already running. Please retry when it completes.');
    });
    expect(screen.getByRole('status', { name: 'scan error' }))
      .not.toHaveTextContent('Connection lost before the scan completed.');
  });

  it('stores connection loss, clears it for a retry, and remains clear after completion', async () => {
    render(
      <ImageProvider>
        <ScanProbe />
      </ImageProvider>
    );

    fireEvent.click(screen.getByRole('button', { name: 'Start test scan' }));
    const failedSource = MockEventSource.instances[0];
    act(() => {
      failedSource.onerror?.(new Event('error'));
    });
    await screen.findByText('Connection lost before the scan completed.');

    fireEvent.click(screen.getByRole('button', { name: 'Start test scan' }));
    const retrySource = MockEventSource.instances[1];
    expect(screen.getByRole('status', { name: 'scan phase' })).toHaveTextContent('scanning');
    expect(screen.getByRole('status', { name: 'scan error' })).toHaveTextContent('');

    act(() => {
      retrySource.onmessage?.({
        data: JSON.stringify({ type: 'complete', processed: 2, total: 2, newFiles: 1, stage: 'complete', message: 'Scan complete.' }),
      } as MessageEvent);
    });
    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'scan phase' })).toHaveTextContent('viewer');
      expect(screen.getByRole('status', { name: 'scan error' })).toHaveTextContent('');
    });
    expect(window.alert).not.toHaveBeenCalled();
  });

  it('dismisses a stored scan failure without changing the current folder set', async () => {
    const user = userEvent.setup();
    render(
      <ImageProvider>
        <ScanProbe />
      </ImageProvider>
    );

    fireEvent.click(screen.getByRole('button', { name: 'Start test scan' }));
    const source = MockEventSource.instances[0];
    act(() => {
      source.onmessage?.({ data: JSON.stringify({ type: 'error', message: 'Server refused the scan.' }) } as MessageEvent);
    });
    await screen.findByText('Server refused the scan.');

    await user.click(screen.getByRole('button', { name: 'Dismiss scan error' }));
    expect(screen.getByRole('status', { name: 'scan error' })).toHaveTextContent('');
    expect(screen.getByRole('status', { name: 'scan directory' })).toHaveTextContent('C:/images');
  });

  it.each([
    ['malformed data', '{not valid json', 'The scan stream returned malformed data.'],
    ['unknown event', JSON.stringify({ type: 'heartbeat' }), 'The scan stream returned an unknown event.'],
  ])('stores %s in the recoverable inline error state', async (_name, eventData, expectedError) => {
    render(
      <ImageProvider>
        <ScanProbe />
      </ImageProvider>
    );

    fireEvent.click(screen.getByRole('button', { name: 'Start test scan' }));
    const source = MockEventSource.instances[0];
    act(() => {
      source.onmessage?.({ data: eventData } as MessageEvent);
    });

    await waitFor(() => {
      expect(screen.getByRole('status', { name: 'scan phase' })).toHaveTextContent('landing');
      expect(screen.getByRole('status', { name: 'scan error' })).toHaveTextContent(expectedError);
    });
    expect(screen.getByRole('status', { name: 'scan progress' })).toHaveTextContent('none');
    expect(window.alert).not.toHaveBeenCalled();
  });

  it('ignores stale events from a failed stream after retrying', async () => {
    render(
      <ImageProvider>
        <ScanProbe />
      </ImageProvider>
    );

    fireEvent.click(screen.getByRole('button', { name: 'Start test scan' }));
    const staleSource = MockEventSource.instances[0];
    act(() => {
      staleSource.onerror?.(new Event('error'));
    });
    await screen.findByText('Connection lost before the scan completed.');

    fireEvent.click(screen.getByRole('button', { name: 'Start test scan' }));
    const activeSource = MockEventSource.instances[1];
    act(() => {
      activeSource.onmessage?.({
        data: JSON.stringify({ type: 'progress', processed: 1, total: 4, newFiles: 0, stage: 'scanning' }),
      } as MessageEvent);
      staleSource.onmessage?.({
        data: JSON.stringify({ type: 'complete', processed: 999, total: 999, newFiles: 999, stage: 'complete' }),
      } as MessageEvent);
    });

    expect(screen.getByRole('status', { name: 'scan phase' })).toHaveTextContent('scanning');
    expect(screen.getByRole('status', { name: 'scan processed' })).toHaveTextContent('1');
    expect(screen.getByRole('status', { name: 'scan error' })).toHaveTextContent('');
  });

  it('does not start a scan for an empty directory', () => {
    render(
      <ImageProvider>
        <ScanProbe />
      </ImageProvider>
    );

    fireEvent.click(screen.getByRole('button', { name: 'Start empty scan' }));
    expect(MockEventSource.instances).toHaveLength(0);
    expect(screen.getByRole('status', { name: 'scan phase' })).toHaveTextContent('landing');
  });
});
