import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useImageStore } from '../store/ImageContext';
import SearchBar from './SearchBar';

vi.mock('../store/ImageContext', () => ({
  useImageStore: vi.fn(),
}));

const setSearchQuery = vi.fn();

function renderSearchBar(searchQuery = '') {
  vi.mocked(useImageStore).mockReturnValue({
    searchQuery,
    setSearchQuery,
  } as unknown as ReturnType<typeof useImageStore>);
  return render(<SearchBar />);
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal('fetch', vi.fn(() => Promise.resolve({
    json: () => Promise.resolve({
      tags: [
        { tag: 'cat', count: 12 },
        { tag: 'castle', count: 7 },
        { tag: 'dog', count: 4 },
      ],
    }),
  })));
});

describe('SearchBar accessibility', () => {
  it('exposes suggestions through a combobox and selects the active option with keyboard', async () => {
    renderSearchBar();

    const input = screen.getByRole('combobox', { name: 'Search tags' });
    fireEvent.change(input, { target: { value: 'ca' } });

    const listbox = await screen.findByRole('listbox', { name: 'Tag suggestions' });
    const options = screen.getAllByRole('option');
    expect(listbox).toContainElement(options[0]);
    expect(input).toHaveAttribute('aria-expanded', 'true');
    expect(input).toHaveAttribute('aria-controls', listbox.id);

    fireEvent.keyDown(input, { key: 'ArrowDown' });
    expect(options[0]).toHaveAttribute('aria-selected', 'true');
    expect(input).toHaveAttribute('aria-activedescendant', options[0].id);

    fireEvent.keyDown(input, { key: 'Enter' });
    expect(screen.getByText('cat')).toBeInTheDocument();
    expect(input).toHaveFocus();
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('closes the listbox with Escape and announces an empty suggestion result', async () => {
    renderSearchBar();

    const input = screen.getByRole('combobox', { name: 'Search tags' });
    fireEvent.change(input, { target: { value: 'ca' } });
    await screen.findByRole('listbox', { name: 'Tag suggestions' });
    fireEvent.keyDown(input, { key: 'ArrowDown' });
    fireEvent.keyDown(input, { key: 'Escape' });

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(input).toHaveAttribute('aria-expanded', 'false');
    expect(input).not.toHaveAttribute('aria-activedescendant');

    fireEvent.change(input, { target: { value: 'unmatched' } });
    await waitFor(() => {
      expect(screen.getByRole('status')).toHaveTextContent('No tag suggestions available.');
    });
    expect(input).toHaveAttribute('aria-expanded', 'false');
  });

  it('keeps clear and committed-tag removal controls labeled after icon rendering', () => {
    renderSearchBar('cat');

    expect(screen.getByRole('button', { name: 'Clear all search tags' })).toBeInTheDocument();
    const removeTag = screen.getByRole('button', { name: 'Remove tag cat' });
    expect(removeTag).toHaveAttribute('title', 'Remove cat');

    fireEvent.click(removeTag);
    expect(screen.queryByRole('button', { name: 'Remove tag cat' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Clear all search tags' })).not.toBeInTheDocument();
  });
});
