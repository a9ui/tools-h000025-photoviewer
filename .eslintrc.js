/** @type {import('eslint').Linter.Config} */
module.exports = {
  extends: ['next/core-web-vitals', 'plugin:testing-library/react', 'prettier'],
  settings: {
    next: {
      rootDir: ['.']
    }
  },
  overrides: [
    {
      files: ['src/**/*.test.{ts,tsx}', 'src/**/__tests__/**/*.{ts,tsx}'],
      extends: ['plugin:testing-library/dom']
    }
  ]
};
