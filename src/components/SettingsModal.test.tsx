import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import SettingsModal from './SettingsModal';
import { useImageStore } from '../store/ImageContext';
import { DEFAULT_KEY_BINDINGS } from '../lib/types';

vi.mock('../store/ImageContext', () => ({
  useImageStore: vi.fn(),
}));

const setView = vi.fn();

describe('SettingsModal unseen markers setting', () => {
  beforeEach(() => {
    setView.mockReset();
    vi.mocked(useImageStore).mockReturnValue({
      showSettings: true,
      setShowSettings: vi.fn(),
      keyBindings: DEFAULT_KEY_BINDINGS,
      setKeyBindings: vi.fn(),
      confirmBeforeDelete: true,
      setConfirmBeforeDelete: vi.fn(),
      view: {
        modalEdgeRatio: 0.28,
        showUnseenMarkers: true,
      },
      setView,
    } as unknown as ReturnType<typeof useImageStore>);
  });

  it('reflects the current value and updates it through the checkbox', async () => {
    const user = userEvent.setup();
    render(<SettingsModal />);

    const checkbox = screen.getByRole('checkbox', { name: 'Show unseen markers' });
    expect(checkbox).toBeChecked();

    await user.click(checkbox);
    expect(setView).toHaveBeenCalledWith({ showUnseenMarkers: false });
  });
});
