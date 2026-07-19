import { expect, test, type Page } from '@playwright/test';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const PNG_1X1 = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
  'base64',
);

type WarmupObservation = {
  priority: string;
  seenAt: number;
};

async function openFixtureFolder(page: Page, fixtureDir: string) {
  await page.goto('/');
  await page.evaluate(() => {
    const markViewerReady = () => {
      if (!document.querySelector('.viewer')) return false;
      (window as Window & { __pvViewerFirstSeenAt?: number }).__pvViewerFirstSeenAt = Date.now();
      return true;
    };
    if (markViewerReady()) return;
    const observer = new MutationObserver(() => {
      if (!markViewerReady()) return;
      observer.disconnect();
    });
    observer.observe(document.body, { childList: true, subtree: true });
  });

  await page.getByPlaceholder('Paste one absolute path per line...').fill(fixtureDir);
  await page.getByRole('button', { name: 'Add pasted' }).click();
  await page.getByRole('button', { name: 'Open folder set', exact: true }).click();

  await expect(page.locator('.viewer')).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('.viewer-header .header-stats')).toContainText('96', { timeout: 15_000 });
  await expect(page.locator('.image-card[role="group"]').first()).toBeVisible();
}

test.describe('viewer grid zoom runtime contract', () => {
  let fixtureDir = '';

  test.beforeAll(async () => {
    fixtureDir = await mkdtemp(join(tmpdir(), 'photoviewer-grid-zoom-e2e-'));
    await Promise.all(Array.from({ length: 96 }, (_, index) => (
      writeFile(join(fixtureDir, `fixture-${String(index).padStart(3, '0')}.png`), PNG_1X1)
    )));
  });

  test.afterAll(async ({ request }) => {
    await expect.poll(async () => {
      const response = await request.get('/api/thumbs/warm');
      if (!response.ok()) return false;
      const payload = await response.json() as {
        warmup?: {
          running?: boolean;
          activeThumbJobs?: number;
          pendingThumbs?: number;
          queuedThumbJobs?: number;
        };
      };
      const warmup = payload.warmup;
      return Boolean(warmup) &&
        warmup?.running === false &&
        warmup.activeThumbJobs === 0 &&
        warmup.pendingThumbs === 0 &&
        warmup.queuedThumbJobs === 0;
    }, { timeout: 15_000 }).toBe(true);

    const resolvedFixture = resolve(fixtureDir);
    const resolvedTemp = resolve(tmpdir());
    if (!resolvedFixture.startsWith(`${resolvedTemp}\\photoviewer-grid-zoom-e2e-`)) {
      throw new Error(`Refusing to remove unexpected fixture path: ${resolvedFixture}`);
    }
    await rm(resolvedFixture, { recursive: true, force: true });
  });

  test('prioritizes visible thumbnails and keeps the selected card plus fixed sidebar anchored through Ctrl+Plus', async ({ page }) => {
    const warmups: WarmupObservation[] = [];
    page.on('request', (request) => {
      if (!request.url().includes('/api/thumbs/warm') || request.method() !== 'POST') return;
      try {
        const body = request.postDataJSON() as { priority?: unknown };
        warmups.push({
          priority: typeof body.priority === 'string' ? body.priority : '',
          seenAt: Date.now(),
        });
      } catch {
        warmups.push({ priority: '', seenAt: Date.now() });
      }
    });

    await openFixtureFolder(page, fixtureDir);

    await expect.poll(() => warmups.some((item) => item.priority === 'visible')).toBe(true);
    await expect.poll(() => warmups.some((item) => item.priority === 'nearby')).toBe(true);

    const firstVisible = warmups.findIndex((item) => item.priority === 'visible');
    const firstNearby = warmups.findIndex((item) => item.priority === 'nearby');
    expect(firstVisible).toBeGreaterThanOrEqual(0);
    expect(firstNearby).toBeGreaterThanOrEqual(0);
    expect(firstVisible).toBeLessThan(firstNearby);

    const viewerFirstSeenAt = await page.evaluate(() => (
      (window as Window & { __pvViewerFirstSeenAt?: number }).__pvViewerFirstSeenAt ?? 0
    ));
    expect(viewerFirstSeenAt).toBeGreaterThan(0);
    expect(warmups[firstVisible].seenAt - viewerFirstSeenAt).toBeLessThan(1_000);

    const visibleThumb = page.locator('.image-card img').first();
    await expect(visibleThumb).toHaveAttribute('loading', 'eager');
    await expect(visibleThumb).toHaveAttribute('fetchpriority', 'high');
    await expect(visibleThumb).toHaveAttribute('src', /[?&]priority=visible(?:&|$)/);

    const viewerMain = page.locator('.viewer-main');
    await viewerMain.evaluate((element) => {
      element.scrollTop = 1_200;
      element.dispatchEvent(new Event('scroll'));
    });
    await expect.poll(() => viewerMain.evaluate((element) => element.scrollTop)).toBeGreaterThan(1_000);

    const visibleCards = page.locator('.image-card[role="group"]');
    await expect.poll(() => visibleCards.count()).toBeGreaterThan(3);
    const selectedCard = visibleCards.nth(2);
    const selectedGridIndex = await selectedCard.getAttribute('data-grid-index');
    expect(selectedGridIndex).not.toBeNull();
    await selectedCard.locator('[data-image-primary="true"]').click();

    const stableSelectedCard = page.locator(`.image-card[data-grid-index="${selectedGridIndex}"]`);
    await expect(stableSelectedCard).toHaveClass(/is-selected/);
    const selectedPrimary = stableSelectedCard.locator('[data-image-primary="true"]');
    await expect(selectedPrimary).toHaveAttribute('aria-pressed', 'true');

    const sidebar = page.locator('.sidebar');
    const slider = page.getByRole('slider', { name: 'Thumbnail size' });
    const before = await page.evaluate(() => {
      const sidebarElement = document.querySelector<HTMLElement>('.sidebar');
      const selectedElement = document.querySelector<HTMLElement>('.image-card.is-selected');
      if (!sidebarElement || !selectedElement) throw new Error('Expected viewer zoom surfaces');
      const sidebarRect = sidebarElement.getBoundingClientRect();
      const selectedRect = selectedElement.getBoundingClientRect();
      const sidebarStyle = getComputedStyle(sidebarElement);
      return {
        innerWidth: window.innerWidth,
        devicePixelRatio: window.devicePixelRatio,
        sidebar: {
          left: sidebarRect.left,
          top: sidebarRect.top,
          width: sidebarRect.width,
          height: sidebarRect.height,
          fontSize: sidebarStyle.fontSize,
        },
        selectedTop: selectedRect.top,
      };
    });
    const previousThumbSize = Number(await slider.inputValue());

    await selectedPrimary.focus();
    await page.keyboard.press('Control+=');
    await expect(slider).toHaveValue(String(previousThumbSize + 20));
    await expect(stableSelectedCard).toHaveClass(/is-selected/);

    await expect.poll(async () => {
      const selectedTop = await stableSelectedCard.evaluate((element) => element.getBoundingClientRect().top);
      return Math.abs(selectedTop - before.selectedTop);
    }).toBeLessThanOrEqual(1);

    const after = await page.evaluate(() => {
      const sidebarElement = document.querySelector<HTMLElement>('.sidebar');
      if (!sidebarElement) throw new Error('Expected fixed sidebar');
      const rect = sidebarElement.getBoundingClientRect();
      const style = getComputedStyle(sidebarElement);
      return {
        innerWidth: window.innerWidth,
        devicePixelRatio: window.devicePixelRatio,
        sidebar: {
          left: rect.left,
          top: rect.top,
          width: rect.width,
          height: rect.height,
          fontSize: style.fontSize,
        },
      };
    });

    expect(after).toEqual({
      innerWidth: before.innerWidth,
      devicePixelRatio: before.devicePixelRatio,
      sidebar: before.sidebar,
    });
    await expect(sidebar).toBeVisible();
  });
});
