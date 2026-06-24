import { describe, expect, it } from 'vitest';
import type { ImageFile } from './types';
import { removeImageSlot } from './imageListState';

function image(id: string): ImageFile {
  return {
    id,
    filename: `${id}.png`,
    absolutePath: id,
    fileUrl: `/api/image?path=${id}&thumb=true`,
    displayUrl: `/api/image?path=${id}&display=true`,
    fullUrl: `/api/image?path=${id}`,
    metadata: null,
    createdAt: 1,
    mtime: 1,
  };
}

describe('removeImageSlot', () => {
  it('removes only the deleted image and preserves unloaded placeholders', () => {
    const results = [image('a'), null, image('b'), null, image('c')];

    expect(removeImageSlot(results, 'b')).toEqual([
      image('a'),
      null,
      null,
      image('c'),
    ]);
  });

  it('keeps loaded items in place when a later unloaded placeholder can absorb the deletion', () => {
    const results = [image('a'), image('b'), image('c'), null, null];

    expect(removeImageSlot(results, 'b')).toEqual([
      image('a'),
      null,
      image('c'),
      null,
    ]);
  });

  it('returns the original array when the image is not loaded', () => {
    const results = [image('a'), null, image('c')];

    expect(removeImageSlot(results, 'b')).toBe(results);
  });
});
