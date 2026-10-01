import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/cdn-proxy': {
        target: 'http://cdn.zadmuslim.cc',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/cdn-proxy/, '')
      }
    }
  }
})
