/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

/**
 * The bundle is emitted into the API project's wwwroot rather than a local dist/.
 *
 * This is not a packaging convenience: the section 2 auth decision is a `SameSite=Strict`,
 * `HttpOnly` cookie, which requires the SPA and the API to be same-origin. PMS.Api therefore
 * serves the built bundle in every environment, and the dev server proxies /api so that
 * development behaves the same way.
 */
const API_DEV_ORIGIN = 'https://localhost:7191';

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../backend/src/PMS.Api/wwwroot',
    emptyOutDir: true,
    sourcemap: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: API_DEV_ORIGIN,
        changeOrigin: false,
        // The ASP.NET Core dev certificate is self-signed; trust it for the proxy only.
        secure: false,
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
    css: false,
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
  },
});
