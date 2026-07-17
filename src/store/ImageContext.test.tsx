import React from 'react';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ImageProvider, useImageStore } from './ImageContext';
import type { ImageFile } from '../lib/types';

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
  const { favorites, cycleFavoriteLevel, toggleFavorite } = useImageStore();
  return (
    <div>
      <output aria-label="favorites state">{JSON.stringify(favorites)}</output>
      <button type="button" onClick={() => cycleFavoriteLevel('clicked-before-hydration')}>
        Favorite before hydration
      </button>
      <button type="button" onClick={() => toggleFavorite('same-key')}>
        Toggle same key before hydration
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

function PreviewTabsProbe() {
  const { previewTabIds, activePreviewId, openPreviewTab, closePreviewTab } = useImageStore();
  return (
    <div>
      <output aria-label="preview tabs">{previewTabIds.join(',')}</output>
      <output aria-label="active preview tab">{activePreviewId ?? ''}</output>
      <button type="button" onClick={() => openPreviewTab(previewProbeImage)}>Open preview tab</button>
      <button type="button" onClick={() => closePreviewTab(previewProbeImage.id)}>Close preview tab</button>
    </div>
  );
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
  const { phase, dirPath, scanProgress, scanError, startScan, dismissScanError } = useImageStore();
  return (
    <div>
      <output aria-label="scan phase">{phase}</output>
      <output aria-label="scan directory">{dirPath}</output>
      <output aria-label="scan error">{scanError ?? ''}</output>
      <output aria-label="scan progress">{scanProgress ? 'active' : 'none'}</output>
      <output aria-label="scan processed">{scanProgress?.processed ?? ''}</output>
      <button type="button" onClick={() => startScan({ dir: 'C:/images' })}>Start test scan</button>
      <button type="button" onClick={() => startScan({ dir: '' })}>Start empty scan</button>
      <button type="button" onClick={dismissScanError}>Dismiss scan error</button>
    </div>
  );
}

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
