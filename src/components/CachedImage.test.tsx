import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ImageRequestError } from '../lib/clientImageCache';
import CachedImage from './CachedImage';

const cacheMocks = vi.hoisted(() => ({
  evict: vi.fn(),
  load: vi.fn(),
}));

vi.mock('../lib/clientImageCache', async (importOriginal) => ({
  ...await importOriginal<typeof import('../lib/clientImageCache')>(),
  evictCachedImageUrl: cacheMocks.evict,
  getCachedImageUrl: () => null,
  loadCancellableCachedImageUrl: cacheMocks.load,
}));

describe('CachedImage expired viewer session recovery', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    cacheMocks.load.mockReturnValue({
      promise: Promise.reject(new ImageRequestError(410)),
      cancel: vi.fn(),
    });
  });

  it('notifies once and settles on a non-network placeholder instead of retrying the expired URL', async () => {
    const onSessionExpired = vi.fn();
    const { rerender } = render(
      <CachedImage
        src="/api/image?path=first&indexToken=expired"
        fallbackSrc="/api/image?path=first&indexToken=expired&full=1"
        cacheKind="display"
        alt="first.png"
        onSessionExpired={onSessionExpired}
      />
    );

    await waitFor(() => expect(onSessionExpired).toHaveBeenCalledTimes(1));
    const image = screen.getByRole('img', { name: 'first.png' });
    expect(image).toHaveAttribute('data-image-session-expired', 'true');
    expect(image.getAttribute('src')).toMatch(/^data:image\/gif/);

    rerender(
      <CachedImage
        src="/api/image?path=first&indexToken=expired"
        fallbackSrc="/api/image?path=first&indexToken=expired&full=1"
        cacheKind="display"
        alt="first.png"
        onSessionExpired={onSessionExpired}
      />
    );

    expect(onSessionExpired).toHaveBeenCalledTimes(1);
    expect(cacheMocks.load).toHaveBeenCalledTimes(1);
  });
});
