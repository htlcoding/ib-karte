import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// Im Entwicklungsbetrieb leitet Vite /api an das C#-Backend weiter.
export default defineConfig({
  plugins: [vue()],
  server: { proxy: { '/api': process.env.API_URL ?? 'http://localhost:5207' } },
  preview: { proxy: { '/api': process.env.API_URL ?? 'http://localhost:5207' } },
  build: { chunkSizeWarningLimit: 1400 },
  worker: { format: 'es' },
})
