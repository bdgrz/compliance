import { askr } from '@askrjs/vite';
import { defineConfig } from 'vite-plus';

export default defineConfig({
  plugins: [askr()],
  resolve: { preserveSymlinks: true },
  lint: {
    ignorePatterns: ['dist/**', 'node_modules/**'],
  },
  fmt: {
    semi: true,
    singleQuote: true,
    trailingComma: 'es5',
    printWidth: 80,
    tabWidth: 2,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://127.0.0.1:5080',
      '/auth': 'http://127.0.0.1:5080',
      '/healthz': 'http://127.0.0.1:5080',
      '/openapi': 'http://127.0.0.1:5080',
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
});
