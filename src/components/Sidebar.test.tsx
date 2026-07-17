import React from 'react';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import Sidebar from './Sidebar';
import { useImageStore } from '../store/ImageContext';

vi.mock('../store/ImageContext', () => ({
  useImageStore: vi.fn(),
}));

const toggleFavoriteFilterLevel = vi.fn();
const clearFavoriteFilterLevels = vi.fn();
const setView = vi.fn();

function createStore(options: {
  dirPath?: string;
  hiddenFolders?: string[];
  searchTotal?: number;
  totalIndexed?: number;
  searchResults?: Array<{ id: string } | null>;
  showFavOnly?: boolean;
  favorites?: Record<string, number>;
} = {}) {
  return {
    view: {
      viewMode: 'grid',
      thumbSize: 200,
      aspectMode: 'original',
      displayStyle: 'standard',
      columns: 0,
      sidebarOpen: true,
      rightPanelOpen: true,
      rightPanelWidth: 320,
      sortBy: 'newest',
      randomSeed: 'default',
      folderSortBy: 'name-asc',
      modalEdgeRatio: 0.28,
      enhanceQueueOpen: true,
      dateFrom: '',
      dateTo: '',
      hiddenFolders: options.hiddenFolders ?? [],
      showUnseenMarkers: false,
    },
    setView,
    setSearchQuery: vi.fn(),
    dirPath: options.dirPath ?? '',
    setDirPath: vi.fn(),
    startScan: vi.fn(),
    totalIndexed: options.totalIndexed ?? 0,
    searchTotal: options.searchTotal ?? 0,
    searchResults: options.searchResults ?? [],
    searchQuery: '',
    favorites: options.favorites ?? {},
    showFavOnly: options.showFavOnly ?? true,
    setShowFavOnly: vi.fn(),
    showUnfavOnly: false,
    setShowUnfavOnly: vi.fn(),
    favoriteFilterLevels: [2, 4],
    toggleFavoriteFilterLevel,
    clearFavoriteFilterLevels,
    showEnhancedOnly: false,
    setShowEnhancedOnly: vi.fn(),
    enhancedSourceIds: {},
    setShowSettings: vi.fn(),
    setPhase: vi.fn(),
    perfEnabled: false,
    setPerfEnabled: vi.fn(),
    perfStats: { searchCount: 0, lastSearchMs: 0, avgSearchMs: 0 },
  } as unknown as ReturnType<typeof useImageStore>;
}

describe('Sidebar favorite level controls', () => {
  beforeEach(() => {
    setView.mockReset();
    toggleFavoriteFilterLevel.mockReset();
    clearFavoriteFilterLevels.mockReset();
    vi.mocked(useImageStore).mockReturnValue(createStore());
  });

  it('renders independent levels and All semantics', async () => {
    const user = userEvent.setup();
    render(<Sidebar />);

    expect(screen.getByRole('checkbox', { name: 'Favorite level 1' })).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Favorite level 2' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Favorite level 4' })).toBeChecked();

    await user.click(screen.getByRole('checkbox', { name: 'Favorite level 1' }));
    expect(toggleFavoriteFilterLevel).toHaveBeenCalledWith(1);

    await user.click(screen.getByRole('button', { name: 'All' }));
    expect(clearFavoriteFilterLevels).toHaveBeenCalledTimes(1);
  });

  it('keeps custom date inputs while omitting quick-search and date presets', () => {
    render(<Sidebar />);

    expect(screen.queryByText('Quick Search')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Today' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '7d' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '30d' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'This year' })).not.toBeInTheDocument();
    expect(screen.getByLabelText('Date from')).toBeInTheDocument();
    expect(screen.getByLabelText('Date to')).toBeInTheDocument();
  });

  it('labels sparse client-filtered matches separately from indexed totals without a live region', () => {
    vi.mocked(useImageStore).mockReturnValue(createStore({
      searchTotal: 900,
      totalIndexed: 900,
      searchResults: [{ id: 'favorite' }, null, { id: 'unrated' }],
      favorites: { favorite: 2 },
    }));

    render(<Sidebar />);

    const count = screen.getByText('1 shown · 900 indexed');
    expect(count).toHaveAttribute('aria-label', 'Results: 1 shown · 900 indexed');
    expect(count).not.toHaveAttribute('aria-live');
  });

  it('collapses and restores the Folders section from its heading', async () => {
    const user = userEvent.setup();
    render(<Sidebar />);

    const foldersToggle = screen.getByRole('button', { name: 'Folders' });
    const emptyFoldersMessage = screen.getByText('No folders found under this root.');
    expect(foldersToggle).toHaveAttribute('aria-expanded', 'true');
    expect(emptyFoldersMessage).toBeVisible();

    await user.click(foldersToggle);
    expect(foldersToggle).toHaveAttribute('aria-expanded', 'false');
    expect(emptyFoldersMessage).not.toBeVisible();

    await user.click(foldersToggle);
    expect(foldersToggle).toHaveAttribute('aria-expanded', 'true');
    expect(emptyFoldersMessage).toBeVisible();
  });
});

describe('Sidebar folder controls', () => {
  beforeEach(() => {
    setView.mockReset();
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve({
      json: () => Promise.resolve({
        folders: [
          { key: 'alpha', label: 'Alpha', count: 12 },
          { key: 'beta', label: 'Beta', count: 3 },
        ],
      }),
    })));
    vi.mocked(useImageStore).mockReturnValue(createStore({ dirPath: 'C:/images' }));
  });

  it('exposes folder selection and visibility as separate keyboard controls', async () => {
    const user = userEvent.setup();
    render(<Sidebar />);

    const selectAlpha = await screen.findByRole('button', { name: 'Select folder Alpha, 12 images' });
    const visibilityAlpha = screen.getByRole('button', { name: 'Hide folder Alpha' });

    expect(selectAlpha).toHaveAttribute('aria-pressed', 'false');
    expect(visibilityAlpha).toHaveAttribute('aria-pressed', 'true');
    expect(within(selectAlpha).queryByRole('checkbox')).not.toBeInTheDocument();

    selectAlpha.focus();
    await user.keyboard('{Enter}');
    expect(selectAlpha).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('checkbox', { name: 'Select Alpha' })).toBeChecked();

    await user.click(visibilityAlpha);
    expect(setView).toHaveBeenCalledWith({ hiddenFolders: ['alpha'] });
  });

  it('keeps folder sorting and collapse controls semantically stateful by keyboard', async () => {
    const user = userEvent.setup();
    render(<Sidebar />);

    const foldersToggle = screen.getByRole('button', { name: 'Folders' });
    const selectAlpha = await screen.findByRole('button', { name: 'Select folder Alpha, 12 images' });
    expect(foldersToggle).toHaveAttribute('aria-expanded', 'true');
    expect(selectAlpha).toBeVisible();

    foldersToggle.focus();
    await user.keyboard(' ');
    expect(foldersToggle).toHaveAttribute('aria-expanded', 'false');
    expect(selectAlpha).not.toBeVisible();

    await user.keyboard('{Enter}');
    expect(foldersToggle).toHaveAttribute('aria-expanded', 'true');
    expect(selectAlpha).toBeVisible();
    expect(screen.getByRole('button', { name: 'Sort folders A to Z' })).toHaveAttribute('aria-pressed', 'true');
    await user.click(screen.getByRole('button', { name: 'Sort folders by image count' }));
    expect(setView).toHaveBeenCalledWith({ folderSortBy: 'count-desc' });
  });
});
