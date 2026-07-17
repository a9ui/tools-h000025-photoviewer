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
const setKeyBindings = vi.fn();
const setShowSettings = vi.fn();

describe('SettingsModal unseen markers setting', () => {
  beforeEach(() => {
    setView.mockReset();
    setKeyBindings.mockReset();
    setShowSettings.mockReset();
    vi.mocked(useImageStore).mockReturnValue({
      showSettings: true,
      setShowSettings,
      keyBindings: DEFAULT_KEY_BINDINGS,
      setKeyBindings,
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

  it('shows conflicting fields inline and never saves the invalid draft on close', async () => {
    const user = userEvent.setup();
    render(<SettingsModal />);

    await user.click(screen.getByRole('button', { name: 'Next image binding' }));
    await user.keyboard('f');

    const nextBinding = screen.getByRole('button', { name: 'Next image binding' });
    const favoriteBinding = screen.getByRole('button', { name: 'Toggle favorite binding' });
    expect(nextBinding).toHaveAttribute('aria-invalid', 'true');
    expect(favoriteBinding).toHaveAttribute('aria-invalid', 'true');
    expect(nextBinding).toHaveAttribute('aria-describedby', 'key-binding-error-nextImage');
    expect(screen.getByText('Also assigned to Toggle favorite.')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Save key bindings' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Close settings' }));
    expect(setKeyBindings).not.toHaveBeenCalled();
    expect(setShowSettings).toHaveBeenCalledWith(false);
  });

  it('resets bindings only through the explicit action, then saves a conflict-free draft', async () => {
    const user = userEvent.setup();
    render(<SettingsModal />);

    await user.click(screen.getByRole('button', { name: 'Next image binding' }));
    await user.keyboard('f');
    await user.click(screen.getByRole('button', { name: 'Reset to defaults' }));

    expect(screen.queryByText('Also assigned to Toggle favorite.')).not.toBeInTheDocument();
    const save = screen.getByRole('button', { name: 'Save key bindings' });
    expect(save).toBeEnabled();
    await user.click(save);
    expect(setKeyBindings).toHaveBeenCalledWith(DEFAULT_KEY_BINDINGS);
    expect(setShowSettings).toHaveBeenCalledWith(false);
  });

  it('keeps Escape as a dialog close while capture is active', async () => {
    const user = userEvent.setup();
    render(<SettingsModal />);

    await user.click(screen.getByRole('button', { name: 'Next image binding' }));
    await user.keyboard('{Escape}');

    expect(setShowSettings).toHaveBeenCalledWith(false);
    expect(setKeyBindings).not.toHaveBeenCalled();
  });
});
