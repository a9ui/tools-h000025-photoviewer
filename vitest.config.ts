import { defineConfig } from 'vitest/config';
import tsconfigPaths from 'vite-tsconfig-paths';

export default defineConfig({
  plugins: [tsconfigPaths()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
    include: ['src/**/*.test.{ts,tsx}', 'scripts/perf/**/*.test.mjs'],
    coverage: {
      provider: 'istanbul',
      reporter: ['text', 'html', 'lcov']
    }
  }
});
