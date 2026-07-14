import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import Sidebar from './Sidebar';
import { useImageStore } from '../store/ImageContext';

vi.mock('../store/ImageContext', () => ({
  useImageStore: vi.fn(),
}));

const toggleFavoriteFilterLevel = vi.fn();
const clearFavoriteFilterLevels = vi.fn();

function createStore() {
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
      hiddenFolders: [],
      showUnseenMarkers: false,
    },
    setView: vi.fn(),
    setSearchQuery: vi.fn(),
    dirPath: '',
    setDirPath: vi.fn(),
    startScan: vi.fn(),
    totalIndexed: 0,
    searchTotal: 0,
    searchQuery: '',
    showFavOnly: true,
    setShowFavOnly: vi.fn(),
    showUnfavOnly: false,
    setShowUnfavOnly: vi.fn(),
    favoriteFilterLevels: [2, 4],
    toggleFavoriteFilterLevel,
    clearFavoriteFilterLevels,
    showEnhancedOnly: false,
    setShowEnhancedOnly: vi.fn(),
    setShowSettings: vi.fn(),
    setPhase: vi.fn(),
    perfEnabled: false,
    setPerfEnabled: vi.fn(),
    perfStats: { searchCount: 0, lastSearchMs: 0, avgSearchMs: 0 },
  } as unknown as ReturnType<typeof useImageStore>;
}

describe('Sidebar favorite level controls', () => {
  beforeEach(() => {
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
