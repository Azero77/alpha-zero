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
      },
      '/api/video/keys': {
        target: 'https://localhost:7016',
        secure: false,
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api\/video\/keys\/([a-f0-9\-]+)$/i, '/api/videos/$1/key')
      },
      '/api/video': {
        target: 'https://localhost:7016',
        secure: false,
        changeOrigin: true
      }
    }
  }
})
