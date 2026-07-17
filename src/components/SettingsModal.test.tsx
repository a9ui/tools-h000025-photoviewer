import React from 'react';
import { act, render, screen, waitFor, within } from '@testing-library/react';
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
const setConfirmBeforeDelete = vi.fn();
const writeText = vi.fn();

const fullRevision = '1234567890abcdef1234567890abcdef12345678';
const validRuntimePayload = {
  product: 'PhotoViewer',
  sourceRevision: fullRevision,
  sourceDirty: false,
  buildId: 'build_2026-07-18',
  buildCompletedAtUtc: '2026-07-18T01:02:03.000Z',
  serverHost: '127.0.0.1',
  serverPort: 3011,
  serverStartedAtUtc: '2026-07-18T01:03:00.000Z',
  processId: 4321,
};

function runtimeResponse(payload: unknown, ok = true) {
  return { ok, json: async () => payload } as Response;
}

function mockSettingsStore(showSettings = true) {
  vi.mocked(useImageStore).mockReturnValue({
    showSettings,
    setShowSettings,
    keyBindings: DEFAULT_KEY_BINDINGS,
    setKeyBindings,
    confirmBeforeDelete: true,
    setConfirmBeforeDelete,
    view: {
      modalEdgeRatio: 0.28,
      showUnseenMarkers: true,
    },
    setView,
  } as unknown as ReturnType<typeof useImageStore>);
}

describe('SettingsModal unseen markers setting', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSettingsStore();
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => {})));
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });
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

describe('SettingsModal runtime diagnostics', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSettingsStore();
    vi.stubGlobal('fetch', vi.fn(async () => runtimeResponse(validRuntimePayload)));
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });
  });

  it('fetches only while Settings is open and requests uncached runtime provenance', async () => {
    mockSettingsStore(false);
    const { rerender } = render(<SettingsModal />);
    expect(fetch).not.toHaveBeenCalled();

    mockSettingsStore(true);
    rerender(<SettingsModal />);

    expect(await screen.findByText('PhotoViewer')).toBeVisible();
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(fetch).toHaveBeenCalledWith('/api/runtime', {
      cache: 'no-store',
      signal: expect.any(AbortSignal),
    });
  });

  it('shows a short revision with the full value in its title and marks dirty builds', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => runtimeResponse({
      ...validRuntimePayload,
      sourceDirty: true,
    })));
    render(<SettingsModal />);

    const revision = await screen.findByText('1234567890');
    expect(revision).toHaveAttribute('title', fullRevision);
    expect(screen.getByText('Dirty')).toHaveClass('is-dirty');
    expect(screen.getByText('build_2026-07-18')).toBeVisible();
    expect(screen.getByText('2026-07-18T01:02:03.000Z')).toHaveAttribute(
      'datetime',
      '2026-07-18T01:02:03.000Z',
    );
    expect(screen.getByText('127.0.0.1:3011')).toBeVisible();
  });

  it('renders launcher-unavailable null fields without blocking normal settings', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => runtimeResponse({
      ...validRuntimePayload,
      sourceRevision: null,
      buildId: null,
      buildCompletedAtUtc: null,
      serverHost: null,
      serverPort: null,
    })));
    render(<SettingsModal />);

    const runtimeSection = screen.getByRole('region', { name: 'Runtime / Version' });
    await waitFor(() => expect(runtimeSection).toHaveTextContent('Unavailable'));
    expect(within(runtimeSection).getAllByRole('definition')).toHaveLength(6);
    expect(screen.getByRole('checkbox', { name: 'Show unseen markers' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Save key bindings' })).toBeEnabled();
  });

  it('shows invalid and failed payloads inline and recovers through Reload', async () => {
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce(runtimeResponse({
        ...validRuntimePayload,
        sourceRevision: 'C:/Users/private/revision',
      }))
      .mockResolvedValueOnce(runtimeResponse(validRuntimePayload)));
    const user = userEvent.setup();
    render(<SettingsModal />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Runtime details could not be loaded.');
    expect(screen.getByRole('checkbox', { name: 'Confirm before delete' })).toBeEnabled();
    await user.click(screen.getByRole('button', { name: 'Reload' }));

    expect(await screen.findByText('1234567890')).toBeVisible();
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('copies only safe diagnostics and reports success through the live region', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => runtimeResponse({
      ...validRuntimePayload,
      projectRoot: 'C:/Users/private/project',
      statePath: 'C:/Users/private/.cache/state.json',
    })));
    const user = userEvent.setup();
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });
    writeText.mockResolvedValue(undefined);
    render(<SettingsModal />);

    await screen.findByText('1234567890');
    await user.click(screen.getByRole('button', { name: 'Copy diagnostics' }));
    expect(await screen.findByRole('status')).toHaveTextContent('Diagnostics copied.');

    const copied = String(writeText.mock.calls[0][0]);
    expect(copied).toContain(`Source revision: ${fullRevision}`);
    expect(copied).toContain('Build ID: build_2026-07-18');
    expect(copied).toContain('Server: 127.0.0.1:3011');
    expect(copied).toContain(`Browser: ${navigator.userAgent}`);
    expect(copied).not.toContain('C:/Users/private');
    expect(copied).not.toContain('4321');
    expect(copied).not.toContain('statePath');
    expect(setView).not.toHaveBeenCalled();
    expect(setKeyBindings).not.toHaveBeenCalled();
    expect(setConfirmBeforeDelete).not.toHaveBeenCalled();
    expect(setShowSettings).not.toHaveBeenCalled();
  });

  it('reports clipboard rejection without closing or disabling Settings', async () => {
    const user = userEvent.setup();
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });
    writeText.mockRejectedValue(new Error('Permission denied'));
    render(<SettingsModal />);

    await screen.findByText('1234567890');
    await user.click(screen.getByRole('button', { name: 'Copy diagnostics' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not copy diagnostics.');
    expect(screen.getByRole('button', { name: 'Copy diagnostics' })).toBeEnabled();
    expect(screen.getByRole('checkbox', { name: 'Show unseen markers' })).toBeEnabled();
  });

  it('aborts a closed request and ignores its stale response after reopening', async () => {
    let resolveFirst!: (response: Response) => void;
    let firstSignal: AbortSignal | undefined;
    const firstResponse = new Promise<Response>((resolve) => { resolveFirst = resolve; });
    vi.stubGlobal('fetch', vi.fn()
      .mockImplementationOnce((_input: RequestInfo | URL, init?: RequestInit) => {
        firstSignal = init?.signal ?? undefined;
        return firstResponse;
      })
      .mockResolvedValueOnce(runtimeResponse({ ...validRuntimePayload, buildId: 'new-build' })));
    const { rerender } = render(<SettingsModal />);
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));

    mockSettingsStore(false);
    rerender(<SettingsModal />);
    expect(firstSignal?.aborted).toBe(true);

    mockSettingsStore(true);
    rerender(<SettingsModal />);
    expect(await screen.findByText('new-build')).toBeVisible();

    await act(async () => {
      resolveFirst(runtimeResponse({ ...validRuntimePayload, buildId: 'stale-build' }));
      await Promise.resolve();
    });
    expect(screen.queryByText('stale-build')).not.toBeInTheDocument();
    expect(screen.getByText('new-build')).toBeVisible();
  });

  it('aborts the runtime request when the Settings component unmounts', async () => {
    let requestSignal: AbortSignal | undefined;
    let resolveRequest!: (response: Response) => void;
    vi.stubGlobal('fetch', vi.fn((_input: RequestInfo | URL, init?: RequestInit) => {
      requestSignal = init?.signal ?? undefined;
      return new Promise<Response>((resolve) => { resolveRequest = resolve; });
    }));
    const view = render(<SettingsModal />);
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));

    view.unmount();
    expect(requestSignal?.aborted).toBe(true);
    await act(async () => {
      resolveRequest(runtimeResponse(validRuntimePayload));
      await Promise.resolve();
    });
  });

  it('keeps runtime actions in the dialog keyboard sequence', async () => {
    const user = userEvent.setup();
    render(<SettingsModal />);

    await screen.findByText('1234567890');
    expect(screen.getByRole('button', { name: 'Close settings' })).toHaveFocus();
    await user.tab();
    expect(screen.getByRole('button', { name: 'Reload' })).toHaveFocus();
    await user.tab();
    expect(screen.getByRole('button', { name: 'Copy diagnostics' })).toHaveFocus();
  });
});
