import React from 'react';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ImageFile } from '../lib/types';
import { useImageStore } from '../store/ImageContext';
import BottomPreviewTabs from './BottomPreviewTabs';

vi.mock('../store/ImageContext', () => ({
  useImageStore: vi.fn(),
}));

vi.mock('./CachedImage', () => ({
  default: ({ alt }: { alt: string }) => <span data-testid="bottom-preview-hover-image" data-image-alt={alt} />,
}));

const firstImage: ImageFile = {
  id: 'C:/images/first.png',
  filename: 'first.png',
  absolutePath: 'C:/images/first.png',
  fileUrl: '/api/image?first',
  displayUrl: '/api/image?first&display=1',
  fullUrl: '/api/image?first&full=1',
  metadata: { prompt: '', negativePrompt: '', settings: {} },
  createdAt: 1,
  mtime: 1,
};

const secondImage: ImageFile = {
  ...firstImage,
  id: 'C:/images/second.png',
  filename: 'second.png',
  absolutePath: 'C:/images/second.png',
};

const setActivePreviewId = vi.fn();
const setSelectedIndex = vi.fn();
const closePreviewTab = vi.fn();
const togglePinPreviewTab = vi.fn();
const restoreLastClosedPreview = vi.fn();

function createStore(options: { activeId?: string; pinnedIds?: string[] } = {}) {
  return {
    previewTabIds: [firstImage.id, secondImage.id],
    activePreviewId: options.activeId ?? firstImage.id,
    previewById: {
      [firstImage.id]: firstImage,
      [secondImage.id]: secondImage,
    },
    searchResults: [firstImage, secondImage],
    pinnedPreviewIds: options.pinnedIds ?? [],
    setActivePreviewId,
    setSelectedIndex,
    closePreviewTab,
    togglePinPreviewTab,
    restoreLastClosedPreview,
  } as unknown as ReturnType<typeof useImageStore>;
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(useImageStore).mockReturnValue(createStore());
});

describe('BottomPreviewTabs', () => {
  it('uses a roving tab stop and activates the focused preview with keyboard', () => {
    render(<BottomPreviewTabs />);

    const firstTab = screen.getByRole('tab', { name: /open preview first\.png/i });
    const secondTab = screen.getByRole('tab', { name: /open preview second\.png/i });
    expect(firstTab).toHaveAttribute('aria-selected', 'true');
    expect(firstTab).toHaveAttribute('tabindex', '0');

    firstTab.focus();
    fireEvent.keyDown(firstTab, { key: 'ArrowRight' });
    expect(secondTab).toHaveFocus();
    expect(secondTab).toHaveAttribute('tabindex', '0');

    fireEvent.keyDown(secondTab, { key: 'Enter' });
    expect(setActivePreviewId).toHaveBeenCalledWith(secondImage.id);
    expect(setSelectedIndex).toHaveBeenCalledWith(1);

    fireEvent.keyDown(secondTab, { key: ' ' });
    expect(setActivePreviewId).toHaveBeenLastCalledWith(secondImage.id);

    fireEvent.keyDown(secondTab, { key: 'Home' });
    expect(firstTab).toHaveFocus();
    fireEvent.keyDown(firstTab, { key: 'End' });
    expect(secondTab).toHaveFocus();
  });

  it('keeps pin and close as independently reachable sibling controls', () => {
    render(<BottomPreviewTabs />);

    const firstTab = screen.getByRole('tab', { name: /open preview first\.png/i });
    const pin = screen.getByRole('button', { name: /pin preview first\.png/i });
    const close = screen.getByRole('button', { name: /close preview first\.png/i });

    expect(within(firstTab).queryByRole('button')).not.toBeInTheDocument();

    fireEvent.click(pin);
    expect(togglePinPreviewTab).toHaveBeenCalledWith(firstImage.id);
    expect(setActivePreviewId).not.toHaveBeenCalled();

    fireEvent.click(close);
    expect(closePreviewTab).toHaveBeenCalledWith(firstImage.id);
  });

  it('closes a preview from the tab middle click without activating it', () => {
    render(<BottomPreviewTabs />);

    fireEvent(
      screen.getByRole('tab', { name: /open preview second\.png/i }),
      new MouseEvent('auxclick', { bubbles: true, button: 1 })
    );

    expect(closePreviewTab).toHaveBeenCalledWith(secondImage.id);
    expect(setActivePreviewId).not.toHaveBeenCalled();
  });

  it('shows and clears the hover preview from the complete tab surface', () => {
    render(<BottomPreviewTabs />);
    const firstTab = screen.getByRole('tab', { name: /open preview first\.png/i });

    fireEvent.mouseEnter(firstTab);
    expect(screen.getByTestId('bottom-preview-hover-image')).toHaveAttribute('data-image-alt', 'first.png');

    fireEvent.mouseLeave(firstTab);
    expect(screen.queryByTestId('bottom-preview-hover-image')).not.toBeInTheDocument();
  });
});
