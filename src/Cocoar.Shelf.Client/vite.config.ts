import dns from 'node:dns'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/postcss'
import { fileURLToPath, URL } from 'node:url'

// Force IPv4 first — prevents ECONNREFUSED when backend binds 0.0.0.0
dns.setDefaultResultOrder('ipv4first')

export default defineConfig({
  base: '/',
  plugins: [vue()],
  css: {
    postcss: {
      plugins: [tailwindcss()],
    },
  },
  build: {
    outDir: '../Cocoar.Shelf/wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/_api': {
        target: 'http://127.0.0.1:8080',
        secure: false,
        changeOrigin: true,
      },
      // OIDC login/logout + callbacks are server routes, not SPA routes. changeOrigin stays OFF
      // so the backend sees the :5173 host and generates the dev-server redirect_uri — the whole
      // flow then stays on the vite origin.
      '^/(login|logout|signin-oidc|signout-callback-oidc)': {
        target: 'http://127.0.0.1:8080',
        secure: false,
      },
    },
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      '@cocoar/vue-data-grid/styles': fileURLToPath(new URL('./node_modules/@cocoar/vue-data-grid/dist/index.css', import.meta.url)),
    },
  },
})
