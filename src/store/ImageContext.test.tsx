import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ImageProvider, useImageStore } from './ImageContext';

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
      });
    });

    await waitFor(() => {
      expect(JSON.parse(localStorage.getItem('pvu_view') || '{}')).toMatchObject({
        columns: 0,
        thumbSize: 200,
        sidebarOpen: false,
        showUnseenMarkers: true,
      });
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
