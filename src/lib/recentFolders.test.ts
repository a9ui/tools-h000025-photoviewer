import { describe, expect, it } from 'vitest';

import {
  buildSharedRecentFolders,
  normalizeSharedRecentFolders,
  sharedRecentToLocalMemory,
} from './recentFolders';

describe('shared recent folders', () => {
  it('normalizes shared recent folder sets into positive unique folder sets', () => {
    const shared = normalizeSharedRecentFolders({
      version: 1,
      lastFolderSet: ['C:/Images', 'C:/Images'],
      recentFolderSets: [
        ['C:/Images'],
        ['C:/Other', ''],
        ['c:/images'],
        [],
        ['C:/Third'],
      ],
      updatedAtUtc: '2026-07-09T00:00:00Z',
    });

    expect(shared).toMatchObject({
      version: 1,
      lastFolderSet: ['C:/Images'],
      recentFolderSets: [['C:/Images'], ['C:/Other'], ['C:/Third']],
      updatedAtUtc: '2026-07-09T00:00:00Z',
    });
  });

  it('builds shared JSON from browser local folder memory', () => {
    const shared = buildSharedRecentFolders({
      lastDirSet: 'C:/Selected\nC:/Extra',
      recentDirs: ['C:/Old', 'C:/Selected\nC:/Extra', 'C:/Another'],
    }, '2026-07-09T01:02:03Z');

    expect(shared).toEqual({
      version: 1,
      lastFolderSet: ['C:/Selected', 'C:/Extra'],
      recentFolderSets: [
        ['C:/Selected', 'C:/Extra'],
        ['C:/Old'],
        ['C:/Another'],
      ],
      updatedAtUtc: '2026-07-09T01:02:03Z',
    });
  });

  it('maps shared JSON back to browser localStorage shapes', () => {
    const memory = sharedRecentToLocalMemory({
      version: 1,
      lastFolderSet: ['C:/Selected', 'C:/Extra'],
      recentFolderSets: [['C:/Selected', 'C:/Extra'], ['C:/Old']],
      updatedAtUtc: '2026-07-09T01:02:03Z',
    });

    expect(memory).toEqual({
      lastDirSet: 'C:/Selected\nC:/Extra',
      recentDirs: ['C:/Selected\nC:/Extra', 'C:/Old'],
    });
  });
});
