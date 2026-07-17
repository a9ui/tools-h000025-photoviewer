import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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
      expect(screen.getByRole('status', { name: 'Tag suggestion status' })).toHaveTextContent('No tag suggestions available.');
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

  it('reorders focused tags with Alt Shift arrows and keeps focus on the moved tag', async () => {
    renderSearchBar('cat, dog, castle');

    const catChip = screen.getByRole('listitem', { name: /Search tag cat, position 1 of 3/i });
    catChip.focus();
    fireEvent.keyDown(catChip, { key: 'ArrowRight', altKey: true, shiftKey: true });

    const movedCat = screen.getByRole('listitem', { name: /Search tag cat, position 2 of 3/i });
    expect(movedCat).toHaveFocus();
    expect(movedCat).toHaveAttribute('aria-posinset', '2');
    expect(movedCat).toHaveAttribute('aria-setsize', '3');
    expect(screen.getByRole('status', { name: 'Search tag arrangement' }))
      .toHaveTextContent('Moved tag cat to position 2 of 3.');
    await waitFor(() => expect(setSearchQuery).toHaveBeenCalledWith('dog, cat, castle'));

    fireEvent.keyDown(movedCat, { key: 'ArrowLeft', altKey: true, shiftKey: true });
    const restoredCat = screen.getByRole('listitem', { name: /Search tag cat, position 1 of 3/i });
    expect(restoredCat).toHaveFocus();
    expect(restoredCat).toHaveAttribute('aria-posinset', '1');
    expect(screen.getByRole('status', { name: 'Search tag arrangement' }))
      .toHaveTextContent('Moved tag cat to position 1 of 3.');
  });

  it('removes only the focused tag with Delete and moves focus to the next chip', async () => {
    renderSearchBar('cat, dog, castle');

    const dogChip = screen.getByRole('listitem', { name: /Search tag dog, position 2 of 3/i });
    dogChip.focus();
    fireEvent.keyDown(dogChip, { key: 'Delete' });

    expect(screen.queryByRole('listitem', { name: /Search tag dog/i })).not.toBeInTheDocument();
    expect(screen.getByRole('listitem', { name: /Search tag castle, position 2 of 2/i })).toHaveFocus();
    expect(screen.getByRole('listitem', { name: /Search tag cat, position 1 of 2/i })).toBeInTheDocument();
    expect(screen.getByRole('status', { name: 'Search tag arrangement' })).toHaveTextContent('Removed tag dog.');
    await waitFor(() => expect(setSearchQuery).toHaveBeenCalledWith('cat, castle'));
  });

  it('removes the final focused tag with Backspace and returns focus to the input', async () => {
    renderSearchBar('cat');

    const catChip = screen.getByRole('listitem', { name: /Search tag cat, position 1 of 1/i });
    catChip.focus();
    fireEvent.keyDown(catChip, { key: 'Backspace' });

    expect(screen.queryByRole('listitem')).not.toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Search tags' })).toHaveFocus();
    expect(screen.getByRole('status', { name: 'Search tag arrangement' })).toHaveTextContent('Removed tag cat.');
    await waitFor(() => expect(setSearchQuery).toHaveBeenCalledWith(''));
  });

  it('keeps duplicate external query tags independently addressable', async () => {
    renderSearchBar('cat, cat, dog');

    const secondCat = screen.getByRole('listitem', { name: /Search tag cat, position 2 of 3/i });
    secondCat.focus();
    fireEvent.keyDown(secondCat, { key: 'ArrowRight', altKey: true, shiftKey: true });

    const movedDuplicate = screen.getByRole('listitem', { name: /Search tag cat, position 3 of 3/i });
    expect(movedDuplicate).toHaveFocus();
    await waitFor(() => expect(setSearchQuery).toHaveBeenCalledWith('cat, dog, cat'));

    fireEvent.keyDown(movedDuplicate, { key: 'Delete' });

    expect(screen.getAllByRole('listitem')).toHaveLength(2);
    expect(screen.getAllByText('cat')).toHaveLength(1);
    expect(screen.getByRole('listitem', { name: /Search tag cat, position 1 of 2/i })).toBeInTheDocument();
    expect(screen.getByRole('listitem', { name: /Search tag dog, position 2 of 2/i })).toHaveFocus();
  });

  it('lets the remove button own Enter and Space without triggering chip keyboard actions', async () => {
    const user = userEvent.setup();
    renderSearchBar('cat, dog, castle');

    const removeCat = screen.getByRole('button', { name: 'Remove tag cat' });
    removeCat.focus();
    await user.keyboard('{Enter}');

    expect(screen.queryByRole('listitem', { name: /Search tag cat/i })).not.toBeInTheDocument();
    expect(screen.getByRole('listitem', { name: /Search tag dog, position 1 of 2/i })).toBeInTheDocument();
    expect(screen.getByRole('status', { name: 'Search tag arrangement' })).toHaveTextContent('Removed tag cat.');

    const removeDog = screen.getByRole('button', { name: 'Remove tag dog' });
    removeDog.focus();
    await user.keyboard(' ');

    expect(screen.queryByRole('listitem', { name: /Search tag dog/i })).not.toBeInTheDocument();
    expect(screen.getByRole('listitem', { name: /Search tag castle, position 1 of 1/i })).toBeInTheDocument();
    expect(screen.getByRole('status', { name: 'Search tag arrangement' })).toHaveTextContent('Removed tag dog.');
  });
});
