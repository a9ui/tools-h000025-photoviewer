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
      <button type="button" onClick={() => toggleFavoriteFilterLevel(2)}>Toggle level 2</button>
      <button type="button" onClick={() => toggleFavoriteFilterLevel(5)}>Toggle level 5</button>
      <button type="button" onClick={() => setView({ showUnseenMarkers: !view.showUnseenMarkers })}>
        Toggle unseen markers
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
});
