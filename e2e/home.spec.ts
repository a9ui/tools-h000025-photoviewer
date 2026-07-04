import { test, expect } from '@playwright/test';

test('landing page exposes the PhotoViewer folder workflow', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('PhotoViewer');
  await expect(page.getByRole('button', { name: 'Add folder' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Open folder set' })).toBeDisabled();
  await expect(page.getByPlaceholder('Paste one absolute path per line...')).toBeVisible();
});

test('landing page restores the last folder set from local storage', async ({ page }) => {
  await page.goto('/');
  await page.evaluate(() => {
    const folder = String.raw`C:\Users\a9ui\Pictures`;
    localStorage.setItem('pvu_last_dir_set', folder);
    localStorage.setItem('pvu_recent_dirs', JSON.stringify([folder]));
  });
  await expect(page.evaluate(() => localStorage.getItem('pvu_last_dir_set'))).resolves.toContain('Pictures');
  await page.reload();
  await expect(page.evaluate(() => localStorage.getItem('pvu_last_dir_set'))).resolves.toContain('Pictures');

  const lastFolderButton = page.getByRole('button', { name: /Open last folder set/ });
  await expect(lastFolderButton).toBeVisible();
  await expect(lastFolderButton).toContainText('Pictures');
  await expect(page.getByText('Recent folder sets')).toBeVisible();
});
