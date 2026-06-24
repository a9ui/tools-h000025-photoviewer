# e2e

Playwright-based end-to-end specs live here. The default configuration (`playwright.config.ts`) points to this directory and launches `pnpm dev` automatically, so you only need to ensure dependencies are installed.

## Commands

- `pnpm test:e2e` &mdash; run the full Playwright suite once.
- `pnpm exec playwright codegen http://localhost:3000` &mdash; record new scenarios.

The sample `home.spec.ts` demonstrates asserting the landing page copy. Feel free to add more specs and tailor devices or base URLs via environment variables.
