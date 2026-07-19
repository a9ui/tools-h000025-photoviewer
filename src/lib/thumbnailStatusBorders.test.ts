import { describe, expect, it } from 'vitest';

import { DEFAULT_THUMBNAIL_STATUS_BORDERS } from './types';
import {
  getThumbnailStatusBorderPresentation,
  isValidThumbnailStatusBordersDocument,
  normalizeThumbnailStatusBorders,
} from './thumbnailStatusBorders';

describe('thumbnail status borders', () => {
  it('defaults both independent indicators on with the requested yellow', () => {
    expect(normalizeThumbnailStatusBorders(undefined)).toEqual(DEFAULT_THUMBNAIL_STATUS_BORDERS);
    expect(getThumbnailStatusBorderPresentation({
      favorite: true,
      enhanced: true,
      settings: normalizeThumbnailStatusBorders(undefined),
    })).toEqual({ favoriteColor: '#facc15', enhancedColor: '#facc15' });
  });

  it('keeps disabled indicators off without hiding the other status', () => {
    const settings = normalizeThumbnailStatusBorders({
      favorite: { enabled: false, color: '#ff0000' },
      enhanced: { enabled: true, color: '#00ff00' },
    });
    expect(getThumbnailStatusBorderPresentation({ favorite: true, enhanced: true, settings }))
      .toEqual({ favoriteColor: null, enhancedColor: '#00ff00' });
  });

  it('normalizes custom colors and retains defaults for missing fields', () => {
    expect(normalizeThumbnailStatusBorders({
      favorite: { color: '#ABCDEF' },
      futureStatus: { enabled: true },
    })).toEqual({
      favorite: { enabled: true, color: '#abcdef' },
      enhanced: { enabled: true, color: '#facc15' },
    });
  });

  it('rejects invalid known values while allowing unknown future fields', () => {
    expect(isValidThumbnailStatusBordersDocument({ favorite: { enabled: 'yes' } })).toBe(false);
    expect(isValidThumbnailStatusBordersDocument({ enhanced: { color: 'yellow' } })).toBe(false);
    expect(isValidThumbnailStatusBordersDocument({ futureStatus: { color: 'anything' } })).toBe(true);
  });
});
