# e2e

Playwright-based end-to-end specs live here. The default configuration (`playwright.config.ts`) points to this directory and launches `pnpm dev` automatically, so you only need to ensure dependencies are installed.

## Commands

- `pnpm test:e2e` &mdash; run the full Playwright suite once.
- `pnpm exec playwright codegen http://localhost:3000` &mdash; record new scenarios.

`home.spec.ts` covers the PhotoViewer landing workflow and last folder set restoration. Add narrower specs for viewer interactions when a stable fixture folder is available.
