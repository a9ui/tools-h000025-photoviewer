import { test, expect } from '@playwright/test';

test('home page renders hero content', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Welcome to the 0000 template');
  await expect(page.getByText('Opinionated defaults, strict TypeScript', { exact: false })).toBeVisible();
  await expect(page.getByRole('link', { name: 'View docs' })).toHaveAttribute('href', 'https://nextjs.org/docs');
});
