import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';

import { afterEach, describe, expect, it } from 'vitest';

import { resolveSharedCachePath, resolveSharedProjectRoot } from './sharedProjectRoot';

const roots: string[] = [];

afterEach(async () => {
  await Promise.all(roots.splice(0).map((root) => fs.rm(root, { recursive: true, force: true })));
});

async function makeProject(root: string) {
  await fs.mkdir(path.join(root, 'local-native'), { recursive: true });
  await fs.mkdir(path.join(root, '.cache'), { recursive: true });
  await fs.writeFile(path.join(root, 'project.toml'), '[project]\n', 'utf8');
}

async function makeResolutionOptions(root: string) {
  const locatorDirectory = path.join(root, 'locator');
  await fs.mkdir(locatorDirectory, { recursive: true });
  return {
    locatorPath: path.join(locatorDirectory, 'shared-root.v1.json'),
    leaseDirectory: path.join(root, 'leases'),
  };
}

describe('shared project root resolution', () => {
  it('keeps a normal checkout as the shared state owner', async () => {
    const project = await fs.mkdtemp(path.join(os.tmpdir(), 'pvu-shared-root-main-'));
    roots.push(project);
    await makeProject(project);
    await fs.mkdir(path.join(project, '.git'));
    const options = await makeResolutionOptions(project);
    const canonicalProject = await fs.realpath(project);

    expect(resolveSharedProjectRoot(path.join(project, 'local-native'))).toBe(project);
    expect(resolveSharedCachePath('favorites.json', path.join(project, 'override.json')))
      .toBe(path.join(project, 'override.json'));
    expect(resolveSharedCachePath('favorites.json', undefined, project, options))
      .toBe(path.join(canonicalProject, '.cache', 'favorites.json'));
  });

  it('routes a linked worktree cache back to the main checkout', async () => {
    const fixture = await fs.mkdtemp(path.join(os.tmpdir(), 'pvu-shared-root-worktree-'));
    roots.push(fixture);
    const main = path.join(fixture, 'main');
    const worktree = path.join(fixture, 'linked');
    const linkedGitDir = path.join(main, '.git', 'worktrees', 'linked');
    await makeProject(main);
    await makeProject(worktree);
    await fs.mkdir(linkedGitDir, { recursive: true });
    await fs.writeFile(path.join(worktree, '.git'), `gitdir: ${linkedGitDir}\n`, 'utf8');
    await fs.writeFile(path.join(linkedGitDir, 'commondir'), '../..\n', 'utf8');
    const options = await makeResolutionOptions(fixture);
    const canonicalMain = await fs.realpath(main);

    expect(resolveSharedProjectRoot(worktree)).toBe(main);
    expect(resolveSharedCachePath('favorites.json', undefined, worktree, options))
      .toBe(path.join(canonicalMain, '.cache', 'favorites.json'));
    expect(resolveSharedCachePath('seen.json', undefined, worktree, options))
      .toBe(path.join(canonicalMain, '.cache', 'seen.json'));
    expect(resolveSharedCachePath('recent-folders.json', undefined, worktree, options))
      .toBe(path.join(canonicalMain, '.cache', 'recent-folders.json'));
    expect(resolveSharedCachePath('settings.json', undefined, worktree, options))
      .toBe(path.join(canonicalMain, '.cache', 'settings.json'));
    expect(resolveSharedCachePath('enhance', undefined, worktree, options))
      .toBe(path.join(canonicalMain, '.cache', 'enhance'));
  });
});
